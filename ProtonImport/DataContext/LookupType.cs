using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.DataContext
{
    public partial class LookupType
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public short Id { get; set; }

        [Column(TypeName = "varchar(255)")]
        public required string Name { get; set; }

    }
}
