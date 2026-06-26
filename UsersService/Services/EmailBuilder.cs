using UsersService.Interfaces;
using UsersService.Models.DTOs;

namespace UsersService.Services
{
    public class EmailBuilder(IConfiguration configuration) : IEmailBuilder
    {
        private readonly IConfiguration _configuration = configuration;

        public Email BuildConfirmationEmail(string emailAddress, string firstName, string confirmationLink)
        {
            var email = new Email();

            email.Subject = "Complete Your Registration";
            email.ReceiverEmail = emailAddress;
            email.ReceiverName = firstName;
            email.SenderEmail = _configuration["BrevoApi:SenderEmail"];
            email.SenderName = _configuration["BrevoApi:SenderName"];
            email.EmailBody = CreateConfirmationEmailBody(firstName, confirmationLink);

            return email;
        }

        public Email BuildPasswordResetEmail(string emailAddress, string firstName, string callbackUrl)
        {
            var email = new Email();

            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "logo.jpg");
            string emailTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "ForgotPasswordEmailTemplate.html");

            string emailTemplate = File.ReadAllText(emailTemplatePath);
            byte[] logoImageArray = File.ReadAllBytes(logoPath);
            string base64ImageRepresentation = Convert.ToBase64String(logoImageArray);

            emailTemplate = emailTemplate.Replace("{{callbackUrl}}", callbackUrl);
            emailTemplate = emailTemplate.Replace("{{firstName}}", firstName);
            emailTemplate = emailTemplate.Replace("{{Logo}}", base64ImageRepresentation);
            emailTemplate = emailTemplate.Replace("{{Link}}", "https://testic.com"); 

            email.Subject = "Reset Your Password";
            email.ReceiverEmail = emailAddress;
            email.ReceiverName = firstName;
            email.SenderEmail = _configuration["BrevoApi:SenderEmail"];
            email.SenderName = _configuration["BrevoApi:SenderName"];
            email.EmailBody = emailTemplate;

