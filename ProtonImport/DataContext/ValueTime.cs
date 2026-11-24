using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.DataContext
{
    public partial class ValueTime(int entityId, short attributeId, short seq) : ValueBase(entityId, attributeId, seq)
    {
        [Column(TypeName = "time(0)")]
        public TimeOnly Value { get; set; }
    }
}
