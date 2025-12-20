using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Web.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly ILoginAttemptService _loginAttemptService;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            IUserService userService,
            ILoginAttemptService loginAttemptService,
            ILogger<LoginModel> logger)
        {
            _userService = userService;
            _loginAttemptService = loginAttemptService;
            _logger = logger;
        }

        [BindProperty]
        [Required(ErrorMessage = "E-posta veya telefon gerekli")]
        public string EmailOrPhone { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Şifre gerekli")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public bool RememberMe { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool LoggedOut { get; set; }

        public string? Error { get; set; }
        public int RemainingAttempts { get; set; }

        public void OnGet()
        {
            if (LoggedOut)
            {
                TempData["Success"] = "Başarıyla çıkış yaptınız.";
            }
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/Dashboard/Index");

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (await _loginAttemptService.IsIpBlockedAsync(ipAddress))
            {
                var blockTimeRemaining = _loginAttemptService.GetLockoutTimeRemaining(ipAddress);
                Error = $"Çok fazla başarısız giriş denemesi yaptınız. Lütfen {blockTimeRemaining.Minutes} dakika {blockTimeRemaining.Seconds} saniye sonra tekrar deneyin.";
                return Page();
            }

            var user = await _userService.AuthenticateAsync(EmailOrPhone, Password);

            if (user == null)
            {
                await _loginAttemptService.RecordFailedLoginAsync(EmailOrPhone, ipAddress);
                RemainingAttempts = _loginAttemptService.GetRemainingAttempts(ipAddress);
                
                Error = "E-posta/telefon veya şifre hatalı.";
                return Page();
            }

            _loginAttemptService.ResetAttempts(ipAddress);

            // ✅ Claims listesi
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("UserId", user.UserId.ToString())
            };

            // ✅ YENİ: FirstName ve LastName claim'leri ekle
            if (!string.IsNullOrWhiteSpace(user.FirstName))
            {
                claims.Add(new Claim("FirstName", user.FirstName));
            }

            if (!string.IsNullOrWhiteSpace(user.LastName))
            {
                claims.Add(new Claim("LastName", user.LastName));
            }

            // ✅ Profile Photo URL
            if (!string.IsNullOrWhiteSpace(user.ProfilePhotoUrl))
            {
                claims.Add(new Claim("ProfilePhotoUrl", user.ProfilePhotoUrl));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = RememberMe,
                ExpiresUtc = RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(12)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            _logger.LogInformation($"User logged in: {user.Email}");

            return LocalRedirect(returnUrl);
        }
    }
}