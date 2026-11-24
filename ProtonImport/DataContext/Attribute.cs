using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProtonConsole2.DataContext;

public class Attribute
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public short Id { get; set; }

    public short DataTypeId { get; set; }

    public short EntityTypeId { get; set; }

    public short? TableId { get; set; }

    [Column(TypeName = "varchar(255)")]
    public required string Name { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string? Comment { get; set; }

    public short? Quark { get; set; }

    public short? DisplayLength { get; set; }

    public string? Format { get; set; }
    public float? Max { get; set; }
    public float? Min { get; set; }
    public short? LookupTypeId { get; set; }
    public short? DateAttributeId { get; set; }

    public virtual  EntityType? EntityType { get; set; }

    public virtual  DataType? DataType { get; set; }

}

public enum DataTypes
{
    Text=1,
    numeric=2,
    Lookup=3,
    Date=4,
    EntityPtr= 5,
    LongText = 6,
    QualifiedNumbers = 7,
    Time = 8,
}




