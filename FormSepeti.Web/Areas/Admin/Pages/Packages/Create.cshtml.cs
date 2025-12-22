using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace FormSepeti.Web.Areas.Admin.Pages.Packages
{
    [Authorize(Policy = "AdminOnly")]
    public class CreateModel : PageModel
    {
        private readonly IPackageRepository _packageRepository;
        private readonly IFormGroupRepository _formGroupRepository;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(
            IPackageRepository packageRepository,
            IFormGroupRepository formGroupRepository,
            ILogger<CreateModel> logger)
        {
            _packageRepository = packageRepository;
            _formGroupRepository = formGroupRepository;
            _logger = logger;
        }

        [BindProperty]
        public PackageCreateViewModel Input { get; set; } = new();

        public List<SelectListItem> Groups { get; set; } = new();

        public async Task OnGetAsync()
        {
            _logger.LogInformation("Admin creating new package");
            await LoadGroupsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadGroupsAsync();
                return Page();
            }

            _logger.LogInformation("Admin saving new package - PackageName: {PackageName}", Input.PackageName);

            // Yeni paket oluþtur
            var package = new Package
            {
                GroupId = Input.GroupId,
                PackageName = Input.PackageName,
                Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description,
                Price = Input.Price,
                DurationDays = Input.DurationDays > 0 ? Input.DurationDays : null,
                IsActive = Input.IsActive,
                CreatedDate = DateTime.UtcNow
            };

            await _packageRepository.CreateAsync(package);

            TempData["SuccessMessage"] = "Paket baþarýyla oluþturuldu.";
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

        public class PackageCreateViewModel
        {
            [Required(ErrorMessage = "Grup seçimi zorunludur.")]
            [Display(Name = "Grup")]
            public int GroupId { get; set; }

            [Required(ErrorMessage = "Paket adý zorunludur.")]
            [StringLength(100, ErrorMessage = "Paket adý en fazla 100 karakter olabilir.")]
            [Display(Name = "Paket Adý")]
            public string PackageName { get; set; } = string.Empty;

            [StringLength(500, ErrorMessage = "Açýklama en fazla 500 karakter olabilir.")]
            [Display(Name = "Paket Açýklamasý")]
            public string? Description { get; set; }

            [Required(ErrorMessage = "Fiyat zorunludur.")]
            [Range(0.01, 99999.99, ErrorMessage = "Fiyat 0.01 ile 99999.99 arasýnda olmalýdýr.")]
            [Display(Name = "Fiyat (TL)")]
            public decimal Price { get; set; }

            [Range(0, 3650, ErrorMessage = "Süre 0 ile 3650 gün arasýnda olmalýdýr.")]
            [Display(Name = "Süre (Gün)")]
            public int DurationDays { get; set; }

            [Display(Name = "Aktif")]
            public bool IsActive { get; set; } = true;
        }
    }
}