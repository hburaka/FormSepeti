using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace FormSepeti.Web.Areas.Admin.Pages.Users
{
    [Authorize(Policy = "AdminOnly")]
    public class DetailModel : PageModel
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserPackageRepository _userPackageRepository;
        private readonly IUserGoogleSheetsRepository _userGoogleSheetsRepository;
        private readonly IFormSubmissionRepository _formSubmissionRepository;
        private readonly IPackageRepository _packageRepository;
        private readonly IFormGroupRepository _formGroupRepository;
        private readonly ILogger<DetailModel> _logger;

        public DetailModel(
            IUserRepository userRepository,
            IUserPackageRepository userPackageRepository,
            IUserGoogleSheetsRepository userGoogleSheetsRepository,
            IFormSubmissionRepository formSubmissionRepository,
            IPackageRepository packageRepository,
            IFormGroupRepository formGroupRepository,
            ILogger<DetailModel> logger)
        {
            _userRepository = userRepository;
            _userPackageRepository = userPackageRepository;
            _userGoogleSheetsRepository = userGoogleSheetsRepository;
            _formSubmissionRepository = formSubmissionRepository;
            _packageRepository = packageRepository;
            _formGroupRepository = formGroupRepository;
            _logger = logger;
        }

        public User? User { get; set; }
        public List<UserPackageViewModel> ActivePackages { get; set; } = new();
        public List<UserPackageViewModel> ExpiredPackages { get; set; } = new();
        public List<GoogleSheetViewModel> GoogleSheets { get; set; } = new();
        public UserStats Stats { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            _logger.LogInformation("Admin viewing user detail - UserId: {UserId}", id);

            User = await _userRepository.GetByIdAsync(id);
            if (User == null)
            {
                TempData["ErrorMessage"] = "Kullanýcý bulunamadý.";
                return RedirectToPage("/Users/Index");
            }

            // Paketleri al
            var allPackages = await _userPackageRepository.GetByUserIdAsync(id);

            foreach (var userPackage in allPackages)
            {
                var package = await _packageRepository.GetByIdAsync(userPackage.PackageId);
                var group = await _formGroupRepository.GetByIdAsync(userPackage.GroupId);

                var viewModel = new UserPackageViewModel
                {
                    UserPackageId = userPackage.UserPackageId,
                    PackageName = package?.PackageName ?? "Bilinmeyen Paket",
                    GroupName = group?.GroupName ?? "Bilinmeyen Grup",
                    PurchaseDate = userPackage.PurchaseDate,
                    ActivationDate = userPackage.ActivationDate,
                    ExpiryDate = userPackage.ExpiryDate,
                    IsActive = userPackage.IsActive,
                    PaymentAmount = userPackage.PaymentAmount
                };

                if (userPackage.IsActive)
                {
                    ActivePackages.Add(viewModel);
                }
                else
                {
                    ExpiredPackages.Add(viewModel);
                }
            }

            // Google Sheets baðlantýlarýný al
            var sheets = await _userGoogleSheetsRepository.GetByUserIdAsync(id);
            foreach (var sheet in sheets)
            {
                var group = await _formGroupRepository.GetByIdAsync(sheet.GroupId);
                GoogleSheets.Add(new GoogleSheetViewModel
                {
                    SheetId = sheet.SheetId,
                    GroupName = group?.GroupName ?? "Bilinmeyen Grup",
                    SpreadsheetUrl = sheet.SpreadsheetUrl,
                    SheetName = sheet.SheetName,
                    CreatedDate = sheet.CreatedDate,
                    LastUpdatedDate = sheet.LastUpdatedDate
                });
            }

            // Ýstatistikler
            var submissions = await _formSubmissionRepository.GetByUserIdAsync(id);
            Stats = new UserStats
            {
                TotalSubmissions = submissions.Count,
                SuccessfulSubmissions = submissions.Count(s => s.Status == "Success" || s.Status.Contains("Baþarýlý")),
                FailedSubmissions = submissions.Count(s => s.Status == "Failed" || s.Status.Contains("Baþarýsýz")),
                TotalPackagesPurchased = allPackages.Count,
                ActivePackagesCount = ActivePackages.Count,
                ConnectedSheetsCount = GoogleSheets.Count
            };

            return Page();
        }

        public async Task<IActionResult> OnPostToggleStatusAsync(int id)
        {
            _logger.LogInformation("Admin toggling user status - UserId: {UserId}", id);

            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Kullanýcý bulunamadý.";
                return RedirectToPage("/Users/Index");
            }

            user.IsActive = !user.IsActive;
            await _userRepository.UpdateAsync(user);

            TempData["SuccessMessage"] = $"Kullanýcý {(user.IsActive ? "aktif" : "pasif")} edildi.";
            return RedirectToPage(new { id });
        }

        public class UserPackageViewModel
        {
            public int UserPackageId { get; set; }
            public string PackageName { get; set; } = string.Empty;
            public string GroupName { get; set; } = string.Empty;
            public DateTime PurchaseDate { get; set; }
            public DateTime? ActivationDate { get; set; }
            public DateTime? ExpiryDate { get; set; }
            public bool IsActive { get; set; }
            public decimal PaymentAmount { get; set; }
        }

        public class GoogleSheetViewModel
        {
            public int SheetId { get; set; }
            public string GroupName { get; set; } = string.Empty;
            public string SpreadsheetUrl { get; set; } = string.Empty;
            public string SheetName { get; set; } = string.Empty;
            public DateTime CreatedDate { get; set; }
            public DateTime? LastUpdatedDate { get; set; }
        }

        public class UserStats
        {
            public int TotalSubmissions { get; set; }
            public int SuccessfulSubmissions { get; set; }
            public int FailedSubmissions { get; set; }
            public int TotalPackagesPurchased { get; set; }
            public int ActivePackagesCount { get; set; }
            public int ConnectedSheetsCount { get; set; }
        }
    }
}