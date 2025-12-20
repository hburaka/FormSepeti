using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
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
        public string? Phone { get; private set; } // ✅ Formatlı gösterim için
        public string? ProfilePhotoUrl { get; private set; }
        public string CreatedDate { get; private set; } = "-";
        public string LastLoginDate { get; private set; } = "-";
        
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
            // ✅ Telefonu formatlı göster
            Phone = FormatPhoneForDisplay(user.PhoneNumber);
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

            // ✅ Telefonu temizle ve kaydet
            user.PhoneNumber = string.IsNullOrWhiteSpace(NewPhone) 
                ? null 
                : CleanPhoneNumber(NewPhone);
            
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

        // ✅ Helper metodlar
        private string FormatPhoneForDisplay(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;
            
            var cleaned = new string(phone.Where(char.IsDigit).ToArray());
            
            if (cleaned.Length == 12 && cleaned.StartsWith("90"))
                cleaned = cleaned.Substring(2);
            else if (cleaned.Length == 11 && cleaned.StartsWith("0"))
                cleaned = cleaned.Substring(1);
            
            if (cleaned.Length == 10)
            {
                return $"+90 ({cleaned.Substring(0, 3)}) {cleaned.Substring(3, 3)} {cleaned.Substring(6, 2)} {cleaned.Substring(8, 2)}";
            }
            
            return phone;
        }

        private string CleanPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;
            
            var cleaned = new string(phone.Where(char.IsDigit).ToArray());
            
            if (cleaned.StartsWith("0") && cleaned.Length == 11)
                cleaned = cleaned.Substring(1);
            
            if (cleaned.StartsWith("90") && cleaned.Length == 12)
                cleaned = cleaned.Substring(2);
            
            return cleaned.Length == 10 ? cleaned : phone;
        }
    }
}