using System.ComponentModel.DataAnnotations;
using UsersService.Enums;

namespace UsersService.Models.Requests.Reports
{
    public class ProblemReportRequest
    {
        [Required(ErrorMessage = "The Title field is required.")]
        public required string Title { get; set; }

        [Required(ErrorMessage = "The Category field is required.")]
        public required IssueCategory Category { get; set; }

        [Required(ErrorMessage = "The Description field is required.")]
        public required string Description { get; set; }

        [Required(ErrorMessage = "The Steps to Reproduce field is required.")]
        public required string StepsToReproduce { get; set; }
        public DateTime? OccurredAt { get; set; }
    }
}
