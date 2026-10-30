using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Transactions;

namespace Photon;

public class ProtonRepository
{
    private readonly string _connectionString;
    public ProtonRepository(string connectionString)
    {
        _connectionString = connectionString;
        SqlMapper.AddTypeHandler(new SqlDateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new SqlTimeOnlyTypeHandler());
    }
    public async Task<UserStarter> TryLogin(string pwd)
    {
        var encrypted = Decrypt(pwd);
        var userStarter = await GetSingleAsync<UserStarter>("SELECT top 1 * FROM UserStarters WHERE UserCode COLLATE Latin1_General_CS_AS = @userCode COLLATE Latin1_General_CS_AS",
            new { userCode = encrypted });

        if (userStarter != null)
        {
            userStarter.Menu = await GetMenu(userStarter.Parameter);
        }
        return userStarter!;
    }

    public async Task<IEnumerable<ViewText>> GetViewCaptions(int viewId)
    {

        return await GetResultAsync<ViewText>(
            "SELECT Caption text, x,y FROM ViewCaptions WHERE ViewId=@viewId",
            new { ViewId = viewId }
        );
    }

    public async Task<IEnumerable<ViewValue>> GetViewValues(int viewId, int entityId, int page = 0)
    {
        return await GetResultAsync<ViewValue>(
            "SELECT attributeId, Seq, Value Text  FROM GetViewValues(@viewId, @entityId, @page)",
            new { ViewId = viewId, EntityId = entityId, Page = page }
        );
    }

    public async Task<IEnumerable<IdValue>> GetIdValues(int entityId)
    {
        return await GetResultAsync<IdValue>(
            "SELECT Value text, x FROM GetEntityIdLine(@entityId)",
            new {EntityId = entityId }
        );
    }
    public async Task<string> GetLongText(int entityId, int attributeId, int seq)
    {
        using var connection = new SqlConnection(_connectionString);

        var result = await connection.ExecuteScalarAsync<string>("SELECT Value FROM ValueLongTexts WHERE EntityId=@EntityId AND AttributeId=@AttributeId AND Seq=@Seq",
                new { EntityId = entityId, AttributeId = attributeId, Seq = seq });

        if (result is null)
        {
            return "String not found.";
        }

        return result;
    }
    public async Task<AttributeConfig> GetAttributeConfig(int attributeId)
    {
        using var cn = new SqlConnection(_connectionString);

        using var multi = await cn.QueryMultipleAsync(@"
SELECT a.Id,  a.Name, a.Comment, t.Name [DataType] , [Max], [Min], a.LookupTypeId, lt.Name LookupType, a.Quark, a.DateAttributeId
FROM Attributes a 
LEFT JOIN DataTypes t on t.Id=a.DataTypeId
LEFT JOIN LookupTypes lt on lt.Id=a.LookupTypeId
WHERE a.Id=@AttributeId

SELECT top 20 l.Id, l.Name 
FROM Attributes a
INNER JOIN Lookups l on (a.LookupTypeId>0 AND l.LookupTypeId=a.LookupTypeId) OR (a.lookupTypeId=-1 AND l.id>=a.Min AND l.id <=a.max)
WHERE a.Id=@attributeId",
        new { AttributeId = attributeId });

        AttributeConfig attributeConfig = await multi.ReadFirstAsync<AttributeConfig>();
        attributeConfig.Lookups = [.. await multi.ReadAsync<IndexType>()];
        return attributeConfig;
    }
    public async Task<IEnumerable<IndexType>> GetFilteredLookups(int attributeId, string? searchTerm, int nRows)
    {
        return await GetResultAsync<IndexType>(@"
SELECT top(@NRows) l.id, l.Name
FROM Attributes a
INNER JOIN Lookups l on (a.LookupTypeId>0 AND l.LookupTypeId=a.LookupTypeId) OR (a.lookupTypeId=-1 AND l.id>=a.Min AND l.id <=a.max)
WHERE a.Id=@AttributeId
AND l.Name like @SearchTerm + '%'
ORDER BY l.Name",
        new { AttributeId = attributeId, SearchTerm = searchTerm, NRows = nRows });
    }
    public async Task<IEnumerable<IndexType>> GetLookups(int attributeId, int page, int nRows)
    {
        return await GetResultAsync<IndexType>(@"
SELECT l.Id, l.Name 
FROM Attributes a
INNER JOIN Lookups l on (a.LookupTypeId>0 AND l.LookupTypeId=a.LookupTypeId) OR (a.lookupTypeId=-1 AND l.id>=a.Min AND l.id <=a.max)
WHERE a.Id=@AttributeId
ORDER BY l.Name
OFFSET @StartRow ROWS 
FETCH NEXT @NRows ROWS ONLY",
       new { AttributeId = attributeId, StartRow = page * nRows, NRows = nRows });
    }

