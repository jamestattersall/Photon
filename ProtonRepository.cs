using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Win32.SafeHandles;
using System;
using System.Data;
using System.Formats.Asn1;
using System.Net;
using System.Security.Principal;
using System.Text;

public class ProtonRepository
{
    private readonly string _connectionString ;
    public ProtonRepository(string connectionString)
    {
        _connectionString = connectionString;
        SqlMapper.AddTypeHandler(new SqlDateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new SqlTimeOnlyTypeHandler());
    }


    public UserStarter? TryLogin(string pwd)
    {
        var encrypted = Decrypt(pwd);
        var userStarter = GetResult<UserStarter>("SELECT * FROM UserStarters WHERE UserCode COLLATE Latin1_General_CS_AS = @userCode COLLATE Latin1_General_CS_AS",
            new { userCode = encrypted }).FirstOrDefault();
        if(userStarter != null) userStarter.Menu = GetMenu(userStarter.MenuId);

        return userStarter;
    }

    public IEnumerable<ViewText> GetViewCaptions(int viewId)
    {

        return GetResult<ViewText>(
            "SELECT Caption text, x,y FROM ViewCaptions WHERE ViewId=@viewId",
            new { ViewId = viewId }
        );
    }

    public IEnumerable<ViewValue> GetViewValues(int viewId, int entityId, int page = 0)
    {
        return GetResult<ViewValue>(
            "SELECT Value text, x, y, dataTypeId, attributeId FROM GetViewValues(@viewId, @entityId, @page)",
            new { ViewId = viewId, EntityId = entityId, Page = page }
        );
    }
    public AttributeConfig GetAttributeConfig(int attributeId)
    {
        using var cn = new SqlConnection(_connectionString);

        using var multi = cn.QueryMultiple(@$"
SELECT a.Id,  a.Name, a.Comment, t.Name [DataType] , [Max], [Min], a.LookupTypeId, lt.Name LookupType, a.Quark
FROM Attributes a 
LEFT JOIN DataTypes t on t.Id=a.DataTypeId
LEFT JOIN LookupTypes lt on lt.Id=a.LookupTypeId
WHERE a.Id=@AttributeId

SELECT top 20 l.Id, l.Name 
FROM Attributes a
INNER JOIN Lookups l on (a.LookupTypeId>0 AND l.LookupTypeId=a.LookupTypeId) OR (a.lookupTypeId=-1 AND l.id>=a.Min AND l.id <=a.max)
WHERE a.Id=@attributeId
", new { AttributeId = attributeId });

        AttributeConfig attributeConfig = multi.ReadFirst<AttributeConfig>();
        attributeConfig.Lookups = multi.Read<IndexType>().ToList();
        return attributeConfig;

    }

    public IEnumerable<IndexType> GetLookups(int attributeId, int page, int nRows)
    {
        return GetResult<IndexType>(
             @"
SELECT l.Id, l.Name 
FROM Attributes a
INNER JOIN Lookups l on (a.LookupTypeId>0 AND l.LookupTypeId=a.LookupTypeId) OR (a.lookupTypeId=-1 AND l.id>=a.Min AND l.id <=a.max)
WHERE a.Id=@AttributeId
ORDER BY l.Name
OFFSET @StartRow ROWS 
FETCH NEXT @NRows ROWS ONLY
",
        new { AttributeId = attributeId, StartRow = page * nRows, NRows = nRows });
    }

    public IEnumerable<ViewAttribute> GetViewAttributes(int viewId)
    {
        return GetResult<ViewAttribute>(
            @"
SELECT AttributeId, a.Name + CASE WHEN a.Comment IS NULL THEN '' ELSE ' ' + a.Comment END [Name], v.X, v.Y, a.DataTypeId, a.DisplayLength
FROM ViewAttributes v 
INNER JOIN Attributes a ON a.Id=v.AttributeId
WHERE v.ViewId = @ViewId",
            new { ViewId = viewId }
        );
    }

    public IEnumerable<IndexType> GetIndexTypes(int entityTypeId)
    {
        return GetResult<IndexType>(
            "SELECT Id, Name FROM IndexTypes WHERE EntityTypeId=@EntityTypeId",
            new { EntityTypeId = entityTypeId }
        );
    }

    public IEnumerable<EntityType> GetEntityTypes()
    {
        var ets = GetResult<EntityType>(
            "SELECT Id, Name, DefaultIndexTypeId, idlineViewId FROM EntityTypes",
            new { }
        );

        foreach (EntityType et in ets)
        {
            et.IndexTypes = GetIndexTypes(et.Id);
            et.Views = GetViews(et.Id);
        }

        return ets;
    }

    public IEnumerable<View> GetViews(int entityTypeId)
    {
        var vs = GetResult<View>(@"
SELECT v.Id, v.Name, v.nRows, case when t.DateAttributeId > 0 then 1 else 0 end as isDated
FROM Views v
INNER JOIN Tables t ON t.Id = v.TableId
WHERE t.EntityTypeId = @entityTypeId
AND NOT v.Name = ''
ORDER BY v.Name",
            new { EntityTypeId = entityTypeId }
        );

        foreach (View v in vs)
        {
            v.Captions = GetViewCaptions(v.Id);
            v.ViewAttributes = GetViewAttributes(v.Id);
        }

        return vs;
    }

    public IEnumerable<Index> GetIndexes(int indexTypeId, int page = 0, int nRows = 16, string? searchterm = "")
    {
        return GetResult<Index>(
            @"
SELECT Term, EntityId 
FROM Indexes 
WHERE IndexTypeId=@IndexTypeId 
    AND Term LIKE @term + '%'
ORDER BY Term
OFFSET @startRow ROWS 
FETCH NEXT @Nrows ROWS ONLY",
            new { IndexTypeId = indexTypeId, term = searchterm, StartRow = page * nRows, Nrows = nRows }
        );
    }

    public IEnumerable<DatedValue> GetDatedValues(int entityId, short attributeId, long daysBack)
    {
        return GetResult<DatedValue>(@"
SELECT d.Value [date], n.Value 
FROM ValueNumbers n
INNER JOIN Attributes a on a.Id=n.AttributeId
INNER JOIN Tables t ON t.Id=a.TableId
INNER JOIN ValueDates d on d.EntityId=n.EntityId and d.AttributeId=t.DateAttributeId and d.seq=n.seq
WHERE n.EntityId = @EntityId
AND n.AttributeId = @AttributeId
AND ((@DaysBack = 0) OR (d.Value > dateAdd(dd,-@DaysBack,getdate())))
ORDER BY d.Value 
", new { EntityId = entityId, AttributeId = attributeId, DaysBack = daysBack });

    }

    public Menu GetMenu(int menuId) {

        using var cn = new SqlConnection(_connectionString);

        using var multi = cn.QueryMultiple($@"
SELECT Id, Name FROM Menus WHERE Id=@Id
SELECT Seq, Name, [Function], Parameter1, NextMenuId, StartMenuId
FROM MenuItems 
WHERE MenuId=@Id
AND Seq < 9
AND LEN([Name]) > 0
AND (
        [Function] IN('SCRN', 'CHGE', '1', '2', '3') 
        OR NextMenuId <> 0 
        OR StartMenuId <> 0
    )
",
        new { Id = menuId, MenuId = menuId });

        Menu menu = multi.ReadFirst<Menu>();
        menu.MenuItems = multi.Read<MenuItem>().ToList();
        return menu;
    }

    public AttributeUse? GetAttributeUse(int attributeId)
    {
        using var cn = new SqlConnection(_connectionString);

        var parameters = new DynamicParameters();
        parameters.Add("@AttributeId", attributeId);

        var res=cn.QuerySingleOrDefault<AttributeUse>("AttributeUse", parameters, commandType: CommandType.StoredProcedure);
        return res;
        
    }

    private IEnumerable<T> GetResult<T>(string sql, object parameters) {

        IEnumerable<T> result = [];
        using var cn = new SqlConnection(_connectionString);
        try
        {
            return cn.Query<T>(sql, parameters);
        }
        catch(Exception ex)
        {
            throw ( new Exception(ex.Message));
        }
    }

    private IEnumerable<T> GetResultSp<T>(string spName, object parameters)
    {
        using var _db = new SqlConnection(_connectionString);
        _db.Open();
        return _db.Query<T>(spName, parameters, commandType: CommandType.StoredProcedure);
    }

    public int NPages( short viewId, int entityId)
    {
        int res = 0;

        using var cn = new SqlConnection(_connectionString);
    
        res = cn.ExecuteScalar<int>($@"
SELECT MAX(CEILING(CAST(d.seq AS FLOAT)/v.NRows)) AS NRows
FROM Views v
INNER JOIN Tables t ON t.Id=v.TableId
INNER JOIN ValueDates d ON d.EntityId={entityId} AND d.AttributeId=t.DateAttributeId
WHERE v.id={viewId}
GROUP BY v.NRows");
       

        return res;

    }

    string Decrypt(string input)
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
    public string Name { get; set; }
}

public class EntityType
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int DefaultIndexTypeId { get; set; }
    public int IdlineViewId { get; set; }
    public IEnumerable<View> Views { get; set; } = [];
    public IEnumerable<IndexType> IndexTypes { get; set; } = [];
}

public class ViewText
{
    public string Text { get; set; }
    public byte X { get; set; }
    public byte Y { get; set; }
}

public class ViewValue
{
    public string Text { get; set; }
    public byte X { get; set; }
    public byte Y { get; set; }
    public short DataTypeId { get; set; }
    public short AttributeId { get; set; }
}

public class AttributeConfig
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Comment { get; set; }
    public string? DataType { get; set; }
    public string? LookupType { get; set; }
    public float? Max { get; set; }
    public float? Min { get; set; }
    public short? Quark { get; set; }
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
    public string Term { get; set; }
    public int EntityId { get; set; }
}

public class IndexType
{
    public string Name { get; set; }
    public int id { get; set; }
}

public class View
{
    public int Id { get; set; }
    public string Name { get; set; }
    public byte nRows { get; set; }
    public bool isDated { get; set; }
    public IEnumerable<ViewText> Captions { get; set; } = [];
    public IEnumerable<ViewAttribute> ViewAttributes { get; set; } = [];
}

public class ViewAttribute
{
    public int AttributeId { get; set; }
    public byte DataTypeId { get; set; }
    public string Name { get; set; }
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
    public int MenuId { get; set; }
    public int entityTypeId { get; set; }
    public int indexTypeId { get; set; }
    public Menu? Menu { get; set; }
}

public class Menu
{
    public short Id { get; set; }
    public string Name { get; set; }
    public List<MenuItem> MenuItems { get; set; } = [];
}

public class MenuItem
{
    public byte Seq { get; set; }
    public string Name { get; set; }
    public string Function { get; set; }
    public int? Parameter1 { get; set; }
    public int? NextMenuId  { get; set; }
    public int? StartMenuId { get; set; }
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