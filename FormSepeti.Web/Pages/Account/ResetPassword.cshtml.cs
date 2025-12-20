using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Web.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly ILogger<ResetPasswordModel> _logger;

        public ResetPasswordModel(
            IUserService userService,
            ILogger<ResetPasswordModel> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string Token { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Yeni şifre gereklidir")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır")]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Şifre tekrarı gereklidir")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Şifreler eşleşmiyor")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public bool IsValidToken { get; set; }
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrWhiteSpace(Token))
            {
                ErrorMessage = "Geçersiz sıfırlama bağlantısı.";
                IsValidToken = false;
                _logger.LogWarning("Password reset page accessed without token");
                return Page();
            }

            // ✅ Token geçerliliğini kontrol et (UserService'de zaten var)
            // ResetPasswordAsync metodunu test çağrısı yapmak yerine, 
            // doğrudan token varlığını kontrol edelim
            IsValidToken = true; // İlk yüklemede true, form gönderiminde kontrol edilecek

            _logger.LogInformation($"Password reset page accessed with token: {Token.Substring(0, 10)}...");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                IsValidToken = true;
                return Page();
            }

            var result = await _userService.ResetPasswordAsync(Token, NewPassword);

            if (result)
            {
                SuccessMessage = "Şifreniz başarıyla sıfırlandı. Giriş yapabilirsiniz.";
                IsValidToken = false;
                return Page();
            }
            else
            {
                // ✅ YENİ: Şifre validation hatası göster
                ErrorMessage = "Şifre sıfırlama başarısız. Şifreniz şu kurallara uymalıdır:\n" +
                               "• En az 6 karakter\n" +
                               "• En az 1 büyük harf, 1 küçük harf, 1 rakam\n" +
                               "• Ardışık karakterler içermemeli (123, abc)";
                
                _logger.LogWarning("Password reset failed for token: {Token}", Token.Substring(0, 10) + "...");
                IsValidToken = true;
                return Page();
            }
        }
    }
}
