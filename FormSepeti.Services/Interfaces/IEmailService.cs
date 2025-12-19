using System.Threading.Tasks;

namespace FormSepeti.Services.Interfaces
{
    public interface IEmailService
    {
        Task<bool> SendActivationEmailAsync(string toEmail, string userName, string activationToken);
        Task<bool> SendWelcomeEmailAsync(string toEmail, string userName);
        Task<bool> SendPasswordResetEmailAsync(string toEmail, string userName, string resetToken);
        Task<bool> SendFormSubmissionNotificationAsync(string toEmail, string formTitle, int submissionCount);
        Task<bool> SendAccountExistsNotificationAsync(string toEmail);
        Task<bool> SendPackagePurchaseConfirmationAsync(string toEmail, string userName, string packageName, decimal amount);
    }
}