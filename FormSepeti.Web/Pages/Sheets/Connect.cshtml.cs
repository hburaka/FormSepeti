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
        private readonly IUserService _userService;
        private readonly ILogger<ConnectModel> _logger;

        public string? ErrorMessage { get; private set; }
        public bool IsAlreadyConnected { get; private set; }
        public bool IsGoogleLogin { get; private set; }
        public string? UserEmail { get; private set; }
        public DateTime? LastConnectionDate { get; private set; } // ✅ YENİ
        public int ActiveSheetsCount { get; private set; } // ✅ YENİ
        public string ConnectionStatus { get; private set; } = "disconnected"; // ✅ YENİ

        public ConnectModel(
            IGoogleSheetsService googleSheetsService,
            IUserService userService,
            ILogger<ConnectModel> logger)
        {
            _googleSheetsService = googleSheetsService;
            _userService = userService;
            _logger = logger;
        }

        private int GetCurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                _logger.LogWarning("Unauthorized access attempt to Sheets/Connect");
                TempData["Error"] = "Lütfen önce giriş yapın.";
                return RedirectToPage("/Account/Login");
            }

            try
            {
                var user = await _userService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogError($"User not found: UserId={userId}");
                    TempData["Error"] = "Kullanıcı bulunamadı.";
                    return RedirectToPage("/Account/Login");
                }

                UserEmail = user.Email;
                IsGoogleLogin = User.FindFirst("LoginProvider")?.Value == "Google";

                // ✅ Detaylı bağlantı kontrolü
                IsAlreadyConnected = !string.IsNullOrEmpty(user.GoogleRefreshToken) &&
                                      !string.IsNullOrEmpty(user.GoogleAccessToken);

                if (IsAlreadyConnected)
                {
                    ConnectionStatus = "connected";
                    LastConnectionDate = user.GoogleTokenExpiry?.AddDays(-30); // Token'dan tahmin
                    
                    // ✅ Aktif sheet sayısını al
                    var sheets = await _googleSheetsService.GetUserSheetsAsync(userId);
                    ActiveSheetsCount = sheets?.Count ?? 0;

                    // ✅ Token süresi kontrolü
                    if (user.GoogleTokenExpiry.HasValue && user.GoogleTokenExpiry.Value <= DateTime.UtcNow)
                    {
                        TempData["Warning"] = "Google Sheets token'ınızın süresi dolmuş. Lütfen yeniden bağlanın.";
                        ConnectionStatus = "expired";
                        IsAlreadyConnected = false;
                    }
                    else
                    {
                        TempData["Success"] = "Google Sheets bağlantınız aktif!";
                    }
                }
                else if (IsGoogleLogin)
                {
                    ConnectionStatus = "needs_permissions";
                    TempData["Info"] = "Google ile giriş yaptınız. Sheets erişimi için ek izin verin.";
                }
                else
                {
                    ConnectionStatus = "disconnected";
                    TempData["Info"] = "Google Sheets'e bağlanarak formlarınızı otomatik kaydedin.";
                }

                _logger.LogInformation($"Sheets/Connect page loaded: UserId={userId}, Status={ConnectionStatus}");
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading Sheets/Connect for UserId={userId}");
                ErrorMessage = "Sayfa yüklenirken bir hata oluştu.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                ErrorMessage = "Kullanıcı giriş yapmamış.";
                _logger.LogWarning("Unauthenticated OAuth attempt");
                return Page();
            }

            try
            {
                var user = await _userService.GetUserByIdAsync(userId);
                if (user != null && !string.IsNullOrEmpty(user.GoogleRefreshToken))
                {
                    TempData["Warning"] = "Google Sheets bağlantınız zaten aktif!";
                    _logger.LogInformation($"User already connected: UserId={userId}");
                    return RedirectToPage();
                }

                _logger.LogInformation($"Initiating Google OAuth for UserId={userId}");

                var authUrl = await _googleSheetsService.GetAuthorizationUrl(userId);
                if (string.IsNullOrEmpty(authUrl))
                {
                    ErrorMessage = "Yetkilendirme URL'si oluşturulamadı. Lütfen tekrar deneyin.";
                    _logger.LogError($"Failed to generate OAuth URL for UserId={userId}");
                    return Page();
                }

                TempData["Info"] = "Google yetkilendirme sayfasına yönlendiriliyorsunuz...";
                return Redirect(authUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during OAuth initiation for UserId={userId}");
                ErrorMessage = "Bağlantı başlatılırken bir hata oluştu. Lütfen tekrar deneyin.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDisconnectAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                TempData["Error"] = "Lütfen giriş yapın.";
                return RedirectToPage("/Account/Login");
            }

            try
            {
                var user = await _userService.GetUserByIdAsync(userId);
                if (user != null)
                {
                    _logger.LogInformation($"Disconnecting Google Sheets for UserId={userId}");

                    user.GoogleAccessToken = null;
                    user.GoogleRefreshToken = null;
                    user.GoogleTokenExpiry = null;

                    await _userService.UpdateUserAsync(user);

                    TempData["Success"] = "Google Sheets bağlantınız başarıyla kaldırıldı.";
                    _logger.LogInformation($"Successfully disconnected: UserId={userId}");
                }
                else
                {
                    TempData["Error"] = "Kullanıcı bulunamadı.";
                    _logger.LogWarning($"User not found during disconnect: UserId={userId}");
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error disconnecting Sheets for UserId={userId}");
                TempData["Error"] = "Bağlantı kaldırılırken bir hata oluştu.";
                return RedirectToPage();
            }
        }

        // ✅ YENİ: Test bağlantısı
        public async Task<IActionResult> OnPostTestConnectionAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return new JsonResult(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var isValid = await _googleSheetsService.TestConnectionAsync(userId);
                
                if (isValid)
                {
                    TempData["Success"] = "Bağlantı testi başarılı! ✓";
                    return new JsonResult(new { success = true, message = "Connection OK" });
                }
                else
                {
                    TempData["Error"] = "Bağlantı testi başarısız. Lütfen yeniden bağlanın.";
                    return new JsonResult(new { success = false, message = "Connection failed" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Connection test failed for UserId={userId}");
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
    }
}
