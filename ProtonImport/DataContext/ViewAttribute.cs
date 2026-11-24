using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ProtonConsole2.DataContext;

[PrimaryKey(nameof(ViewId), nameof(Seq))]
public partial class ViewAttribute
{
    public short ViewId { get; set; } 

    public short Seq { get; init; } 

    public short AttributeId { get; init; }

    public byte X { get; init; } 

    public byte Y { get; init; } 

    public virtual View? View { get; set; }
    public virtual Attribute? Attribute { get; set; }

}
