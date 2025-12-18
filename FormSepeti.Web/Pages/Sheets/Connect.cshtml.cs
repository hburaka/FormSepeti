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
        private readonly IUserService _userService; // ? EKLE

        public string? ErrorMessage { get; private set; }
        public bool IsAlreadyConnected { get; private set; } // ? EKLE
        public string? UserEmail { get; private set; } // ? EKLE

        public ConnectModel(
            IGoogleSheetsService googleSheetsService,
            IUserService userService) // ? EKLE
        {
            _googleSheetsService = googleSheetsService;
            _userService = userService; // ? EKLE
        }

        private int GetCurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        public async Task<IActionResult> OnGetAsync() // ? async Task ekle
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return RedirectToPage("/Account/Login");
            }

            // ? Kullanýcýnýn Google baðlantýsýný kontrol et
            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }

            UserEmail = user.Email;

            // ? GoogleRefreshToken varsa ? ZATEN BAÐLI!
            IsAlreadyConnected = !string.IsNullOrEmpty(user.GoogleRefreshToken);

            if (IsAlreadyConnected)
            {
                TempData["Info"] = "Google Sheets baðlantýnýz zaten aktif!";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                ErrorMessage = "Kullanýcý giriþ yapmamýþ.";
                return Page();
            }

            // ? Zaten baðlýysa tekrar baðlanmaya gerek yok
            var user = await _userService.GetUserByIdAsync(userId);
            if (user != null && !string.IsNullOrEmpty(user.GoogleRefreshToken))
            {
                TempData["Warning"] = "Google Sheets baðlantýnýz zaten aktif!";
                return RedirectToPage("/Dashboard/Index");
            }

            var authUrl = await _googleSheetsService.GetAuthorizationUrl(userId);
            if (string.IsNullOrEmpty(authUrl))
            {
                ErrorMessage = "Yetkilendirme URL'si oluþturulamadý.";
                return Page();
            }

            return Redirect(authUrl); // Google OAuth'a yönlendir
        }

        // ? YENÝ: Baðlantýyý kaldýr
        public async Task<IActionResult> OnPostDisconnectAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return RedirectToPage("/Account/Login");
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user != null)
            {
                user.GoogleAccessToken = null;
                user.GoogleRefreshToken = null;
                user.GoogleTokenExpiry = null;

                await _userService.UpdateUserAsync(user);

                TempData["Success"] = "Google Sheets baðlantýnýz kaldýrýldý.";
            }

            return RedirectToPage();
        }
    }
}
