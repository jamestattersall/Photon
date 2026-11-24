using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.DataContext
{
    [PrimaryKey(nameof(MenuId), nameof(Seq))]
    public class MenuItem
    {
        public short MenuId { get; set; }
        public byte Seq { get; set; }

        [Column(TypeName = "varchar(255)")]
        public required string Name { get; set; }

        [Column(TypeName = "varchar(255)")]
        public string? Function { get; set; }

        public short NextMenuId { get; set; }
        public short StartMenuId { get; set; }
        public short Parameter1 { get; set; }
        public short Parameter2 { get; set; }
        public short Parameter3 { get; set; }
        public short Parameter4 { get; set; }

        public virtual Menu? Menu { get; set; }

    }
}
