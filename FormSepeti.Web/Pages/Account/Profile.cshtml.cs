using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Web.Pages.Account
{
    public class ProfileModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly IFormSubmissionRepository _submissionRepository; // ✅ EKLE
        private readonly IUserPackageRepository _packageRepository; // ✅ EKLE

        public ProfileModel(
            IUserService userService,
            IFormSubmissionRepository submissionRepository, // ✅ EKLE
            IUserPackageRepository packageRepository) // ✅ EKLE
        {
            _userService = userService;
            _submissionRepository = submissionRepository; // ✅ EKLE
            _packageRepository = packageRepository; // ✅ EKLE
        }

        public string? Email { get; private set; }
        public string? Phone { get; private set; }
        public string CreatedDate { get; private set; } = "-";
        public string LastLoginDate { get; private set; } = "-";
        public bool IsGoogleConnected { get; private set; }
        public int TotalSubmissions { get; private set; }
        public int ActivePackages { get; private set; }

        [BindProperty]
        public string? NewPhone { get; set; }

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        private int GetUserId()
        {
            var v = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(v, out var id) ? id : 0;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var id = GetUserId();
            if (id == 0)
            {
                return RedirectToPage("/Account/Login");
            }

            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }

            Email = user.Email;
            Phone = user.PhoneNumber;
            CreatedDate = user.CreatedDate.ToString("dd MMMM yyyy");
            LastLoginDate = user.LastLoginDate?.ToString("dd MMMM yyyy HH:mm") ?? "Hiç giriş yapılmadı";
            IsGoogleConnected = !string.IsNullOrEmpty(user.GoogleRefreshToken);

            // ✅ GERÇEK İSTATİSTİKLER
            TotalSubmissions = await _submissionRepository.GetCountByUserIdAsync(id);
            
            var activePackages = await _packageRepository.GetActiveByUserIdAsync(id);
            ActivePackages = activePackages.Count;

            return Page();
        }

        public async Task<IActionResult> OnPostUpdatePhoneAsync()
        {
            var id = GetUserId();
            if (id == 0)
            {
                return RedirectToPage("/Account/Login");
            }

            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                ErrorMessage = "Kullanıcı bulunamadı.";
                return Page();
            }

            user.PhoneNumber = NewPhone?.Trim();
            var updated = await _userService.UpdateUserAsync(user);

            if (updated)
            {
                TempData["Success"] = "Telefon numaranız güncellendi!";
                return RedirectToPage(); // ✅ Redirect ile TempData kullan
            }
            else
            {
                ErrorMessage = "Telefon numarası güncellenemedi.";
                return Page();
            }
        }
    }
}