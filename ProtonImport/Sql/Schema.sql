-- =============================================================================
-- Photon / ProtonImport database schema updates and database objects.
--
-- Run by ProtonImport at the start of every metadata load and import
-- (DatabaseSchema.Apply). Every batch is idempotent, so running it repeatedly
-- is safe. Batches are separated by GO; a failing batch is logged and the rest
-- still run.
--
-- Tables themselves are created by EF Core (Proton2Context / EnsureCreated).
-- =============================================================================


-- -----------------------------------------------------------------------------
-- Entities.LastUpdated: smalldatetime rounds to the nearest minute, which can
-- round the import watermark (MAX(LastUpdated)) up past entities changed later
-- in that minute, so they are never re-imported. Proton stores milliseconds.
-- -----------------------------------------------------------------------------
IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.Entities') AND c.name = N'LastUpdated' AND t.name = N'smalldatetime')
    ALTER TABLE dbo.Entities ALTER COLUMN LastUpdated datetime2(3) NOT NULL;
GO


-- -----------------------------------------------------------------------------
-- Foreign keys: the importer disables them for every load (NOCHECK), which
-- leaves them untrusted, so they neither enforce anything nor help the query
-- optimiser. Dropped so the schema says what is actually true.
-- Limited to the importer's own tables.
-- -----------------------------------------------------------------------------
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.'
             + QUOTENAME(OBJECT_NAME(fk.parent_object_id))
             + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' + CHAR(10)
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.parent_object_id) IN (
    N'Attributes', N'DataTypes', N'Entities', N'EntityTypes', N'Indexes', N'IndexTypes',
    N'Lookups', N'LookupTypes', N'Menus', N'MenuItems', N'Tables', N'UserStarters',
    N'ValueDates', N'ValueTimes', N'ValueEntities', N'ValueLookups', N'ValueNumbers',
    N'ValueTexts', N'ValueLongTexts', N'Views', N'ViewAttributes', N'ViewCaptions');
IF @sql <> N'' EXEC sys.sp_executesql @sql;
GO


-- -----------------------------------------------------------------------------
-- Page compression for the large tables. EAV keys repeat heavily, so tables
-- typically shrink by half or more: fewer pages read, more data held in memory.
-- Available in all editions from SQL Server 2016 SP1.
-- The first run rebuilds each table, which can take a while on a large
-- database; later runs skip tables that are already compressed.
-- -----------------------------------------------------------------------------
DECLARE @t sysname, @sql nvarchar(max);
DECLARE table_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM (VALUES
        (N'ValueNumbers'), (N'ValueTexts'), (N'ValueDates'), (N'ValueTimes'),
        (N'ValueLookups'), (N'ValueEntities'), (N'ValueLongTexts'), (N'Indexes')) v(name)
    WHERE OBJECT_ID(N'dbo.' + name) IS NOT NULL
      AND EXISTS (
          SELECT 1 FROM sys.partitions p
          WHERE p.object_id = OBJECT_ID(N'dbo.' + name) AND p.data_compression_desc <> N'PAGE');
OPEN table_cursor;
FETCH NEXT FROM table_cursor INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    -- rebuilds the clustered index (the table) and any nonclustered indexes
    SET @sql = N'ALTER INDEX ALL ON dbo.' + QUOTENAME(@t) + N' REBUILD WITH (DATA_COMPRESSION = PAGE);';
    EXEC sys.sp_executesql @sql;
    FETCH NEXT FROM table_cursor INTO @t;
END
CLOSE table_cursor;
DEALLOCATE table_cursor;
GO


-- -----------------------------------------------------------------------------
-- GetViewValues: values (as text) for one view, entity and page of rows.
--
-- Inline table-valued function (was multi-statement with table variables), so
-- SQL Server optimises it as part of the calling query with real row estimates.
-- A multi-statement function cannot be ALTERed into an inline one, so the old
-- version is dropped once (this loses any permissions granted on it); later runs
-- use CREATE OR ALTER, which keeps permissions.
--
-- Lookups paired with a number (qualifiers such as '<' on '<5') are returned
-- once, combined with the number, not a second time on their own.
-- -----------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID(N'dbo.GetViewValues'), N'IsInlineFunction') = 0
    DROP FUNCTION dbo.GetViewValues;
