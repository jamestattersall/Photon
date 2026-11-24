using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.DataContext
{
    public class DataType() 
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public short Id { get; set; }

        [Column(TypeName = "varchar(255)")]
        public required string Name { get; set; } 

        [Column(TypeName = "varchar(255)")]
        public required string ValueTable { get; set; }

        [Column(TypeName = "varchar(255)")]
        public string? LookupTable { get; set; }

        [Column(TypeName = "varchar(255)")]
        public string? AltValueTable { get; set; }

        [Column(TypeName = "varchar(255)")]
        public string? AltLookupTable { get; set; }
    }
}
