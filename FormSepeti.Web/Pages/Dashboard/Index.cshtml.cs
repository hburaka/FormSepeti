using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Data.Entities;

namespace FormSepeti.Web.Pages.Dashboard
{
    public class IndexModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly IPackageService _packageService;
        private readonly IUserGoogleSheetsRepository _sheetsRepository;
        private readonly IFormSubmissionRepository _submissionRepository;
        private readonly IUserPackageRepository _userPackageRepository;
        private readonly IGoogleSheetsService _googleSheetsService; // ? EKLENDI

        public IndexModel(
            IUserService userService,
            IPackageService packageService,
            IUserGoogleSheetsRepository sheetsRepository,
            IFormSubmissionRepository submissionRepository,
            IUserPackageRepository userPackageRepository,
            IGoogleSheetsService googleSheetsService) // ? EKLENDI
        {
            _userService = userService;
            _packageService = packageService;
            _sheetsRepository = sheetsRepository;
            _submissionRepository = submissionRepository;
            _userPackageRepository = userPackageRepository;
            _googleSheetsService = googleSheetsService; // ? EKLENDI
        }

        public string UserName { get; private set; } = "";
        public bool IsGoogleConnected { get; private set; }
        public int TotalSubmissions { get; private set; }
        public string? SpreadsheetUrl { get; set; } // ? NULLABLE YAPILDI
        
        // ? Aktif paketler
        public List<UserPackage> ActivePackages { get; private set; } = new();
        public bool HasAnyPackage => ActivePackages?.Any() ?? false;

        // ? Ýstatistikler
        public int ActivePackageCount { get; private set; }
        public int ExpiringSoonCount { get; private set; }
        public string NextExpiryDate { get; private set; } = "Yok";

        private int GetUserId()
        {
            var v = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(v, out var id) ? id : 0;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return RedirectToPage("/Account/Login");
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }

            UserName = user.Email ?? user.PhoneNumber ?? "Kullanýcý";
            IsGoogleConnected = !string.IsNullOrEmpty(user.GoogleRefreshToken);

            // ? Aktif paketleri getir
            ActivePackages = await _userPackageRepository.GetActiveByUserIdAsync(userId);
            ActivePackageCount = ActivePackages.Count;

            // ? Her paket için Google Sheets URL'lerini al
            foreach (var package in ActivePackages)
            {
                var sheet = await _sheetsRepository.GetByUserAndGroupAsync(userId, package.GroupId);
                
                if (sheet == null && IsGoogleConnected)
                {
                    var sheetUrl = await _googleSheetsService.CreateSpreadsheetForUserGroup(
                        userId, 
                        package.GroupId, 
                        package.FormGroup.GroupName
                    );
                    
                    if (!string.IsNullOrEmpty(sheetUrl))
                    {
                        package.FormGroup.Description = sheetUrl;
                    }
                }
                else if (sheet != null)
                {
                    package.FormGroup.Description = sheet.SpreadsheetUrl;
                }
            }

            // ? Ýstatistikler
            ExpiringSoonCount = ActivePackages.Count(p => 
                p.ExpiryDate.HasValue && 
                p.ExpiryDate.Value <= System.DateTime.UtcNow.AddDays(7) &&
                p.ExpiryDate.Value > System.DateTime.UtcNow);

            var nextExpiry = ActivePackages
                .Where(p => p.ExpiryDate.HasValue)
                .OrderBy(p => p.ExpiryDate)
                .FirstOrDefault();

            if (nextExpiry?.ExpiryDate != null) // ? NULL CHECK EKLENDÝ
            {
                NextExpiryDate = nextExpiry.ExpiryDate.Value.ToString("dd.MM.yyyy");
            }

            TotalSubmissions = await _submissionRepository.GetCountByUserIdAsync(userId);

            return Page();
        }
    }
}