GO
CREATE OR ALTER FUNCTION dbo.GetViewValues
(
    @ViewId smallint,
    @EntityId int,
    @Page int
)
RETURNS TABLE
AS
RETURN
WITH rng AS (
    SELECT @Page * NRows + 1 AS StartRow, @Page * NRows + NRows AS EndRow
    FROM dbo.Views
    WHERE Id = @ViewId
),
atts AS (
    SELECT DISTINCT va.AttributeId AS Id, a.[Format]
    FROM dbo.ViewAttributes va
    INNER JOIN dbo.Attributes a ON a.Id = va.AttributeId
    WHERE va.ViewId = @ViewId
)
SELECT CAST(v.Seq AS int) AS Seq, v.AttributeId, v.Value
FROM rng
CROSS APPLY (
    SELECT vn.Seq, a.Id AS AttributeId,
        CAST(CASE WHEN l.Name IS NULL THEN CAST(vn.Value AS varchar(30))
                  ELSE l.Name + CAST(vn.Value AS varchar(30)) END AS nvarchar(300)) AS Value
    FROM atts a
    INNER JOIN dbo.ValueNumbers vn ON vn.EntityId = @EntityId AND vn.AttributeId = a.Id AND vn.Seq BETWEEN rng.StartRow AND rng.EndRow
    LEFT JOIN dbo.ValueLookups vl ON vl.EntityId = @EntityId AND vl.AttributeId = a.Id AND vl.Seq = vn.Seq
    LEFT JOIN dbo.Lookups l ON l.Id = vl.LookupId

    UNION ALL

    SELECT v.Seq, a.Id, CAST(v.Value AS nvarchar(300))
    FROM atts a
    INNER JOIN dbo.ValueTexts v ON v.EntityId = @EntityId AND v.AttributeId = a.Id AND v.Seq BETWEEN rng.StartRow AND rng.EndRow

    UNION ALL

    SELECT v.Seq, a.Id, CAST(l.Name AS nvarchar(300))
    FROM atts a
    INNER JOIN dbo.ValueLookups v ON v.EntityId = @EntityId AND v.AttributeId = a.Id AND v.Seq BETWEEN rng.StartRow AND rng.EndRow
    INNER JOIN dbo.Lookups l ON l.Id = v.LookupId
    WHERE NOT EXISTS (   -- already shown with its number above
        SELECT 1 FROM dbo.ValueNumbers n
        WHERE n.EntityId = @EntityId AND n.AttributeId = v.AttributeId AND n.Seq = v.Seq)

    UNION ALL

    -- ISO yyyy-mm-dd (style 23), the format HTML <input type="date"> uses
    SELECT v.Seq, a.Id, CAST(CONVERT(varchar(10), v.Value, 23) AS nvarchar(300))
    FROM atts a
    INNER JOIN dbo.ValueDates v ON v.EntityId = @EntityId AND v.AttributeId = a.Id AND v.Seq BETWEEN rng.StartRow AND rng.EndRow

    UNION ALL

    SELECT v.Seq, a.Id, CAST(N'Y' AS nvarchar(300))
    FROM atts a
    INNER JOIN dbo.ValueLongTexts v ON v.EntityId = @EntityId AND v.AttributeId = a.Id AND v.Seq BETWEEN rng.StartRow AND rng.EndRow

    UNION ALL

    -- hh:mm (style 108, first 5 characters), the format HTML <input type="time"> uses
    SELECT v.Seq, a.Id, CAST(CONVERT(varchar(5), v.Value, 108) AS nvarchar(300))
    FROM atts a
    INNER JOIN dbo.ValueTimes v ON v.EntityId = @EntityId AND v.AttributeId = a.Id AND v.Seq BETWEEN rng.StartRow AND rng.EndRow
) v;
GO


-- -----------------------------------------------------------------------------
-- GetEntityIdLine: values (as text) and x positions for an entity's ID line.
-- Inline table-valued function (was multi-statement); output unchanged.
-- For a qualified number the qualifier is shown at x and the number at x+1.
-- -----------------------------------------------------------------------------
IF OBJECTPROPERTY(OBJECT_ID(N'dbo.GetEntityIdLine'), N'IsInlineFunction') = 0
    DROP FUNCTION dbo.GetEntityIdLine;
