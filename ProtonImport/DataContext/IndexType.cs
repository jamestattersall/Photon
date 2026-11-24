using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace ProtonConsole2.DataContext;

public partial class IndexType
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public short Id { get; set; }

    public short EntityTypeId { get; set; }

    [Column(TypeName = "varchar(255)")]
    public required string Name { get; set; }

    public string? Prefix { get; set; } = null;

    public short IdLineViewId { get; set; }

    public virtual EntityType? EntityType {get; set;}

}
