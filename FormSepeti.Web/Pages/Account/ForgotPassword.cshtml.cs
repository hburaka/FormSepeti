using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Web.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            IUserService userService,
            ILogger<ForgotPasswordModel> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [BindProperty]
        [Required(ErrorMessage = "E-posta adresi gereklidir")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin")]
        public string Email { get; set; } = string.Empty;

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsRequestSent { get; set; }

        public void OnGet()
        {
            // Sayfa ilk açıldığında boş form göster
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // ✅ Güvenlik: Her zaman başarılı mesaj döndür (email enumeration önleme)
                var result = await _userService.RequestPasswordResetAsync(Email.Trim());

                // ✅ Kullanıcıya her zaman aynı mesajı göster
                IsRequestSent = true;
                SuccessMessage = "Eğer bu e-posta kayıtlıysa, şifre sıfırlama bağlantısı gönderildi.";

                _logger.LogInformation($"Password reset requested for email: {MaskEmail(Email)}");

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during password reset request for: {MaskEmail(Email)}");
                ErrorMessage = "Bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
                return Page();
            }
        }

        // ✅ Helper: Email maskeleme (loglama için)
        private string MaskEmail(string email)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
                return "***@***.***";

            var parts = email.Split('@');
            var username = parts[0].Length > 2
                ? parts[0].Substring(0, 2) + "***"
                : "***";
            var domain = parts[1].Length > 2
                ? parts[1].Substring(0, 2) + "***"
                : "***";

            return $"{username}@{domain}";
        }
    }
}
