namespace UsersService.Models.Requests.Reports
{
    public class ReportAnonymousRequest
    {
        public string OrganizationId { get; set; }
        public string FeatureKey { get; set; }
        public long FeatureValue { get; set; }
    }
}
