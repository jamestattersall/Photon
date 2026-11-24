using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProtonConsole2.DataContext;

public partial class EntityType 
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public short Id { get; set; } 

    public short KeyIndexTypeId { get; set; }

    public short DefaultIndexTypeId { get; set; }

    [Column(TypeName = "varchar(255)")]
    public required string Name { get; set; }

    public short IdLineViewId { get; set; }

    public short IdAttributeId { get; set; }

}
