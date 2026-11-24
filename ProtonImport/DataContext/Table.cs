using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ProtonConsole2.DataContext;

public partial class Table
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public short Id { get; set; } 

    public short EntityTypeId { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string Name { get; set; } = string.Empty!;

    public short DateAttributeId { get; set; }

    public virtual  EntityType? EntityType { get; set; }

}
