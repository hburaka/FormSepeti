using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;
using FormSepeti.Data.Repositories.Interfaces;
using BCrypt.Net;
using Microsoft.Extensions.Logging;

namespace FormSepeti.Web.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(IUserService userService, IUserRepository userRepository, ILogger<LoginModel> logger)
        {
            _userService = userService;
            _userRepository = userRepository;
            _logger = logger;
        }

        [BindProperty] public string EmailOrPhone { get; set; }
        [BindProperty] public string Password { get; set; }
        public string Error { get; private set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(EmailOrPhone) || string.IsNullOrWhiteSpace(Password))
            {
                Error = "Email/Telefon ve þifre gereklidir.";
                ModelState.AddModelError(string.Empty, Error);
                return Page();
            }

            // Try email-auth with service if value looks like email
            bool isEmail = EmailOrPhone.Contains("@");
            FormSepeti.Data.Entities.User user = null;

            if (isEmail)
            {
                user = await _userService.AuthenticateAsync(EmailOrPhone, Password);
            }
            else
            {
                // fallback: find by phone/email, verify password locally
                user = await _userRepository.GetByEmailOrPhoneAsync(EmailOrPhone);
                if (user != null)
                {
                    if (string.IsNullOrEmpty(user.PasswordHash))
                    {
                        _logger.LogWarning("User found but PasswordHash is empty for {EmailOrPhone}", EmailOrPhone);
                        user = null;
                    }
                    else
                    {
                        _logger.LogDebug("Verifying password for {EmailOrPhone}. StoredHashPrefix={HashPrefix} Length={Length}",
                            EmailOrPhone,
                            user.PasswordHash.Length >= 4 ? user.PasswordHash.Substring(0, 4) : user.PasswordHash,
                            user.PasswordHash.Length);

                        bool ok = false;
                        try
                        {
                            ok = BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash);
                            _logger.LogDebug("BCrypt.Verify result={Result}", ok);
                        }
                        catch (System.Exception ex)
                        {
                            _logger.LogError(ex, "BCrypt.Verify threw an exception for user {EmailOrPhone}", EmailOrPhone);
                        }

                        if (!ok)
                        {
                            _logger.LogWarning("Password verification failed for {EmailOrPhone}", EmailOrPhone);
                            user = null;
                        }
                        else
                        {
                            // ensure activated/active
                            if (!user.IsActivated || !user.IsActive) user = null;
                        }
                    }
                }
            }

            if (user == null)
            {
                Error = "Geçersiz kullanýcý veya þifre.";
                ModelState.AddModelError(string.Empty, Error);
                return Page();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Email ?? user.PhoneNumber ?? "")
            };

            var identity = new ClaimsIdentity(claims, "Cookie");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("Cookie", principal);
            return RedirectToPage("/Index");
        }
    }
}