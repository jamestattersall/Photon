using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.DataContext
{
    public partial class ValueLongText(int entityId, short attributeId, short seq) : ValueBase(entityId, attributeId, seq)
    {
        [Column(TypeName = "varchar(max)")]
        public required string Value { get; set; } 
    }
}
