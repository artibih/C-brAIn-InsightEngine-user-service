namespace UsersService.Models.Responses.Users
{
    public class UserResponse
    {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool IsUsingTemporaryPassword { get; set; }
        public UserOrganization? Organization { get; set; }
    }
}