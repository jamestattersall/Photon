namespace ProtonImport.ProtonToSql
{
    internal static class Sql
    {
        const string stagingPrefix = "staging_";

        private static string EqualSet(string[] keyColumnNames)
        {
            List<string> res = [];
            foreach (string val in keyColumnNames)
            {
                res.Add($"s.{val} = t.{val}");
            }
            return string.Join(" AND ", res);
        }

        private static string UpdateSet(string[] nonKeyColumnNames)
        {
            List<string> res = [];
            foreach (string val in nonKeyColumnNames)
            {
                res.Add($"{val} = s.{val}");
            }
            return string.Join(", ", res);
        }

        /// <summary>
        /// NULL-safe "any column differs" test: INTERSECT treats NULLs as equal,
        /// so a change to or from NULL is detected.
        /// </summary>
        private static string NonEqualSet(string[] nonKeyColumnNames)
        {
            var t = string.Join(", ", nonKeyColumnNames.Select(c => $"t.{c}"));
            var s = string.Join(", ", nonKeyColumnNames.Select(c => $"s.{c}"));
            return $"NOT EXISTS (SELECT {t} INTERSECT SELECT {s})";
        }

        public static string StagingTableName(string tableName) => $@"{stagingPrefix}{tableName}";

        /// <summary>
        /// Holds the ids of every entity re-read from Proton in the current batch,
        /// including entities that now have no rows in this value table.
        /// </summary>
        public static string StagingEntityIdsTableName(string tableName) => $@"{stagingPrefix}ids_{tableName}";

        public static string SqlCreateStagingEntityIdsTable(string tableName)
        {
            var idsTableName = StagingEntityIdsTableName(tableName);

            return $@"
IF OBJECT_ID('{idsTableName}') IS NOT NULL
TRUNCATE TABLE [{idsTableName}]
ELSE
CREATE TABLE [{idsTableName}] (EntityId INT NOT NULL PRIMARY KEY)";
        }

        public static string SqlDeleteStagingEntityIdsTable(string tableName)
        {
            var idsTableName = StagingEntityIdsTableName(tableName);

            return $@"
IF OBJECT_ID('{idsTableName}') IS NOT NULL
DROP TABLE [{idsTableName}]";
        }

        /// <summary>
        /// Deletes rows for entities re-read in this batch that are no longer present in Proton.
        /// </summary>
        public static string SqlValuesDeleteFromStaging(string tableName, string[] keyColumnNames)
        {
            var stagingTableName = StagingTableName(tableName);
            var idsTableName = StagingEntityIdsTableName(tableName);
            var joinStr = EqualSet(keyColumnNames);

            return $@"
DELETE t
FROM [{tableName}] t
WHERE t.EntityId IN (SELECT EntityId FROM [{idsTableName}])
AND NOT EXISTS (SELECT 1 FROM [{stagingTableName}] s WHERE {joinStr})";
        }

        public static string SqlMetadataDeleteFromStaging(string tableName, string[] keyColumnNames)
        {
            var stagingTableName = StagingTableName(tableName);

            var joinStr = EqualSet(keyColumnNames);

            return $@"
DELETE {tableName}
FROM {tableName} t
LEFT JOIN {stagingTableName} s 
ON {joinStr}
WHERE s.{keyColumnNames[0]} is null";
        }


        public static string SqlCreateStagingTable(string tableName)
        {
            var stagingTableName = StagingTableName(tableName);

            return $@"
IF OBJECT_ID('{stagingTableName}') IS NOT NULL  
TRUNCATE TABLE [{stagingTableName}]
ELSE
SELECT TOP 0 * 
INTO [{stagingTableName}]
FROM [{tableName}]";
        }

        public static string SqlCreateStagingPrimaryKey(string tableName, string[] keyColumnNames)
        {
            var stagingTableName = StagingTableName(tableName);

            return $@"
IF OBJECT_ID(N'PK_staging_{tableName}')IS NULL
ALTER TABLE [{stagingTableName}] ADD CONSTRAINT PK_staging_{tableName} PRIMARY KEY CLUSTERED ({string.Join(", ", keyColumnNames)})";

        }

        public static string SqlUpsert(string tableName, string[] keyColumnNames, string[] nonKeyColumnNames)
        {
            var stagingTableName = StagingTableName(tableName);
            var joinStr = EqualSet(keyColumnNames);
            var strNonEqual = NonEqualSet(nonKeyColumnNames);
            var updateStr = UpdateSet(nonKeyColumnNames);

            var insert = $@"
INSERT INTO [{tableName}]
SELECT s.*
FROM [{stagingTableName}] s
LEFT JOIN [{tableName}] t ON {joinStr}
WHERE t.{keyColumnNames[0]} is null";

            // every column is part of the key, so there is nothing to update
            if (nonKeyColumnNames.Length == 0) return insert;

            return $@"
UPDATE [{tableName}]
SET {updateStr}
FROM [{tableName}] t
INNER JOIN [{stagingTableName}] s ON {joinStr}
WHERE {strNonEqual}
{insert}";

        }

        public static string SqlDeleteStagingTable(string tableName)
        {
            var stagingTableName = StagingTableName(tableName);

            return $@"
IF OBJECT_ID('{stagingTableName}') IS NOT NULL  
DROP TABLE [{stagingTableName}]";
        }

        public static string SqlTruncateStagingTable(string tableName)
        {
            var stagingTableName = StagingTableName(tableName);

            return $@"
TRUNCATE TABLE [{stagingTableName}]
";
        }

        public static string SqlTruncateTable(string tablename)
        {
            return $@"
TRUNCATE TABLE [{tablename}]
";
        }

        public static string NoCheckConstraint()
        {
            return $"EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'";
        }

    }
}
