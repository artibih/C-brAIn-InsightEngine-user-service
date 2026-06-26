namespace UsersService.Models.Requests.Auth
{
    public class ConfirmAndLoginRequest
    {
        public string Email { get; set; }
        public string Token { get; set; }
        public string Password { get; set; }
    }
}