    public async Task<IEnumerable<ViewAttribute>> GetViewAttributes(int viewId)
    {
        return await GetResultAsync<ViewAttribute>(@"
SELECT AttributeId, a.Name + CASE WHEN a.Comment IS NULL THEN '' ELSE ' ' + a.Comment END [Name], v.X, v.Y, a.DataTypeId, a.DisplayLength
FROM ViewAttributes v 
INNER JOIN Attributes a ON a.Id=v.AttributeId
WHERE v.ViewId = @ViewId",
        new { ViewId = viewId });
    }

    public async Task<IEnumerable<IndexType>> GetIndexTypes(int entityTypeId)
    {
        return await GetResultAsync<IndexType>(
            "SELECT Id, Name FROM IndexTypes WHERE EntityTypeId=@EntityTypeId",
            new { EntityTypeId = entityTypeId }
        );
    }

    public async Task<IEnumerable<EntityType>> GetEntityTypes()
    {
        var ets = await GetResultAsync<EntityType>(
            "SELECT Id, Name, DefaultIndexTypeId, idlineViewId FROM EntityTypes",
            new { }
        );

        foreach (EntityType et in ets)
        {
            et.IndexTypes = await GetIndexTypes(et.Id);
            et.Views = await GetViews(et.Id);
        }

        return ets;
    }

    public async Task<IEnumerable<View>> GetViews(int entityTypeId)
    {
        var vs = await GetResultAsync<View>(@"
SELECT v.Id, v.Name, v.nRows, t.DateAttributeId
FROM Views v
INNER JOIN Tables t ON t.Id = v.TableId
WHERE t.EntityTypeId = @entityTypeId
AND NOT v.Name = ''
ORDER BY v.Name",
        new { EntityTypeId = entityTypeId });

        foreach (View v in vs)
        {
            v.Captions = await GetViewCaptions(v.Id);
            v.ViewAttributes = await GetViewAttributes(v.Id);
        }

        return vs;
    }

    public async Task<IEnumerable<Index>> GetIndexes(int indexTypeId = 1, int page = 0, int nRows = 16, string searchTerm = "")
    {

        if (int.TryParse(searchTerm.Trim(), out int eid))
        {
            return await GetResultAsync<Index>(@"
SELECT Term, EntityId 
FROM Indexes 
WHERE IndexTypeId=@IndexTypeId 
AND EntityId = @entityId",
            new { IndexTypeId = indexTypeId, entityId = eid });
        }
        else
        {
            return await GetResultAsync<Index>(@"
SELECT Term, EntityId 
FROM Indexes 
WHERE IndexTypeId=@IndexTypeId 
AND Term LIKE @term + '%'
ORDER BY Term
OFFSET @startRow ROWS 
FETCH NEXT @Nrows ROWS ONLY",
            new { IndexTypeId = indexTypeId, term = searchTerm, StartRow = page * nRows, Nrows = nRows });
        }
    }

    public async Task<IEnumerable<DatedValue>> GetDatedValues(int entityId, short attributeId)
    {
        return await GetResultAsync<DatedValue>(@"
SELECT d.Value [date], n.Value 
FROM ValueNumbers n
INNER JOIN Attributes a on a.Id=n.AttributeId
INNER JOIN Tables t ON t.Id=a.TableId
INNER JOIN ValueDates d on d.EntityId=n.EntityId and d.AttributeId=t.DateAttributeId and d.seq=n.seq
WHERE n.EntityId = @EntityId
AND n.AttributeId = @AttributeId
ORDER BY d.Value",
        new { EntityId = entityId, AttributeId = attributeId });

    }

