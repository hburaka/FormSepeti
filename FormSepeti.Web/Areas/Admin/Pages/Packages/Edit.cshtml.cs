using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace FormSepeti.Web.Areas.Admin.Pages.Packages
{
    [Authorize(Policy = "AdminOnly")]
    public class EditModel : PageModel
    {
        private readonly IPackageRepository _packageRepository;
        private readonly IFormGroupRepository _formGroupRepository;
        private readonly IUserPackageRepository _userPackageRepository;
        private readonly ILogger<EditModel> _logger;

        public EditModel(
            IPackageRepository packageRepository,
            IFormGroupRepository formGroupRepository,
            IUserPackageRepository userPackageRepository,
            ILogger<EditModel> logger)
        {
            _packageRepository = packageRepository;
            _formGroupRepository = formGroupRepository;
            _userPackageRepository = userPackageRepository;
            _logger = logger;
        }

        [BindProperty]
        public PackageEditViewModel Input { get; set; } = new();

        public List<SelectListItem> Groups { get; set; } = new();
        public int PurchaseCount { get; set; }
        public int ActivePurchaseCount { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            _logger.LogInformation("Admin editing package - PackageId: {PackageId}", id);

            var package = await _packageRepository.GetByIdAsync(id);
            if (package == null)
            {
                TempData["ErrorMessage"] = "Paket bulunamadý.";
                return RedirectToPage("/Packages/Index");
            }

            Input = new PackageEditViewModel
            {
                PackageId = package.PackageId,
                GroupId = package.GroupId,
                PackageName = package.PackageName,
                Description = package.Description ?? string.Empty,
                Price = package.Price,
                DurationDays = package.DurationDays ?? 0,
                IsActive = package.IsActive
            };

            await LoadGroupsAsync();

            // Satýþ istatistikleri
            var userPackages = await _userPackageRepository.GetByPackageIdAsync(id);
            PurchaseCount = userPackages.Count;
            ActivePurchaseCount = userPackages.Count(up => up.IsActive);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadGroupsAsync();

                // Ýstatistikleri yeniden yükle
                var userPackages = await _userPackageRepository.GetByPackageIdAsync(Input.PackageId);
                PurchaseCount = userPackages.Count;
                ActivePurchaseCount = userPackages.Count(up => up.IsActive);

                return Page();
            }

            _logger.LogInformation("Admin saving package changes - PackageId: {PackageId}", Input.PackageId);

            var package = await _packageRepository.GetByIdAsync(Input.PackageId);
            if (package == null)
            {
                TempData["ErrorMessage"] = "Paket bulunamadý.";
                return RedirectToPage("/Packages/Index");
            }

            // Güncelleme
            package.GroupId = Input.GroupId;
            package.PackageName = Input.PackageName;
            package.Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description;
            package.Price = Input.Price;
            package.DurationDays = Input.DurationDays > 0 ? Input.DurationDays : null;
            package.IsActive = Input.IsActive;

            await _packageRepository.UpdateAsync(package);

            TempData["SuccessMessage"] = "Paket baþarýyla güncellendi.";
            return RedirectToPage("/Packages/Index");
        }

        private async Task LoadGroupsAsync()
        {
            var groups = await _formGroupRepository.GetAllAsync();
            Groups = groups.Select(g => new SelectListItem
            {
                Value = g.GroupId.ToString(),
                Text = g.GroupName
            }).ToList();
        }

        public class PackageEditViewModel
        {
            public int PackageId { get; set; }

            [Required(ErrorMessage = "Grup seçimi zorunludur.")]
            [Display(Name = "Grup")]
            public int GroupId { get; set; }

            [Required(ErrorMessage = "Paket adý zorunludur.")]
            [StringLength(100, ErrorMessage = "Paket adý en fazla 100 karakter olabilir.")]
            [Display(Name = "Paket Adý")]
            public string PackageName { get; set; } = string.Empty;

            [StringLength(500, ErrorMessage = "Açýklama en fazla 500 karakter olabilir.")]
            [Display(Name = "Paket Açýklamasý")]
            public string Description { get; set; } = string.Empty;

            [Required(ErrorMessage = "Fiyat zorunludur.")]
            [Range(0.01, 99999.99, ErrorMessage = "Fiyat 0.01 ile 99999.99 arasýnda olmalýdýr.")]
            [Display(Name = "Fiyat (TL)")]
            public decimal Price { get; set; }

            [Range(0, 3650, ErrorMessage = "Süre 0 ile 3650 gün arasýnda olmalýdýr.")]
            [Display(Name = "Süre (Gün)")]
            public int DurationDays { get; set; }

            [Display(Name = "Aktif")]
            public bool IsActive { get; set; }
        }
    }
}