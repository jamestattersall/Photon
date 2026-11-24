using Microsoft.EntityFrameworkCore;
using ProtonConsole2.DataContext;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProtonConsole2.DataContext;

[Index(nameof(Value))]
public partial class ValueText(int entityId, short attributeId, short seq) : ValueBase(entityId, attributeId, seq)
{
    [Column(TypeName = "varchar(80)")]
    public required string Value { get; set; } 
}