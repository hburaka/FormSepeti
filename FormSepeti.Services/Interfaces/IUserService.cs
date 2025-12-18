using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Services.Models;

namespace FormSepeti.Services.Interfaces
{
    public interface IUserService
    {
        Task<UserRegistrationResult> RegisterUserAsync(string email, string password, string phoneNumber);
        Task<User> AuthenticateAsync(string email, string password);
        Task<bool> ActivateAccountAsync(string activationToken);
        Task<bool> ResendActivationEmailAsync(string email);
        Task<bool> RequestPasswordResetAsync(string email);
        Task<bool> ResetPasswordAsync(string resetToken, string newPassword);
        Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword);
        Task<User> GetUserByIdAsync(int userId);
        Task<bool> IsEmailExistsAsync(string email);
        Task<bool> UpdateUserAsync(User user);
    }
}
