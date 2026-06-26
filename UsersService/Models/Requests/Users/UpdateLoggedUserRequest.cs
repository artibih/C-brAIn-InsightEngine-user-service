namespace UsersService.Models.Requests.Users
{
    public class UpdateLoggedUserRequest
    {
        public string? UserName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}
