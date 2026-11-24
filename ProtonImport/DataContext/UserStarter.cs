using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ProtonConsole2.DataContext;

public partial class UserStarter
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public short Id { get; set; }

    [Column(TypeName = "varchar(255)")]
    public required string UserCode { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string? UserName { get; set; }
    public short MenuId { get; set; }
    public short EntityTypeId { get; set; }
    public short IdLineViewId { get; set; }
    public short IndexTypeId { get; set; }
}
