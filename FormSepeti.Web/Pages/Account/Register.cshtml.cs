using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BCrypt.Net;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Web.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;

        public RegisterModel(IUserRepository userRepository, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _configuration = configuration;
        }

        [BindProperty] public string? EmailOrPhone { get; set; }
        [BindProperty] public string? Password { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(EmailOrPhone) || string.IsNullOrWhiteSpace(Password))
                return Page();

            var normalized = EmailOrPhone!.Trim();
            var isEmail = normalized.Contains("@", StringComparison.Ordinal);

            var token = Guid.NewGuid().ToString("N");
            var expiryHours = 24;
            if (int.TryParse(_configuration["Application:ActivationTokenExpiryHours"], out var h))
                expiryHours = h;

            var user = new User
            {
                Email = isEmail ? normalized : null,
                PhoneNumber = isEmail ? null : normalized,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password!),
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                IsActivated = false,
                ActivationToken = token,
                ActivationTokenExpiry = DateTime.UtcNow.AddHours(expiryHours),

                // short-term fix: ensure non-null values for DB columns that don't allow NULL
                GoogleAccessToken = string.Empty,
                GoogleRefreshToken = string.Empty
            };

            await _userRepository.CreateAsync(user);
            return RedirectToPage("Login");
        }
    }
}