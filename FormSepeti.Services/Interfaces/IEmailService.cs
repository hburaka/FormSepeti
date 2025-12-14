using System.Threading.Tasks;

namespace FormSepeti.Services.Interfaces
{
    public interface IEmailService
    {
        Task<bool> SendActivationEmailAsync(string toEmail, string userName, string activationToken);
        Task<bool> SendPasswordResetEmailAsync(string toEmail, string userName, string resetToken);
        Task<bool> SendWelcomeEmailAsync(string toEmail, string userName);
        Task<bool> SendPackagePurchaseConfirmationAsync(string toEmail, string userName, string packageName, decimal amount);
        Task<bool> SendCustomEmailAsync(string toEmail, string subject, string body);
    }
}