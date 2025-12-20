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
using Microsoft.Extensions.Logging;

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
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IUserService userService,
            IPackageService packageService,
            IUserGoogleSheetsRepository sheetsRepository,
            IFormSubmissionRepository submissionRepository,
            IUserPackageRepository userPackageRepository,
            IGoogleSheetsService googleSheetsService,
            IFormGroupRepository formGroupRepository,
            ILogger<IndexModel> logger)
        {
            _userService = userService;
            _packageService = packageService;
            _sheetsRepository = sheetsRepository;
            _submissionRepository = submissionRepository;
            _userPackageRepository = userPackageRepository;
            _googleSheetsService = googleSheetsService;
            _formGroupRepository = formGroupRepository;
            _logger = logger;
        }

        public string UserName { get; private set; } = "";
        public string UserFullName { get; set; }
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
            
            // ✅ YENİ: Token kontrol ve yenileme
            IsGoogleConnected = await CheckAndRefreshTokenAsync(user);

            // Form verilerini doldur
            UserFullName = !string.IsNullOrWhiteSpace(user.FullName) 
                ? user.FullName 
                : user.Email;

            // Aktif paketleri getir
            var userPackages = await _userPackageRepository.GetActiveByUserIdAsync(userId);
            ActivePackageCount = userPackages.Count;

            var activeGroupIds = new HashSet<int>();

            foreach (var up in userPackages)
            {
                activeGroupIds.Add(up.GroupId);
                
                var sheet = await _sheetsRepository.GetByUserAndGroupAsync(userId, up.GroupId);
                
                if (sheet == null && IsGoogleConnected)
                {
                    await _googleSheetsService.CreateSpreadsheetForUserGroup(
                        userId, 
                        up.GroupId, 
                        up.FormGroup.GroupName
                    );
                    sheet = await _sheetsRepository.GetByUserAndGroupAsync(userId, up.GroupId);
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

            // Ücretsiz formları olan grupları getir
            var freeGroups = await _formGroupRepository.GetGroupsWithFreeFormsAsync();

            foreach (var group in freeGroups)
            {
                if (activeGroupIds.Contains(group.GroupId))
                    continue;

                var sheet = await _sheetsRepository.GetByUserAndGroupAsync(userId, group.GroupId);

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

        // ✅ YENİ: Token kontrol ve yenileme metodu
        private async Task<bool> CheckAndRefreshTokenAsync(User user)
        {
            if (string.IsNullOrEmpty(user.GoogleRefreshToken))
            {
                _logger.LogInformation($"No refresh token for UserId={user.UserId}");
                return false;
            }

            // Token geçerli mi?
            if (user.GoogleTokenExpiry.HasValue && user.GoogleTokenExpiry.Value > DateTime.UtcNow)
            {
                _logger.LogInformation($"Token valid for UserId={user.UserId}");
                return true;
            }

            // Token dolmuş, yenile
            _logger.LogInformation($"Dashboard: Refreshing expired token for UserId={user.UserId}");
            
            try
            {
                var refreshed = await _googleSheetsService.RefreshAccessToken(user.UserId);
                
                if (!refreshed)
                {
                    TempData["Warning"] = "Google Sheets bağlantınızın süresi dolmuş. Lütfen yeniden bağlanın.";
                    _logger.LogWarning($"Token refresh failed for UserId={user.UserId}");
                    return false;
                }

                TempData["Info"] = "Google Sheets bağlantınız otomatik olarak yenilendi.";
                _logger.LogInformation($"Token refreshed successfully for UserId={user.UserId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error refreshing token for UserId={user.UserId}");
                TempData["Error"] = "Google Sheets bağlantınız yenilenirken bir hata oluştu.";
                return false;
            }
        }
    }
}
