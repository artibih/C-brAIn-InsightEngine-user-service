using UsersService.Enums;

namespace UsersService.Models.Responses.Reports
{
    public class ProblemReportResponse
    {
        public int ReporterId { get; set; }
        public int OrganizationId { get; set; }
        public string Title { get; set; }
        public IssueCategory Category { get; set; }
        public string Description { get; set; }
        public string StepsToReproduce { get; set; }
        public string JiraIssueKey { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}
