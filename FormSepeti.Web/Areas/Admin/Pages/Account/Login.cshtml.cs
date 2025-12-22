// FormSepeti.Web/Areas/Admin/Pages/Account/Login.cshtml.cs
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Web.Areas.Admin.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IAdminService _adminService;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(IAdminService adminService, ILogger<LoginModel> logger)
        {
            _adminService = adminService;
            _logger = logger;
        }

        [BindProperty]
        [Required(ErrorMessage = "Kullanýcý adý gerekli")]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Þifre gerekli")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public bool RememberMe { get; set; }

        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            // Eðer zaten login ise dashboard'a yönlendir
            if (User.Identity?.IsAuthenticated == true)
            {
                Response.Redirect("/Admin/Dashboard");
            }
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= "/Admin/Dashboard";

            if (!ModelState.IsValid)
            {
                ErrorMessage = "Lütfen tüm alanlarý doldurun.";
                return Page();
            }

            var admin = await _adminService.AuthenticateAsync(Username, Password);

            if (admin == null)
            {
                ErrorMessage = "Kullanýcý adý veya þifre hatalý.";
                _logger.LogWarning($"Failed admin login attempt: {Username}");
                return Page();
            }

            // Claims oluþtur
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, admin.AdminId.ToString()),
                new Claim(ClaimTypes.Name, admin.Username),
                new Claim(ClaimTypes.Email, admin.Email),
                new Claim("FullName", admin.FullName),
                new Claim("Role", admin.Role),
                new Claim("AdminId", admin.AdminId.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, "AdminScheme");
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = RememberMe,
                ExpiresUtc = RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(7)
                    : DateTimeOffset.UtcNow.AddHours(12)
            };

            await HttpContext.SignInAsync(
                "AdminScheme",
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            _logger.LogInformation($"Admin logged in: {admin.Username}");

            return LocalRedirect(returnUrl);
        }
    }
}