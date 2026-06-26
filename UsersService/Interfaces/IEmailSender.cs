using UsersService.Models.DTOs;

namespace UsersService.Interfaces
{
    public interface IEmailSender
    {
        Task SendEmailAsync(Email email);
    }
}