GO
CREATE OR ALTER FUNCTION dbo.GetEntityIdLine
(
    @EntityId int
)
RETURNS TABLE
AS
RETURN
WITH atts AS (
    SELECT DISTINCT v.AttributeId, CAST(v.X AS tinyint) AS x, a.[Format]
    FROM dbo.Entities e
    INNER JOIN dbo.EntityTypes et ON et.Id = e.EntityTypeId
    INNER JOIN dbo.ViewAttributes v ON v.ViewId = et.IdLineViewId
    INNER JOIN dbo.Attributes a ON a.Id = v.AttributeId
    WHERE e.Id = @EntityId
)
SELECT CAST(vn.Value AS varchar(255)) AS Value,
       CAST(CASE WHEN vl.EntityId IS NULL THEN a.x ELSE a.x + 1 END AS tinyint) AS x
FROM atts a
INNER JOIN dbo.ValueNumbers vn ON vn.EntityId = @EntityId AND vn.AttributeId = a.AttributeId AND vn.Seq = 1
LEFT JOIN dbo.ValueLookups vl ON vl.EntityId = @EntityId AND vl.AttributeId = a.AttributeId AND vl.Seq = 1

UNION ALL

SELECT CAST(v.Value AS varchar(255)), a.x
FROM atts a
INNER JOIN dbo.ValueTexts v ON v.EntityId = @EntityId AND v.AttributeId = a.AttributeId AND v.Seq = 1

UNION ALL

SELECT CAST(l.Name AS varchar(255)), a.x
FROM atts a
INNER JOIN dbo.ValueLookups v ON v.EntityId = @EntityId AND v.AttributeId = a.AttributeId AND v.Seq = 1
INNER JOIN dbo.Lookups l ON l.Id = v.LookupId

UNION ALL

SELECT CAST(FORMAT(v.Value, a.[Format]) AS varchar(255)), a.x
FROM atts a
INNER JOIN dbo.ValueDates v ON v.EntityId = @EntityId AND v.AttributeId = a.AttributeId AND v.Seq = 1

UNION ALL

SELECT CAST(FORMAT(v.Value, a.[Format]) AS varchar(255)), a.x
FROM atts a
INNER JOIN dbo.ValueTimes v ON v.EntityId = @EntityId AND v.AttributeId = a.AttributeId AND v.Seq = 1;
GO


-- -----------------------------------------------------------------------------
-- AttributeUse: how many entities have a value for an attribute, the latest
-- date in its table, and the average number of rows per entity.
--   * table name from DataTypes is quoted (QUOTENAME) before use in dynamic SQL
--   * COUNT(*) instead of COUNT(DISTINCT): Seq = 1 is unique per entity
--   * no divide-by-zero error when the attribute has no values
-- Note: each call still scans the value table, as the clustered key starts
-- with EntityId, not AttributeId.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.AttributeUse
(
    @AttributeId int
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @tablename sysname, @dateAttributeId int, @maxDate datetime, @nInstances int, @count int;

    SELECT @tablename = t.ValueTable, @dateAttributeId = ta.DateAttributeId
    FROM dbo.Attributes a
    INNER JOIN dbo.DataTypes t ON t.Id = a.DataTypeId
    INNER JOIN dbo.Tables ta ON ta.Id = a.TableId
    WHERE a.Id = @AttributeId;

    IF @tablename IS NOT NULL
    BEGIN
        DECLARE @sql nvarchar(max) = N'
            SELECT @count = COUNT(*)
            FROM dbo.' + QUOTENAME(@tablename) + N' AS v
            WHERE v.AttributeId = @Id AND v.Seq = 1';
        EXEC sys.sp_executesql @sql, N'@count int OUTPUT, @Id int', @count OUTPUT, @Id = @AttributeId;
    END

    IF @dateAttributeId > 0
    BEGIN
        SELECT @maxDate = MAX(Value), @nInstances = COUNT(*)
        FROM dbo.ValueDates
        WHERE AttributeId = @dateAttributeId;
    END

    SELECT @count AS nEntities, @maxDate AS latest,
        CASE WHEN @dateAttributeId > 0 THEN @nInstances / NULLIF(@count, 0) ELSE 1 END AS PerEntity;
END
GO
