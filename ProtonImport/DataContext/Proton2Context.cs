using System.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace ProtonConsole2.DataContext;

public partial class Proton2Context : DbContext
{
    public Proton2Context() 
    {
        try
        {
             Database.EnsureCreated();
        }
        catch {
            Log.Warning("Unable to connect to database");
        
        }
    }

    public Proton2Context(DbContextOptions<Proton2Context> options)
        : base(options)
    {
       
    }

    public virtual DbSet<Attribute> Attributes { get; set; }

    public virtual DbSet<DataType> DataTypes { get; set; }

    public virtual DbSet<DataContext.Entity> Entities { get; set; }

    public virtual DbSet<EntityType> EntityTypes { get; set; }

    public virtual DbSet<Index> Indexes { get; set; }

    public virtual DbSet<IndexType> IndexTypes { get; set; }

    public virtual DbSet<Lookup> Lookups { get; set; }

    public virtual DbSet<LookupType> LookupTypes { get; set; }

    public virtual DbSet<Menu> Menus { get; set; }

    public virtual DbSet<MenuItem> MenuItems { get; set; }

    public virtual DbSet<Table> Tables { get; set; }

    public virtual DbSet<UserStarter> UserStarters { get; set; }

    public virtual DbSet<ValueDate> ValueDates { get; set; }

    public virtual DbSet<ValueTime> ValueTimes { get; set; }

    public virtual DbSet<ValueEntity> ValueEntities { get; set; }

    public virtual DbSet<ValueLookup> ValueLookups { get; set; }

    public virtual DbSet<ValueNumber> ValueNumbers { get; set; }

    public virtual DbSet<ValueText> ValueTexts { get; set; }

    public virtual DbSet<ValueLongText> ValueLongTexts { get; set; }

    public virtual DbSet<View> Views { get; set; }

    public virtual DbSet<ViewAttribute> ViewAttributes { get; set; }

    public virtual DbSet<ViewCaption> ViewCaptions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer(
                  Utilities.ConfigurationManager.AppSettings.SQLConnectionString(false)
            )
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }


        var valueDate = modelBuilder.Entity<ValueDate>();
        valueDate.Property(f => f.Value).HasColumnType(nameof(SqlDbType.Date));

        var valueTime = modelBuilder.Entity<ValueTime>();
        valueTime.Property(f => f.Value).HasColumnType(nameof(SqlDbType.Time));

        var entity = modelBuilder.Entity<Entity>();
        entity.Property(f => f.LastUpdated).HasColumnType(nameof(SqlDbType.SmallDateTime));

        OnModelCreatingPartial(modelBuilder);
    }


    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
