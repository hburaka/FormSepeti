using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace FormSepeti.Web.Areas.Admin.Pages.Packages
{
    [Authorize(Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly IPackageRepository _packageRepository;
        private readonly IFormGroupRepository _formGroupRepository;
        private readonly IUserPackageRepository _userPackageRepository;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IPackageRepository packageRepository,
            IFormGroupRepository formGroupRepository,
            IUserPackageRepository userPackageRepository,
            ILogger<IndexModel> logger)
        {
            _packageRepository = packageRepository;
            _formGroupRepository = formGroupRepository;
            _userPackageRepository = userPackageRepository;
            _logger = logger;
        }

        public List<PackageViewModel> Packages { get; set; } = new();
        public int TotalPackages { get; set; }
        public int ActivePackages { get; set; }
        public int InactivePackages { get; set; }
        public decimal TotalRevenue { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public async Task OnGetAsync()
        {
            _logger.LogInformation("Admin viewing packages list - SearchTerm: {SearchTerm}, StatusFilter: {StatusFilter}",
                SearchTerm, StatusFilter);

            var allPackages = await _packageRepository.GetAllAsync();
            var filteredPackages = allPackages.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                filteredPackages = filteredPackages.Where(p =>
                    p.PackageName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (p.Description != null && p.Description.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)));
            }

            if (StatusFilter == "active")
            {
                filteredPackages = filteredPackages.Where(p => p.IsActive);
            }
            else if (StatusFilter == "inactive")
            {
                filteredPackages = filteredPackages.Where(p => !p.IsActive);
            }

            Packages = new List<PackageViewModel>();
            foreach (var package in filteredPackages.OrderByDescending(p => p.CreatedDate))
            {
                var group = await _formGroupRepository.GetByIdAsync(package.GroupId);
                var userPackages = await _userPackageRepository.GetByPackageIdAsync(package.PackageId);

                Packages.Add(new PackageViewModel
                {
                    PackageId = package.PackageId,
                    PackageName = package.PackageName,
                    Description = package.Description ?? "-",
                    GroupName = group?.GroupName ?? "Grup Bulunamadý",
                    Price = package.Price,
                    DurationDays = package.DurationDays,
                    IsActive = package.IsActive,
                    CreatedDate = package.CreatedDate,
                    PurchaseCount = userPackages.Count,
                    ActivePurchaseCount = userPackages.Count(up => up.IsActive)
                });
            }

            TotalPackages = allPackages.Count;
            ActivePackages = allPackages.Count(p => p.IsActive);
            InactivePackages = TotalPackages - ActivePackages;

            // Toplam gelir hesaplama
            var allUserPackages = await _userPackageRepository.GetAllAsync();
            TotalRevenue = allUserPackages.Sum(up => up.PaymentAmount);
        }

        public async Task<IActionResult> OnPostToggleStatusAsync(int packageId)
        {
            _logger.LogInformation("Admin toggling package status - PackageId: {PackageId}", packageId);

            var package = await _packageRepository.GetByIdAsync(packageId);
            if (package == null)
            {
                TempData["ErrorMessage"] = "Paket bulunamadý.";
                return RedirectToPage();
            }

            package.IsActive = !package.IsActive;
            await _packageRepository.UpdateAsync(package);

            TempData["SuccessMessage"] = $"Paket {(package.IsActive ? "aktif" : "pasif")} edildi.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int packageId)
        {
            _logger.LogInformation("Admin deleting package - PackageId: {PackageId}", packageId);

            var package = await _packageRepository.GetByIdAsync(packageId);
            if (package == null)
            {
                TempData["ErrorMessage"] = "Paket bulunamadý.";
                return RedirectToPage();
            }

            var userPackages = await _userPackageRepository.GetByPackageIdAsync(packageId);
            if (userPackages.Any())
            {
                TempData["ErrorMessage"] = $"Bu paket {userPackages.Count} kullanýcý tarafýndan satýn alýnmýþ. Silinemez.";
                return RedirectToPage();
            }

            await _packageRepository.DeleteAsync(packageId);

            TempData["SuccessMessage"] = "Paket baþarýyla silindi.";
            return RedirectToPage();
        }

        public class PackageViewModel
        {
            public int PackageId { get; set; }
            public string PackageName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string GroupName { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int? DurationDays { get; set; }
            public bool IsActive { get; set; }
            public DateTime CreatedDate { get; set; }
            public int PurchaseCount { get; set; }
            public int ActivePurchaseCount { get; set; }
        }
    }
}