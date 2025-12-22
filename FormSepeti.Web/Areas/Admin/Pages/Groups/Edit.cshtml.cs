using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace FormSepeti.Web.Areas.Admin.Pages.Groups
{
    [Authorize(Policy = "AdminOnly")]
    public class EditModel : PageModel
    {
        private readonly IFormGroupRepository _formGroupRepository;
        private readonly IFormGroupMappingRepository _formGroupMappingRepository;
        private readonly IPackageRepository _packageRepository;
        private readonly IFormRepository _formRepository;
        private readonly ILogger<EditModel> _logger;

        public EditModel(
            IFormGroupRepository formGroupRepository,
            IFormGroupMappingRepository formGroupMappingRepository,
            IPackageRepository packageRepository,
            IFormRepository formRepository,
            ILogger<EditModel> logger)
        {
            _formGroupRepository = formGroupRepository;
            _formGroupMappingRepository = formGroupMappingRepository;
            _packageRepository = packageRepository;
            _formRepository = formRepository;
            _logger = logger;
        }

        [BindProperty]
        public GroupEditViewModel Input { get; set; } = new();

        public int FormCount { get; set; }
        public int PackageCount { get; set; }
        public List<FormMappingViewModel> AssignedForms { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            _logger.LogInformation("Admin editing group - GroupId: {GroupId}", id);

            var group = await _formGroupRepository.GetByIdAsync(id);
            if (group == null)
            {
                TempData["ErrorMessage"] = "Grup bulunamadý.";
                return RedirectToPage("/Groups/Index");
            }

            Input = new GroupEditViewModel
            {
                GroupId = group.GroupId,
                GroupName = group.GroupName,
                Description = group.Description ?? string.Empty,
                IconUrl = group.IconUrl ?? string.Empty,
                SortOrder = group.SortOrder,
                IsActive = group.IsActive
            };

            // Form ve paket sayýlarýný al
            await LoadGroupStatsAsync(id);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadGroupStatsAsync(Input.GroupId);
                return Page();
            }

            _logger.LogInformation("Admin saving group changes - GroupId: {GroupId}", Input.GroupId);

            var group = await _formGroupRepository.GetByIdAsync(Input.GroupId);
            if (group == null)
            {
                TempData["ErrorMessage"] = "Grup bulunamadý.";
                return RedirectToPage("/Groups/Index");
            }

            // Güncelleme
            group.GroupName = Input.GroupName;
            group.Description = string.IsNullOrWhiteSpace(Input.Description) ? string.Empty : Input.Description;
            group.IconUrl = string.IsNullOrWhiteSpace(Input.IconUrl) ? string.Empty : Input.IconUrl;
            group.SortOrder = Input.SortOrder;
            group.IsActive = Input.IsActive;

            await _formGroupRepository.UpdateAsync(group);

            TempData["SuccessMessage"] = "Grup baþarýyla güncellendi.";
            return RedirectToPage("/Groups/Index");
        }

        private async Task LoadGroupStatsAsync(int groupId)
        {
            var mappings = await _formGroupMappingRepository.GetByGroupIdAsync(groupId);
            var packages = await _packageRepository.GetByGroupIdAsync(groupId);

            FormCount = mappings.Count;
            PackageCount = packages.Count;

            AssignedForms = new List<FormMappingViewModel>();
            foreach (var mapping in mappings.OrderBy(m => m.SortOrder))
            {
                var form = await _formRepository.GetByIdAsync(mapping.FormId);
                if (form != null)
                {
                    AssignedForms.Add(new FormMappingViewModel
                    {
                        FormId = form.FormId,
                        FormName = form.FormName,
                        IsFreeInGroup = mapping.IsFreeInGroup,
                        RequiresPackage = mapping.RequiresPackage,
                        SortOrder = mapping.SortOrder
                    });
                }
            }
        }

        public class FormMappingViewModel
        {
            public int FormId { get; set; }
            public string FormName { get; set; } = string.Empty;
            public bool IsFreeInGroup { get; set; }
            public bool RequiresPackage { get; set; }
            public int SortOrder { get; set; }
        }

        public class GroupEditViewModel
        {
            public int GroupId { get; set; }

            [Required(ErrorMessage = "Grup adý zorunludur.")]
            [StringLength(100, ErrorMessage = "Grup adý en fazla 100 karakter olabilir.")]
            [Display(Name = "Grup Adý")]
            public string GroupName { get; set; } = string.Empty;

            [StringLength(500, ErrorMessage = "Açýklama en fazla 500 karakter olabilir.")]
            [Display(Name = "Grup Açýklamasý")]
            public string Description { get; set; } = string.Empty;

            [Display(Name = "Ýkon CSS Class")]
            public string IconUrl { get; set; } = string.Empty;

            [Required(ErrorMessage = "Sýra numarasý zorunludur.")]
            [Range(1, 999, ErrorMessage = "Sýra numarasý 1 ile 999 arasýnda olmalýdýr.")]
            [Display(Name = "Sýra Numarasý")]
            public int SortOrder { get; set; }

            [Display(Name = "Aktif")]
            public bool IsActive { get; set; }
        }
    }
}