using UsersService.Models.DTOs;

namespace UsersService.Interfaces
{
    public interface IEmailBuilder
    {
        public Email BuildConfirmationEmail(string emailAddress, string firstName, string confirmationLink);
        public Email BuildPasswordResetEmail(string emailAddress, string firstName, string callbackUrl);
        public Email BuildChangedPasswordNotificationEmail(string emailAddress, string firstName);
        public Email BuildAccountApprovedEmail(string emailAddress, string userName, string loginUrl);
        public Email BuildTemporaryPasswordEmail(string emailAddress, string firstName, string temporaryPassword, string loginUrl);
        public Email BuildTemporaryPasswordForOrganizationAdminEmail(string emailAddress, string firstName, string orgName, string temporaryPassword, string loginUrl);
        public Email BuildSubscriptionAboutToExpireEmail(string name, string planName);
        Email BuildTokenAlertEmail(string receiverEmail, string receiverName, string orgName, Guid chatbotId, long used, long limit, string monthKey, long[] thresholdsHit);
    }
}
