using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;
using FormSepeti.Services.Models;

namespace FormSepeti.Web.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly IUserService _userService;

        public RegisterModel(IUserService userService) => _userService = userService;

        [BindProperty] public string EmailOrPhone { get; set; }
        [BindProperty] public string Password { get; set; }
        [BindProperty] public string ConfirmPassword { get; set; }
        [BindProperty] public bool AcceptTerms { get; set; }

        public string ErrorMessage { get; private set; }
        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!AcceptTerms)
            {
                ModelState.AddModelError(string.Empty, "Kullaným þartlarýný kabul etmelisiniz.");
                return Page();
            }

            if (string.IsNullOrWhiteSpace(EmailOrPhone) || string.IsNullOrWhiteSpace(Password))
            {
                ModelState.AddModelError(string.Empty, "Email/Telefon ve þifre gereklidir.");
                return Page();
            }

            if (Password != ConfirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Þifreler eþleþmiyor.");
                return Page();
            }

            var email = EmailOrPhone.Contains("@") ? EmailOrPhone : null;
            var phone = EmailOrPhone.Contains("@") ? null : EmailOrPhone;

            var result = await _userService.RegisterUserAsync(email, Password, phone);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Kayýt baþarýsýz.");
                return Page();
            }

            TempData["Success"] = result.Message;
            return RedirectToPage("/Account/Login");
        }
    }
}