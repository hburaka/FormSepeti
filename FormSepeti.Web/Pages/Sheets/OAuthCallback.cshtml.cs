using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Web.Pages.Sheets
{
    public class OAuthCallbackModel : PageModel
    {
        private readonly IGoogleSheetsService _googleSheetsService;
        private readonly ILogger<OAuthCallbackModel> _logger;
        
        public bool Success { get; private set; }
        public string? ErrorMessage { get; private set; }

        public OAuthCallbackModel(
            IGoogleSheetsService googleSheetsService,
            ILogger<OAuthCallbackModel> logger)
        {
            _googleSheetsService = googleSheetsService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(string code, string state, string error)
        {
            // ✅ Kullanıcı izni reddetmiş
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning($"OAuth error: {error}");
                TempData["Error"] = "Google bağlantısı iptal edildi.";
                return RedirectToPage("/Sheets/Connect");
            }

            // ✅ Eksik parametreler
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                _logger.LogWarning("OAuth callback missing code or state");
                TempData["Error"] = "Geçersiz yönlendirme parametreleri.";
                return RedirectToPage("/Sheets/Connect");
            }

            // ✅ UserId parse
            if (!int.TryParse(state, out var userId))
            {
                _logger.LogError($"Invalid state parameter: {state}");
                TempData["Error"] = "Geçersiz kullanıcı bilgisi.";
                return RedirectToPage("/Sheets/Connect");
            }

            // ✅ Token exchange
            Success = await _googleSheetsService.HandleOAuthCallback(userId, code);
            
            if (Success)
            {
                _logger.LogInformation($"✅ Google Sheets connected successfully for UserId={userId}");
                TempData["Success"] = "Google Sheets başarıyla bağlandı! ✓";
                return RedirectToPage("/Dashboard/Index");
            }
            else
            {
                _logger.LogError($"❌ OAuth callback failed for UserId={userId}");
                TempData["Error"] = "Google bağlantısı sırasında bir hata oluştu. Lütfen tekrar deneyin.";
                return RedirectToPage("/Sheets/Connect");
            }
        }
    }
}
