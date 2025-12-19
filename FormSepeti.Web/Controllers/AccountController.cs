using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Web.Controllers
{
    [Route("[controller]/[action]")]
    public class AccountController : Controller
    {
        private readonly IUserService _userService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IUserService userService,
            ILogger<AccountController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult GoogleLogin(string returnUrl = null)
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action("GoogleCallback", new { returnUrl }),
                Items = { { "LoginProvider", "Google" } }
            };
            
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleCallback(string returnUrl = null)
        {
            try
            {
                // Google'dan gelen authentication bilgilerini al
                var authenticateResult = await HttpContext.AuthenticateAsync(
                    GoogleDefaults.AuthenticationScheme);
                
                if (!authenticateResult.Succeeded)
                {
                    _logger.LogWarning("Google authentication failed");
                    TempData["Error"] = "Google girişi başarısız oldu.";
                    return RedirectToPage("/Account/Login");
                }
                
                // Google bilgilerini çek
                var googleId = authenticateResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = authenticateResult.Principal.FindFirst(ClaimTypes.Email)?.Value;
                var name = authenticateResult.Principal.FindFirst(ClaimTypes.Name)?.Value;
                
                // Token'ları al
                var tokens = authenticateResult.Properties.GetTokens();
                var accessToken = tokens.FirstOrDefault(t => t.Name == "access_token")?.Value;
                var refreshToken = tokens.FirstOrDefault(t => t.Name == "refresh_token")?.Value;
                
                _logger.LogInformation($"Google callback - GoogleId: {googleId}, Email: {email}, HasRefreshToken: {!string.IsNullOrEmpty(refreshToken)}");
                
                if (string.IsNullOrEmpty(googleId) || string.IsNullOrEmpty(email))
                {
                    _logger.LogError("Google callback - Missing googleId or email");
                    TempData["Error"] = "Google hesap bilgileri alınamadı.";
                    return RedirectToPage("/Account/Login");
                }
                
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogError("Google callback - Missing access token");
                    TempData["Error"] = "Google erişim izni alınamadı.";
                    return RedirectToPage("/Account/Login");
                }
                
                // Kullanıcıyı oluştur veya güncelle
                var user = await _userService.GetOrCreateGoogleUserAsync(
                    googleId, 
                    email, 
                    name ?? email.Split('@')[0], 
                    accessToken, 
                    refreshToken);
                
                if (user == null)
                {
                    _logger.LogError("Failed to create/update Google user");
                    TempData["Error"] = "Kullanıcı oluşturulamadı.";
                    return RedirectToPage("/Account/Login");
                }
                
                // Cookie authentication claim'leri oluştur
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("UserId", user.UserId.ToString()),
                    new Claim("Email", user.Email),
                    new Claim("LoginProvider", "Google")
                };
                
                var claimsIdentity = new ClaimsIdentity(
                    claims, 
                    CookieAuthenticationDefaults.AuthenticationScheme);
                
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
                };
                
                // Kullanıcıyı login et
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);
                
                _logger.LogInformation($"✅ User logged in via Google: {email}");
                
                // Return URL'e yönlendir veya Dashboard'a git
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                
                return RedirectToPage("/Dashboard/Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Google callback");
                TempData["Error"] = "Giriş sırasında bir hata oluştu.";
                return RedirectToPage("/Account/Login");
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            
            // Cookie authentication'dan çıkış yap
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            // Session'ı temizle
            HttpContext.Session.Clear();
            
            _logger.LogInformation($"✅ User logged out: {userEmail}");
            
            TempData["Success"] = "Başarıyla çıkış yaptınız.";
            return RedirectToPage("/Account/Login");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> LogoutGet()
        {
            // GET request için de çıkış yapabilmek amacıyla
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            
            _logger.LogInformation($"✅ User logged out: {userEmail}");
            
            TempData["Success"] = "Başarıyla çıkış yaptınız.";
            return RedirectToPage("/Account/Login");
        }
    }
}