            return email;
        }

        public Email BuildChangedPasswordNotificationEmail(string emailAddress, string firstName)
        {
            var email = new Email();

            string emailTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "ChangedPasswordNotification.html");

            string emailTemplate = File.ReadAllText(emailTemplatePath);

            emailTemplate = emailTemplate.Replace("{{firstName}}", firstName);
            emailTemplate = emailTemplate.Replace("{{dateAndTime}}", DateTime.UtcNow.ToString());

            email.Subject = "Reset Your Password";
            email.ReceiverEmail = emailAddress;
            email.ReceiverName = firstName;
            email.SenderEmail = _configuration["BrevoApi:SenderEmail"];
            email.SenderName = _configuration["BrevoApi:SenderName"];
            email.EmailBody = emailTemplate;

            return email;
        }

        public Email BuildAccountApprovedEmail(string emailAddress, string userName, string loginUrl)
        {
            var email = new Email();

            string emailTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "AccountApprovedEmailTemplate.html");

            string emailTemplate = File.ReadAllText(emailTemplatePath);

            emailTemplate = emailTemplate.Replace("{{userName}}", System.Net.WebUtility.HtmlEncode(userName));
            emailTemplate = emailTemplate.Replace("{{loginUrl}}", System.Net.WebUtility.HtmlEncode(loginUrl));

            email.Subject = "Your C-BrAIn Platform account has been approved";
            email.ReceiverEmail = emailAddress;
            email.ReceiverName = userName;
            email.SenderEmail = _configuration["BrevoApi:SenderEmail"];
            email.SenderName = _configuration["BrevoApi:SenderName"];
            email.EmailBody = emailTemplate;

            return email;
        }

        private static string CreateConfirmationEmailBody(string firstName, string confirmationLink)
        {
            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "logo.jpg");
            string emailTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "ConfirmationEmailTemplate.html");

            string emailTemplate = File.ReadAllText(emailTemplatePath);
            byte[] logoImageArray = File.ReadAllBytes(logoPath);
            string base64ImageRepresentation = Convert.ToBase64String(logoImageArray);

            emailTemplate = emailTemplate.Replace("{{verificationLink}}", confirmationLink);
            emailTemplate = emailTemplate.Replace("{{firstName}}", firstName);
            emailTemplate = emailTemplate.Replace("{{Logo}}", base64ImageRepresentation);
            emailTemplate = emailTemplate.Replace("{{Link}}", "https://test.com"); //Replace with real values

            return emailTemplate;
        }

        public Email BuildTemporaryPasswordEmail(string emailAddress, string firstName, string temporaryPassword, string callbackUrl)
        {
            var email = new Email();

            string emailTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "TemporaryPasswordEmailNotification.html");

            string emailTemplate = File.ReadAllText(emailTemplatePath);

            emailTemplate = emailTemplate.Replace("{{firstName}}", firstName);
            emailTemplate = emailTemplate.Replace("{{temporaryPassword}}", temporaryPassword);
            emailTemplate = emailTemplate.Replace("{{loginUrl}}", callbackUrl);
            emailTemplate = emailTemplate.Replace("{{email}}", emailAddress);


            email.Subject = "Your Temporary Password";
            email.ReceiverEmail = emailAddress;
            email.ReceiverName = firstName;
            email.SenderEmail = _configuration["BrevoApi:SenderEmail"];
            email.SenderName = _configuration["BrevoApi:SenderName"];
            email.EmailBody = emailTemplate;

            return email;
        }

        public Email BuildTemporaryPasswordForOrganizationAdminEmail(string emailAddress, string firstName, string orgName, string temporaryPassword, string callbackUrl)
        {
            var email = new Email();

            string emailTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "TemporaryPasswordOrgAdminEmailNotification.html");

            string emailTemplate = File.ReadAllText(emailTemplatePath);

            emailTemplate = emailTemplate.Replace("{{firstName}}", firstName);
            emailTemplate = emailTemplate.Replace("{{temporaryPassword}}", temporaryPassword);
            emailTemplate = emailTemplate.Replace("{{loginUrl}}", callbackUrl);
            emailTemplate = emailTemplate.Replace("{{email}}", emailAddress);
            emailTemplate = emailTemplate.Replace("{{orgName}}", orgName);


            email.Subject = "Your Temporary Password";
            email.ReceiverEmail = emailAddress;
            email.ReceiverName = firstName;
            email.SenderEmail = _configuration["BrevoApi:SenderEmail"];
            email.SenderName = _configuration["BrevoApi:SenderName"];
            email.EmailBody = emailTemplate;

            return email;
        }

        public Email BuildSubscriptionAboutToExpireEmail(string orgName, string subscriptionPlan)
        {
            string receiverEmail = _configuration["EmailConfig:ArtiAdminEmail"];
            string receiverName = orgName;

            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "SubscriptionAboutToExpireTemplate.html");
            string emailTemplate = File.ReadAllText(templatePath);

            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "logo.jpg");
            byte[] logoImageArray = File.ReadAllBytes(logoPath);
            string base64ImageRepresentation = Convert.ToBase64String(logoImageArray);

            emailTemplate = emailTemplate.Replace("{{organizationName}}", orgName);
            emailTemplate = emailTemplate.Replace("{{subscriptionPlan}}", subscriptionPlan);
            emailTemplate = emailTemplate.Replace("{{Logo}}", base64ImageRepresentation);
            emailTemplate = emailTemplate.Replace("{{currentYear}}", DateTime.UtcNow.Year.ToString());

            var email = new Email
            {
                Subject = "Subscription is About to Expire",
                ReceiverEmail = receiverEmail,
                ReceiverName = receiverName,
                SenderEmail = _configuration["BrevoApi:SenderEmail"],
                SenderName = _configuration["BrevoApi:SenderName"],
                EmailBody = emailTemplate
            };
            return email;
        }
        public Email BuildTokenAlertEmail(
            string receiverEmail,
            string receiverName,
            string orgName,
            Guid chatbotId,
            long used,
            long limit,
            string monthKey,
            long[] thresholdsHit)
        {
            var subject = $"Token alert: {used:N0}/{limit:N0} used in {monthKey}";
            var thresholdsText = (thresholdsHit?.Length ?? 0) == 0
                ? "—"
                : string.Join(", ", thresholdsHit.Select(h => $"{h:N0} remaining"));

            var html = $@"
        <div style=""font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:1.5"">
            <h2 style=""margin:0 0 12px 0"">{System.Net.WebUtility.HtmlEncode(orgName)} — Token Usage Alert</h2>
            <p><strong>Chatbot:</strong> {System.Net.WebUtility.HtmlEncode(chatbotId.ToString())}</p>
            <p><strong>Month:</strong> {System.Net.WebUtility.HtmlEncode(monthKey)}</p>
            <p><strong>Used:</strong> {used:N0} / {limit:N0} tokens ({(limit > 0 ? (int)Math.Round(100.0 * used / limit) : 0)}%)</p>
            <p><strong>Thresholds crossed (remaining tokens):</strong> {thresholdsText}</p>
            <hr />
            <p style=""color:#666;margin-top:10px"">This notification was generated automatically.</p>
        </div>";

            return new Email
            {
                Subject = subject,
                ReceiverEmail = receiverEmail,
                ReceiverName = receiverName,
                SenderEmail = _configuration["BrevoApi:SenderEmail"],
                SenderName = _configuration["BrevoApi:SenderName"],
                EmailBody = html
            };
        }

    }
}
