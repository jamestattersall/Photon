using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ProtonConsole2.DataContext;

public partial class View
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public short Id { get; set; }

    [Column(TypeName = "varchar(255)")]
    public required string Name { get; set; } = "";

    public short NRows { get; set; }

    public short NItems { get; set; }

    public short TableId { get; set; }

    public short EntityTypeId { get; set; }

    public virtual EntityType? EntityType { get; set; }

 }
