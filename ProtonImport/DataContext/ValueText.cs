using Microsoft.EntityFrameworkCore;
using ProtonImport.DataContext;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProtonImport.DataContext;

[Index(nameof(Value))]
public partial class ValueText(int entityId, short attributeId, short seq) : ValueBase(entityId, attributeId, seq)
{
    [Column(TypeName = "varchar(80)")]
    public required string Value { get; set; } 
}