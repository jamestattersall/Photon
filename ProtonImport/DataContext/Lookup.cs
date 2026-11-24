using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ProtonConsole2.DataContext;

public partial class Lookup
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    public short LookupTypeId { get; set; }

    [Column(TypeName = "varchar(255)")]
    public required string Name { get; set; } 

    [Column(TypeName = "char(5)")]
    public string? Code { get; set; }

    public virtual  LookupType? LookupType { get; set; }

}
