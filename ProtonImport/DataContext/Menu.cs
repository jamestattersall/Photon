using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProtonImport.DataContext;

public partial class Menu
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public short Id { get; set; }

    [Column(TypeName = "varchar(255)")]
    public required string Name { get; set; }

    public short EntityTypeId { get; set; }   

    public virtual EntityType? EntityType { get; set; }

}
