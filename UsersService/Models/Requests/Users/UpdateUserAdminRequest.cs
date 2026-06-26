using System.ComponentModel.DataAnnotations;

namespace UsersService.Models.Requests.Users
{
    public class UpdateUserAdminRequest
    {
      
        public int? RoleId { get; set; }

        public string? NewPassword { get; set; }
    }
}
