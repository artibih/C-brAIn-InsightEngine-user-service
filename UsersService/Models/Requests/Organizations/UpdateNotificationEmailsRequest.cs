using System.ComponentModel.DataAnnotations;

namespace UsersService.Models.Requests.Organizations
{
    public class UpdateNotificationEmailsRequest
    {
        public int? OrganizationId { get; set; }
        [Required]
        public string[] NotificationEmails { get; set; } = Array.Empty<string>();
    }
}
