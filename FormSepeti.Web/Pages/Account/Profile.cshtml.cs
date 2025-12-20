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
        private readonly IFormSubmissionRepository _submissionRepository;
        private readonly IUserPackageRepository _packageRepository;

        public ProfileModel(
            IUserService userService,
            IFormSubmissionRepository submissionRepository,
            IUserPackageRepository packageRepository)
        {
            _userService = userService;
            _submissionRepository = submissionRepository;
            _packageRepository = packageRepository;
        }

        public string? Email { get; private set; }
        public string? Phone { get; private set; }
        public string? ProfilePhotoUrl { get; private set; }
        public string CreatedDate { get; private set; } = "-";
        public string LastLoginDate { get; private set; } = "-";
        
        // ✅ İKİ FARKLI DURUM
        public bool IsGoogleLogin { get; private set; }
        public bool IsGoogleSheetsConnected { get; private set; }
        
        public int TotalSubmissions { get; private set; }
        public int ActivePackages { get; private set; }

        [BindProperty]
        public string? NewPhone { get; set; }

        [BindProperty]
        public string FirstName { get; set; }

        [BindProperty]
        public string LastName { get; set; }

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
            FirstName = user.FirstName;
            LastName = user.LastName;
            ProfilePhotoUrl = user.ProfilePhotoUrl;
            CreatedDate = user.CreatedDate.ToString("dd MMMM yyyy");
            LastLoginDate = user.LastLoginDate?.ToString("dd MMMM yyyy HH:mm") ?? "Hiç giriş yapılmadı";
            
            IsGoogleLogin = !string.IsNullOrEmpty(user.GoogleId);
            IsGoogleSheetsConnected = await _userService.IsGoogleSheetsConnectedAsync(user.GoogleId);

            TotalSubmissions = await _submissionRepository.GetCountByUserIdAsync(id);
            var activePackages = await _packageRepository.GetActiveByUserIdAsync(id);
            ActivePackages = activePackages.Count;

            // ✅ YENİ: Eğer logout mesajı varsa, sadece login sayfasında gösterilmeli
            // Profile sayfasında gereksiz mesajları temizle
            if (TempData.ContainsKey("Success") && TempData["Success"]?.ToString() == "Başarıyla çıkış yaptınız.")
            {
                TempData.Remove("Success");
            }

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
                return RedirectToPage();
            }
            else
            {
                ErrorMessage = "Telefon numarası güncellenemedi.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostUpdateProfileAsync()
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

            user.FirstName = FirstName;
            user.LastName = LastName;
            
            await _userService.UpdateUserAsync(user);
            
            TempData["Success"] = "Profil bilgileriniz güncellendi";
            return RedirectToPage();
        }
    }
}