using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
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
        private readonly IGoogleSheetsService _googleSheetsService;
        private readonly IFormGroupRepository _formGroupRepository;

        public IndexModel(
            IUserService userService,
            IPackageService packageService,
            IUserGoogleSheetsRepository sheetsRepository,
            IFormSubmissionRepository submissionRepository,
            IUserPackageRepository userPackageRepository,
            IGoogleSheetsService googleSheetsService,
            IFormGroupRepository formGroupRepository)
        {
            _userService = userService;
            _packageService = packageService;
            _sheetsRepository = sheetsRepository;
            _submissionRepository = submissionRepository;
            _userPackageRepository = userPackageRepository;
            _googleSheetsService = googleSheetsService;
            _formGroupRepository = formGroupRepository;
        }

        public string UserName { get; private set; } = "";
        public bool IsGoogleConnected { get; private set; }
        public int TotalSubmissions { get; private set; }
        
        // ? Aktif paketler
        public List<GroupPackageInfo> ActivePackages { get; private set; } = new();
        public bool HasAnyPackage => ActivePackages?.Any() ?? false;

        // ? Diğer erişilebilir gruplar (ücretsiz formlar)
        public List<GroupPackageInfo> OtherAccessibleGroups { get; private set; } = new();
        public bool HasOtherGroups => OtherAccessibleGroups?.Any() ?? false;

        // İstatistikler
        public int ActivePackageCount { get; private set; }
        public int ExpiringSoonCount { get; private set; }
        public string NextExpiryDate { get; private set; } = "Yok";

        public class GroupPackageInfo
        {
            public int GroupId { get; set; }
            public string GroupName { get; set; }
            public string PackageName { get; set; }
            public DateTime? PurchaseDate { get; set; }
            public DateTime? ExpiryDate { get; set; }
            public bool IsActive { get; set; }
            public string SpreadsheetUrl { get; set; }
            public bool IsFreeAccess { get; set; } // ? Paket olmadan erişim
        }

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

            UserName = user.Email ?? user.PhoneNumber ?? "Kullanıcı";
            
            // ✅ DÜZELT: userId ile çağır (email yerine)
            IsGoogleConnected = await _userService.IsGoogleSheetsConnectedAsync(user.GoogleId);

            // ✅ 1. Aktif paketleri getir
            var userPackages = await _userPackageRepository.GetActiveByUserIdAsync(userId);
            ActivePackageCount = userPackages.Count;

            var activeGroupIds = new HashSet<int>();

            foreach (var up in userPackages)
            {
                activeGroupIds.Add(up.GroupId); // ✅ Grup ID'sini ekle
                
                var sheet = await _sheetsRepository.GetByUserAndGroupAsync(userId, up.GroupId);
                
                if (sheet == null && IsGoogleConnected)
                {
                    // Sheet oluştur
                    await _googleSheetsService.CreateSpreadsheetForUserGroup(
                        userId, 
                        up.GroupId, 
                        up.FormGroup.GroupName
                    );
                }

                ActivePackages.Add(new GroupPackageInfo
                {
                    GroupId = up.GroupId,
                    GroupName = up.FormGroup.GroupName,
                    PackageName = up.Package.PackageName,
                    PurchaseDate = up.PurchaseDate,
                    ExpiryDate = up.ExpiryDate,
                    IsActive = up.IsActive,
                    SpreadsheetUrl = sheet?.SpreadsheetUrl ?? "",
                    IsFreeAccess = false
                });
            }

            // ✅ 2. Ücretsiz formu olan grupları getir
            var freeGroups = await _formGroupRepository.GetGroupsWithFreeFormsAsync();

            foreach (var group in freeGroups)
            {
                // Zaten aktif pakete sahipse atla
                if (activeGroupIds.Contains(group.GroupId))
                    continue;

                var sheet = await _sheetsRepository.GetByUserAndGroupAsync(userId, group.GroupId);

                // ? Eğer bu gruba daha önce form gönderildiyse spreadsheet olabilir
                OtherAccessibleGroups.Add(new GroupPackageInfo
                {
                    GroupId = group.GroupId,
                    GroupName = group.GroupName,
                    PackageName = "Ücretsiz Formlar",
                    PurchaseDate = null,
                    ExpiryDate = null,
                    IsActive = true,
                    SpreadsheetUrl = sheet?.SpreadsheetUrl ?? "",
                    IsFreeAccess = true
                });
            }

            // İstatistikler
            ExpiringSoonCount = userPackages.Count(p => 
                p.ExpiryDate.HasValue && 
                p.ExpiryDate.Value <= DateTime.UtcNow.AddDays(7) &&
                p.ExpiryDate.Value > DateTime.UtcNow);

            var nextExpiry = userPackages
                .Where(p => p.ExpiryDate.HasValue)
                .OrderBy(p => p.ExpiryDate)
                .FirstOrDefault();

            if (nextExpiry?.ExpiryDate != null)
            {
                NextExpiryDate = nextExpiry.ExpiryDate.Value.ToString("dd.MM.yyyy");
            }

            TotalSubmissions = await _submissionRepository.GetCountByUserIdAsync(userId);

            return Page();
        }
    }
}
