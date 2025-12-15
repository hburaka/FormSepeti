using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Web.Pages.Sheets
{
    public class ConnectModel : PageModel
    {
        private readonly IGoogleSheetsService _googleSheetsService;
        public string? ErrorMessage { get; private set; }

        public ConnectModel(IGoogleSheetsService googleSheetsService) => _googleSheetsService = googleSheetsService;

        private int GetCurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) { ErrorMessage = "Kullanýcý giriþ yapmamýþ."; return Page(); }

            var authUrl = await _googleSheetsService.GetAuthorizationUrl(userId);
            if (string.IsNullOrEmpty(authUrl)) { ErrorMessage = "Yetkilendirme URL'si oluþturulamadý."; return Page(); }

            return Redirect(authUrl);
        }
    }
}
