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
        private readonly IEncryptionService _encryptionService; // ✅ EKLENDİ
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserService> _logger;
        private readonly int _activationTokenExpiryHours;

        public UserService(
            IUserRepository userRepository,
            IEmailService emailService,
            IEncryptionService encryptionService, // ✅ EKLENDİ
            IConfiguration configuration,
            ILogger<UserService> logger)
        {
            _userRepository = userRepository;
            _emailService = emailService;
            _encryptionService = encryptionService; // ✅ EKLENDİ
            _configuration = configuration;
            _logger = logger;
            _activationTokenExpiryHours = int.Parse(configuration["Application:ActivationTokenExpiryHours"] ?? "24");
        }

        public async Task<UserRegistrationResult> RegisterUserAsync(string email, string password, string phoneNumber)
        {
            var result = new UserRegistrationResult();

            try
            {
                // ✅ Email kontrolü - ANCAK sonucu gizle
                bool emailExists = await IsEmailExistsAsync(email);
                
                if (emailExists)
                {
                    // ✅ Mevcut kullanıcıya bildirim gönder
                    await _emailService.SendAccountExistsNotificationAsync(email);
                    
                    _logger.LogWarning("Registration attempted with existing email: {MaskedEmail}", MaskEmail(email));
                    
                    // ✅ HER ZAMAN AYNI MESAJ
                    result.Success = true;  // ⚠️ Success = true döndür (güvenlik için)
                    result.Message = "Kayıt isteğiniz alındı. Lütfen e-posta adresinizi kontrol edin.";
                    return result;
                }

                // ✅ Şifre validasyonu
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
                    _logger.LogWarning("Activation email could not be sent: {MaskedEmail}", MaskEmail(email));
                }

                result.Success = true;
                result.UserId = createdUser.UserId;
                result.Message = "Kayıt isteğiniz alındı. Lütfen e-posta adresinizi kontrol edin.";  // ✅ Aynı mesaj

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user: {MaskedEmail}", MaskEmail(email));
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
                    _logger.LogWarning("Authentication failed - user not found or inactive: {MaskedEmail}", MaskEmail(email));
                    return null;
                }

                if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    _logger.LogWarning("Authentication failed - invalid password: {MaskedEmail}", MaskEmail(email));
                    return null;
                }

                if (!user.IsActivated)
                {
                    _logger.LogWarning("Authentication failed - account not activated: {MaskedEmail}", MaskEmail(email));
                    return null;
                }

                user.LastLoginDate = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);

                _logger.LogInformation("User authenticated successfully: {MaskedEmail}", MaskEmail(email));
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error authenticating user: {MaskedEmail}", MaskEmail(email));
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
            if (string.IsNullOrWhiteSpace(email))
                return false;

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

            // ✅ Minimum uzunluk: 6 → 8
            if (password.Length < 6)
            {
                errorMessage = "Şifre en az 6 karakter olmalıdır.";
                return false;
            }

            if (password.Length > 20)
            {
                errorMessage = "Şifre çok uzun.";
                return false;
            }

            // ✅ YENİ - Karmaşıklık kontrolleri
            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);

            if (!hasUpper)
            {
                errorMessage = "Şifre en az 1 büyük harf içermelidir.";
                return false;
            }

            if (!hasLower)
            {
                errorMessage = "Şifre en az 1 küçük harf içermelidir.";
                return false;
            }

            if (!hasDigit)
            {
                errorMessage = "Şifre en az 1 rakam içermelidir.";
                return false;
            }

            // ✅ YENİ - Yaygın şifre kontrolü
            var commonPasswords = new[]
            {
                "password", "password123", "12345678", "qwerty", "abc123",
                "monkey", "111111", "letmein", "admin", "welcome",
                "123456", "654321", "qwerty123", "password1"
            };

            if (commonPasswords.Contains(password.ToLower()))
            {
                errorMessage = "Bu şifre çok yaygın kullanılıyor. Lütfen daha güvenli bir şifre seçin.";
                return false;
            }

            // ✅ YENİ - Ardışık karakter kontrolü
            if (HasSequentialCharacters(password))
            {
                errorMessage = "Şifre çok fazla ardışık karakter içeriyor (örn: 123, abc).";
                return false;
            }

            return true;
        }

        // ✅ YENİ - Ardışık karakter kontrolü helper
        private bool HasSequentialCharacters(string password)
        {
            for (int i = 0; i < password.Length - 2; i++)
            {
                if (char.IsLetterOrDigit(password[i]) &&
                    char.IsLetterOrDigit(password[i + 1]) &&
                    char.IsLetterOrDigit(password[i + 2]))
                {
                    // Sayısal ardışıklık (123, 321)
                    if (char.IsDigit(password[i]) && char.IsDigit(password[i + 1]) && char.IsDigit(password[i + 2]))
                    {
                        int diff1 = password[i + 1] - password[i];
                        int diff2 = password[i + 2] - password[i + 1];
                        if ((diff1 == 1 && diff2 == 1) || (diff1 == -1 && diff2 == -1))
                            return true;
                    }

                    // Alfabetik ardışıklık (abc, xyz)
                    if (char.IsLetter(password[i]) && char.IsLetter(password[i + 1]) && char.IsLetter(password[i + 2]))
                    {
                        int diff1 = char.ToLower(password[i + 1]) - char.ToLower(password[i]);
                        int diff2 = char.ToLower(password[i + 2]) - char.ToLower(password[i + 1]);
                        if ((diff1 == 1 && diff2 == 1) || (diff1 == -1 && diff2 == -1))
                            return true;
                    }
                }
            }
            return false;
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            try
            {
                if (user == null)
                {
                    _logger.LogWarning("UpdateUserAsync: User is null");
                    return false;
                }

                return await _userRepository.UpdateAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating user: UserId={user?.UserId}");
                return false;
            }
        }

        public async Task<User> GetOrCreateGoogleUserAsync(
            string googleId, 
            string email, 
            string name, 
            string accessToken, 
            string refreshToken,
            string? photoUrl = null) // ✅ EKLENDİ
        {
            try
            {
                // 1. Google ID ile kullanıcı ara
                var user = await _userRepository.GetByGoogleIdAsync(googleId);
                
                if (user != null)
                {
                    // ✅ Mevcut kullanıcı - token'ları ve fotoğrafı güncelle
                    user.GoogleAccessToken = _encryptionService.Encrypt(accessToken);
                    
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        user.GoogleRefreshToken = _encryptionService.Encrypt(refreshToken);
                    }
                    
                    // ✅ PHOTO URL'İ GÜNCELLE
                    if (!string.IsNullOrEmpty(photoUrl))
                    {
                        user.ProfilePhotoUrl = photoUrl;
                    }
                    
                    user.GoogleTokenExpiry = DateTime.UtcNow.AddHours(1);
                    user.LastLoginDate = DateTime.UtcNow;
                    
                    await _userRepository.UpdateAsync(user);
                    _logger.LogInformation($"✅ Existing Google user logged in: {email}");
                    
                    return user;
                }
                
                // 2. Email ile kullanıcı ara
                user = await _userRepository.GetByEmailAsync(email.ToLower().Trim());
                
                if (user != null)
                {
                    // ✅ Normal kayıtlı kullanıcı - Google bilgilerini ekle
                    user.GoogleId = googleId;
                    user.GoogleAccessToken = _encryptionService.Encrypt(accessToken);
                    
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        user.GoogleRefreshToken = _encryptionService.Encrypt(refreshToken);
                    }
                    
                    // ✅ PHOTO URL'İ EKLE
                    if (!string.IsNullOrEmpty(photoUrl))
                    {
                        user.ProfilePhotoUrl = photoUrl;
                    }
                    
                    user.GoogleTokenExpiry = DateTime.UtcNow.AddHours(1);
                    user.IsActivated = true;
                    user.LastLoginDate = DateTime.UtcNow;
                    
                    await _userRepository.UpdateAsync(user);
                    _logger.LogInformation($"✅ Linked Google account to existing user: {email}");
                    
                    return user;
                }
                
                // 3. Yeni kullanıcı oluştur
                var newUser = new User
                {
                    Email = email.ToLower().Trim(),
                    GoogleId = googleId,
                    GoogleAccessToken = _encryptionService.Encrypt(accessToken),
                    GoogleRefreshToken = string.IsNullOrEmpty(refreshToken) 
                        ? null 
                        : _encryptionService.Encrypt(refreshToken),
                    GoogleTokenExpiry = DateTime.UtcNow.AddHours(1),
                    ProfilePhotoUrl = photoUrl, // ✅ EKLENDI
                    IsActivated = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow,
                    LastLoginDate = DateTime.UtcNow,
                    PasswordHash = null // ✅ Google kullanıcıları şifresiz
                };
                
                var createdUser = await _userRepository.CreateAsync(newUser);
                await _emailService.SendWelcomeEmailAsync(email, name);
                
                _logger.LogInformation($"✅ New Google user created: {email} with photo: {photoUrl}");
                
                return createdUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetOrCreateGoogleUserAsync for {email}");
                return null;
            }
        }

        public async Task<User> GetUserByGoogleIdAsync(string googleId)
        {
            return await _userRepository.GetByGoogleIdAsync(googleId);
        }

        // ✅ Class'ın sonuna helper method ekle
        private string MaskEmail(string email)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
                return "***@***.***";
            
            var parts = email.Split('@');
            var username = parts[0].Length > 2 
                ? parts[0].Substring(0, 2) + "***" 
                : "***";
            var domainParts = parts[1].Split('.');
            var domain = domainParts[0].Length > 2
                ? domainParts[0].Substring(0, 2) + "***"
                : "***";
            var extension = domainParts.Length > 1 ? "." + domainParts[^1] : "";
            
            return $"{username}@{domain}{extension}";
        }
    }
}