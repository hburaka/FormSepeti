using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Web.Pages.GoogleAuth
{
    public class CallbackModel : PageModel
    {
        private readonly IGoogleSheetsService _googleSheetsService;
        public string Message { get; private set; } = "";

        public CallbackModel(IGoogleSheetsService googleSheetsService) => _googleSheetsService = googleSheetsService;

        private int GetCurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        public async Task<IActionResult> OnGetAsync(string code, string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                Message = "Google izin ekranýnda hata: " + error;
                return Page();
            }

            if (string.IsNullOrEmpty(code))
            {
                Message = "Authorization code alýnamadý.";
                return Page();
            }

            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToPage("/Account/Login");

            var ok = await _googleSheetsService.HandleOAuthCallback(userId, code);
            Message = ok ? "Google hesabýnýz baþarýyla baðlandý." : "Google baðlantýsý baþarýsýz oldu.";

            return Page();
        }
    }
}
