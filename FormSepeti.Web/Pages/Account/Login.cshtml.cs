using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BCrypt.Net;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Web.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IUserRepository _userRepository;
        public LoginModel(IUserRepository userRepository) => _userRepository = userRepository;

        [BindProperty] public string EmailOrPhone { get; set; }
        [BindProperty] public string Password { get; set; }
        public string Error { get; private set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userRepository.GetByEmailOrPhoneAsync(EmailOrPhone); // implement in repo
            if (user == null || !BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash))
            {
                Error = "Invalid credentials";
                return Page();
            }

            var claims = new List<Claim> {
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