    public async Task<Menu> GetMenu(int menuId)
    {

        using var cn = new SqlConnection(_connectionString);

        using var multi = await cn.QueryMultipleAsync(@"
SELECT Id, Name FROM Menus WHERE Id=@Id
SELECT Seq, Name, Priv1, [Function], Parameter1, NextMenuId, StartMenuId
FROM MenuItems 
WHERE MenuId=@Id
AND Seq < 9
AND LEN([Name]) > 0
AND (
[Function] IN('SCRN', 'CHGE', '1', '2', '3', '5', '6') 
OR NextMenuId <> 0 
OR StartMenuId <> 0)",
        new { Id = menuId, MenuId = menuId });

        Menu menu = await multi.ReadFirstAsync<Menu>();
        menu.MenuItems = [.. await multi.ReadAsync<MenuItem>()];
        return menu;
    }

    public async Task<AttributeUse?> GetAttributeUse(int attributeId)
    {
        using var cn = new SqlConnection(_connectionString);

        var parameters = new DynamicParameters();
        parameters.Add("@AttributeId", attributeId);

        var res = await cn.QuerySingleOrDefaultAsync<AttributeUse>("AttributeUse", parameters, commandType: CommandType.StoredProcedure);
        return res;
    }
    public async Task<int> NPages(short viewId, int entityId)
    {
        return await GetSingleAsync<int>(@"
SELECT MAX(CEILING(CAST(d.seq AS FLOAT)/v.NRows)) AS NRows
FROM Views v
INNER JOIN Tables t ON t.Id=v.TableId
INNER JOIN ValueDates d ON d.EntityId=@EntityId AND d.AttributeId=t.DateAttributeId
WHERE v.id=@ViewId
GROUP BY v.NRows",
      new { EntityId = entityId, ViewId = viewId });
    }

    private async Task<IEnumerable<T>> GetResultAsync<T>(string sql, object parameters)
    {

        using var cn = new SqlConnection(_connectionString);
        try
        {
            return await cn.QueryAsync<T>(sql, parameters);
        }
        catch (Exception ex)
        {
            throw (new Exception(ex.Message));
        }
    }

    private async Task<T> GetSingleAsync<T>(string sql, object parameters)
    {

        using var cn = new SqlConnection(_connectionString);
        try
        {
            return await cn.QueryFirstOrDefaultAsync<T>(sql, parameters);
        }
        catch (Exception ex)
        {
            throw (new Exception(ex.Message));
        }
    }

