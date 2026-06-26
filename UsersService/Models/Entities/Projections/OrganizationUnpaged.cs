using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UsersService.Models.Entities.Projections
{

    public class OrganizationUnpaged
    {
        [Key]
        [Column("OrganizationId")]
        public int Id { get; set; }
        public string Name { get; set; }
    }
}