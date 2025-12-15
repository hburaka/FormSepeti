using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Web.Pages.Sheets
{
    public class OAuthCallbackModel : PageModel
    {
        private readonly IGoogleSheetsService _googleSheetsService;
        public bool Success { get; private set; }
        public string? ErrorMessage { get; private set; }

        public OAuthCallbackModel(IGoogleSheetsService googleSheetsService) => _googleSheetsService = googleSheetsService;

        public async Task<IActionResult> OnGetAsync(string code, string state)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                Success = false; ErrorMessage = "Eksik kod veya state."; return Page();
            }
            if (!int.TryParse(state, out var userId)) { Success = false; ErrorMessage = "State hatalý."; return Page(); }

            Success = await _googleSheetsService.HandleOAuthCallback(userId, code);
            if (!Success) ErrorMessage = "Token alýnamadý veya kaydedilemedi.";
            return Page();
        }
    }
}
