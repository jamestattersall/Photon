using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProtonConsole2.DataContext;

[PrimaryKey(nameof(ViewId), nameof(Seq))]
public partial class ViewCaption
{
    public short ViewId { get; set; }

    public short Seq { get; init; }

    [Column(TypeName = "varchar(255)")]
    public string? Caption { get; init; } 

    public byte X { get; init; } 

    public byte Y { get; init; } 

    public virtual View? View { get; set; }

}
