using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace FormSepeti.Web.Areas.Admin.Pages.Users
{
    [Authorize(Policy = "AdminOnly")]
    public class EditModel : PageModel
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<EditModel> _logger;

        public EditModel(
            IUserRepository userRepository,
            ILogger<EditModel> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
        }

        [BindProperty]
        public UserEditViewModel Input { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            _logger.LogInformation("Admin editing user - UserId: {UserId}", id);

            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Kullanýcý bulunamadý.";
                return RedirectToPage("/Users/Index");
            }

            Input = new UserEditViewModel
            {
                UserId = user.UserId,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                IsActive = user.IsActive,
                IsActivated = user.IsActivated
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _logger.LogInformation("Admin saving user changes - UserId: {UserId}", Input.UserId);

            var user = await _userRepository.GetByIdAsync(Input.UserId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Kullanýcý bulunamadý.";
                return RedirectToPage("/Users/Index");
            }

            // Email deðiþikliði kontrolü
            if (user.Email != Input.Email)
            {
                var existingUser = await _userRepository.GetByEmailAsync(Input.Email);
                if (existingUser != null && existingUser.UserId != Input.UserId)
                {
                    ModelState.AddModelError("Input.Email", "Bu email adresi baþka bir kullanýcý tarafýndan kullanýlýyor.");
                    return Page();
                }
            }

            // Güncelleme
            user.Email = Input.Email;
            user.PhoneNumber = string.IsNullOrWhiteSpace(Input.PhoneNumber) ? null : Input.PhoneNumber;
            user.IsActive = Input.IsActive;
            user.IsActivated = Input.IsActivated;

            await _userRepository.UpdateAsync(user);

            TempData["SuccessMessage"] = "Kullanýcý baþarýyla güncellendi.";
            return RedirectToPage("/Users/Detail", new { id = Input.UserId });
        }

        public class UserEditViewModel
        {
            public int UserId { get; set; }

            [Required(ErrorMessage = "Email adresi zorunludur.")]
            [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz.")]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Phone(ErrorMessage = "Geçerli bir telefon numarasý giriniz.")]
            [Display(Name = "Telefon")]
            public string PhoneNumber { get; set; } = string.Empty;

            [Display(Name = "Aktif")]
            public bool IsActive { get; set; }

            [Display(Name = "Email Doðrulanmýþ")]
            public bool IsActivated { get; set; }
        }
    }
}