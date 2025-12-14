using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Services.Interfaces;
using FormSepeti.Services.Models;

namespace FormSepeti.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserService> _logger;
        private readonly int _activationTokenExpiryHours;

        public UserService(
            IUserRepository userRepository,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<UserService> logger)
        {
            _userRepository = userRepository;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
            _activationTokenExpiryHours = int.Parse(configuration["Application:ActivationTokenExpiryHours"] ?? "24");
        }

        public async Task<UserRegistrationResult> RegisterUserAsync(string email, string password, string phoneNumber)
        {
            var result = new UserRegistrationResult();

            try
            {
                if (await IsEmailExistsAsync(email))
                {
                    result.ErrorMessage = "Bu e-posta adresi zaten kullanılıyor.";
                    return result;
                }

                if (!ValidatePassword(password, out string passwordError))
                {
                    result.ErrorMessage = passwordError;
                    return result;
                }

                var activationToken = GenerateSecureToken();
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

                var user = new User
                {
                    Email = email.ToLower().Trim(),
                    PhoneNumber = phoneNumber?.Trim(),
                    PasswordHash = passwordHash,
                    IsActivated = false,
                    ActivationToken = activationToken,
                    ActivationTokenExpiry = DateTime.UtcNow.AddHours(_activationTokenExpiryHours),
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                };

                var createdUser = await _userRepository.CreateAsync(user);

                var emailSent = await _emailService.SendActivationEmailAsync(
                    user.Email,
                    user.Email.Split('@')[0],
                    activationToken
                );

                if (!emailSent)
                {
                    _logger.LogWarning($"Activation email could not be sent to {email}");
                }

                result.Success = true;
                result.UserId = createdUser.UserId;
                result.Message = "Kayıt başarılı! Lütfen e-posta adresinizi kontrol edin ve hesabınızı aktive edin.";

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error registering user: {email}");
                result.ErrorMessage = "Kayıt sırasında bir hata oluştu. Lütfen tekrar deneyin.";
                return result;
            }
        }

        public async Task<User> AuthenticateAsync(string email, string password)
        {
            try
            {
                var user = await _userRepository.GetByEmailAsync(email.ToLower().Trim());

                if (user == null || !user.IsActive)
                {
                    return null;
                }

                if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    return null;
                }

                if (!user.IsActivated)
                {
                    return null;
                }

                user.LastLoginDate = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error authenticating user: {email}");
                return null;
            }
        }

        public async Task<bool> ActivateAccountAsync(string activationToken)
        {
            try
            {
                var user = await _userRepository.GetByActivationTokenAsync(activationToken);

                if (user == null)
                {
                    return false;
                }

                if (user.ActivationTokenExpiry < DateTime.UtcNow)
                {
                    return false;
                }

                if (user.IsActivated)
                {
                    return true;
                }

                user.IsActivated = true;
                user.ActivationToken = null;
                user.ActivationTokenExpiry = null;

                await _userRepository.UpdateAsync(user);
                await _emailService.SendWelcomeEmailAsync(user.Email, user.Email.Split('@')[0]);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error activating account: {activationToken}");
                return false;
            }
        }

        public async Task<bool> ResendActivationEmailAsync(string email)
        {
            try
            {
                var user = await _userRepository.GetByEmailAsync(email.ToLower().Trim());

                if (user == null || user.IsActivated)
                {
                    return false;
                }

                var activationToken = GenerateSecureToken();
                user.ActivationToken = activationToken;
                user.ActivationTokenExpiry = DateTime.UtcNow.AddHours(_activationTokenExpiryHours);

                await _userRepository.UpdateAsync(user);

                return await _emailService.SendActivationEmailAsync(
                    user.Email,
                    user.Email.Split('@')[0],
                    activationToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resending activation email: {email}");
                return false;
            }
        }

        public async Task<bool> RequestPasswordResetAsync(string email)
        {
            try
            {
                var user = await _userRepository.GetByEmailAsync(email.ToLower().Trim());

                if (user == null)
                {
                    return true;
                }

                var resetToken = GenerateSecureToken();
                user.ActivationToken = resetToken;
                user.ActivationTokenExpiry = DateTime.UtcNow.AddHours(1);

                await _userRepository.UpdateAsync(user);

                await _emailService.SendPasswordResetEmailAsync(
                    user.Email,
                    user.Email.Split('@')[0],
                    resetToken
                );

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error requesting password reset: {email}");
                return false;
            }
        }

        public async Task<bool> ResetPasswordAsync(string resetToken, string newPassword)
        {
            try
            {
                var user = await _userRepository.GetByActivationTokenAsync(resetToken);

                if (user == null || user.ActivationTokenExpiry < DateTime.UtcNow)
                {
                    return false;
                }

                if (!ValidatePassword(newPassword, out string passwordError))
                {
                    return false;
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                user.ActivationToken = null;
                user.ActivationTokenExpiry = null;

                await _userRepository.UpdateAsync(user);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resetting password: {resetToken}");
                return false;
            }
        }

        public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    return false;
                }

                if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash))
                {
                    return false;
                }

                if (!ValidatePassword(newPassword, out string passwordError))
                {
                    return false;
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                await _userRepository.UpdateAsync(user);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error changing password for user: {userId}");
                return false;
            }
        }

        public async Task<User> GetUserByIdAsync(int userId)
        {
            return await _userRepository.GetByIdAsync(userId);
        }

        public async Task<bool> IsEmailExistsAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email.ToLower().Trim());
            return user != null;
        }

        private string GenerateSecureToken()
        {
            var randomBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        private bool ValidatePassword(string password, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(password))
            {
                errorMessage = "Şifre boş olamaz.";
                return false;
            }

            if (password.Length < 6)
            {
                errorMessage = "Şifre en az 6 karakter olmalıdır.";
                return false;
            }

            if (password.Length > 100)
            {
                errorMessage = "Şifre çok uzun.";
                return false;
            }

            return true;
        }
    }
}