using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ProtonConsole2.DataContext;

public partial class Entity
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    public short EntityTypeId { get; set; }

    [Column(TypeName = "varchar(255)")]
    public required string Name { get; set; } = string.Empty;

    public DateTime LastUpdated { get; set; }

    public virtual EntityType? EntityType { get; set; }

}