    public async Task<IResult> ProcessUpdates(BatchRequestDto request)
    {
        using var cn = new SqlConnection(_connectionString);

        try
        {
            cn.Open();

            await cn.ExecuteAsync(@" 
            IF OBJECT_ID('BatchStaging') IS NOT NULL
                DROP TABLE BatchStaging
            ");

            await cn.ExecuteAsync(@"
            CREATE TABLE BatchStaging (
                Action VARCHAR(10),
                EntityId INT,
                AttributeId smallint,
                Seq INT,
                Text VARCHAR(255)
            );");

            var stagingRecords = new List<object>();

            stagingRecords.AddRange(request.Add.Select(c => new { Action = "CREATE", c.EntityId, c.AttributeId, c.Seq, c.Text }));
            stagingRecords.AddRange(request.Update.Select(u => new { Action = "UPDATE", u.EntityId, u.AttributeId, u.Seq, u.Text }));
            stagingRecords.AddRange(request.Delete.Select(id => new { Action = "DELETE", EntityId = id, AttributeId = 0, Seq = 0, Text = "" }));

            // 4. Bulk insert the instructions into the temp table
            const string insertStagingSql = @"
            INSERT INTO BatchStaging (Action, EntityId, AttributeId, Seq, Text) 
            VALUES (@Action, @EntityId, @AttributeId, @Seq, @Text);";

            await cn.ExecuteAsync(insertStagingSql, stagingRecords);


        }
        catch (SqlException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Database execution failed.");
        }

        return Results.Ok(new { message = "Batch processing completed successfully." });
    }

    static string Decrypt(string input)
    {
        var chars = input.ToCharArray();
        byte[] bytes = new byte[chars.Length];
        for (int i = 0; i < chars.Length; i++)
        {
            bytes[i] = Encoding.ASCII.GetBytes(chars[i].ToString())[0];
            //decrypt using XOR 31 (c# ^ 31) 
            bytes[i] ^= 31;
        }
        return Encoding.ASCII.GetString(bytes);
    }
}

public class Entity
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public class EntityType
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int DefaultIndexTypeId { get; set; }
    public int IdlineViewId { get; set; }
    public IEnumerable<View> Views { get; set; } = [];
    public IEnumerable<IndexType> IndexTypes { get; set; } = [];
}

public class ViewText
{
    public required string Text { get; set; }
    public byte X { get; set; }
    public byte Y { get; set; }
}

public class IdValue
{
    public required string Text { get; set; }
    public byte X { get; set; }
    public byte Y { get; set; }
    public short DataTypeId { get; set; }
    public short AttributeId { get; set; }
}

public class ViewValue
{
    public short AttributeId { get; set; }
    public int Seq { get; set; }
    public required string Text { get; set; }
}

public record ValueDto(int EntityId, Int16 AttributeId, int Seq, string Text);

public record BatchRequestDto(List<ValueDto> Add, List<ValueDto> Delete, List<ValueDto> Update);




public class AttributeConfig
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Comment { get; set; }
    public required string DataType { get; set; }
    public string? LookupType { get; set; }
    public int? LookupTypeId { get; set; }
    public float? Max { get; set; }
    public float? Min { get; set; }
    public short? Quark { get; set; }
    public short? DateAttributeId { get; set; }
    public IEnumerable<IndexType> Lookups { get; set; } = [];
}

public class AttributeUse
{
    public int NEntities { get; set; }
    public DateOnly Latest { get; set; }
    public int PerEntity { get; set; }
}

public class Index
{
    public required string Term { get; set; }
    public int EntityId { get; set; }
}

public class IndexType
{
    public required string Name { get; set; }
    public int Id { get; set; }
}

public class View
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public byte NRows { get; set; }
    public short? DateAttributeId { get; set; }
    public IEnumerable<ViewText> Captions { get; set; } = [];
    public IEnumerable<ViewAttribute> ViewAttributes { get; set; } = [];
}

public class ViewAttribute
{
    public int AttributeId { get; set; }
    public byte DataTypeId { get; set; }
    public string? Name { get; set; }
    public byte DisplayLength { get; set; }
    public byte X { get; set; }
    public byte Y { get; set; }
}

public class DatedValue
{
    public DateOnly Date { get; set; }
    public float Value { get; set; }
}

public class UserStarter
{
    public int Priv { get; set; }
    public int FunctionId { get; set; }
    public int Parameter { get; set; }
    public int EntityTypeId { get; set; }
    public int IndexTypeId { get; set; }

    public int MenuId
    {
        get
        {
            if (FunctionId == 0) return Parameter;
            else return 0;
        }
    }
    public Menu? Menu { get; set; }
}

public class Menu
{
    public short Id { get; set; }
    public string? Name { get; set; }
    public List<MenuItem> MenuItems { get; set; } = [];
}

public class MenuItem
{
    public byte Seq { get; set; }
    public required string Name { get; set; }
    public byte Priv1 { get; set; }
    public byte Priv2 { get; set; }
    public string? Function { get; set; }
    public byte Flags { get; set; }
    public int? Parameter1 { get; set; }
    public int? Parameter2 { get; set; }
    public int? Parameter3 { get; set; }
    public int? Parameter4 { get; set; }
    public int? NextMenuId { get; set; }
    public int? StartMenuId { get; set; }
    public bool ForEdit
    {
        get
        {
            return (Flags & 32) == 32;
        }
    }
}

public class SqlDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly date)
        => parameter.Value = date.ToDateTime(new TimeOnly(0, 0));
    public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);
}

public class SqlTimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
{
    public override void SetValue(IDbDataParameter parameter, TimeOnly time)
    {
        parameter.Value = time.ToString();
    }

    public override TimeOnly Parse(object value) => TimeOnly.FromTimeSpan((TimeSpan)value);
}
