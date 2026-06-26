using System.ComponentModel.DataAnnotations;

namespace UsersService.Models.Requests.Organizations
{
    public class CreateNotificationEmailsRequest
    {
        public int? OrganizationId { get; set; }
        [Required, MinLength(1)]
        public string[] NotificationEmails { get; set; } = Array.Empty<string>();
    }
}
