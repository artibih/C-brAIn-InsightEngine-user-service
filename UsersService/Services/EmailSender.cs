using sib_api_v3_sdk.Api;
using sib_api_v3_sdk.Model;
using UsersService.Interfaces;
using UsersService.Models.DTOs;
using Task = System.Threading.Tasks.Task;

namespace UsersService.Services
{
    public class EmailSender(IConfiguration configuration, ILogger<EmailSender> logger) : IEmailSender
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<EmailSender> _logger = logger;

        public async Task SendEmailAsync(Email email)
        {
            var apiInstance = new TransactionalEmailsApi();

            var sender = new SendSmtpEmailSender(email.SenderName, email.SenderEmail);
            var to = new List<SendSmtpEmailTo>
            {
                new (email.ReceiverEmail, email.ReceiverName)
            };

            var smtp = new SendSmtpEmail(
                sender: sender,
                to: to,
                htmlContent: email.EmailBody,
                subject: email.Subject
            );

            try
            {
                await apiInstance.SendTransacEmailAsync(smtp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {Email}", email.ReceiverEmail);
            }
        }
    }
}
