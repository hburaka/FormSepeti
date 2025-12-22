using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace FormSepeti.Web.Areas.Admin.Pages.Mappings
{
    [Authorize(Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly IFormGroupMappingRepository _mappingRepository;
        private readonly IFormRepository _formRepository;
        private readonly IFormGroupRepository _groupRepository;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IFormGroupMappingRepository mappingRepository,
            IFormRepository formRepository,
            IFormGroupRepository groupRepository,
            ILogger<IndexModel> logger)
        {
            _mappingRepository = mappingRepository;
            _formRepository = formRepository;
            _groupRepository = groupRepository;
            _logger = logger;
        }

        public List<MappingViewModel> Mappings { get; set; } = new();
        public List<SelectListItem> AvailableForms { get; set; } = new();
        public List<SelectListItem> AvailableGroups { get; set; } = new();

        public int TotalMappings { get; set; }
        public int FreeMappings { get; set; }
        public int PackageRequiredMappings { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FilterGroupId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FilterFormId { get; set; }

        [BindProperty]
        public MappingCreateViewModel NewMapping { get; set; } = new();

        public async Task OnGetAsync()
        {
            _logger.LogInformation("Admin viewing form-group mappings");

            await LoadDataAsync();
        }

        public async Task<IActionResult> OnPostCreateMappingAsync()
        {
            _logger.LogInformation("Admin creating new mapping - FormId: {FormId}, GroupId: {GroupId}",
                NewMapping.FormId, NewMapping.GroupId);

            // Ayný form-group eþleþmesi var mý kontrol et
            var existingMapping = await _mappingRepository.GetByFormAndGroupAsync(
                NewMapping.FormId, NewMapping.GroupId);

            if (existingMapping != null)
            {
                TempData["ErrorMessage"] = "Bu form zaten bu gruba atanmýþ.";
                await LoadDataAsync();
                return Page();
            }

            var mapping = new FormGroupMapping
            {
                FormId = NewMapping.FormId,
                GroupId = NewMapping.GroupId,
                IsFreeInGroup = NewMapping.IsFreeInGroup,
                RequiresPackage = NewMapping.RequiresPackage,
                SortOrder = NewMapping.SortOrder
            };

            await _mappingRepository.CreateAsync(mapping);

            TempData["SuccessMessage"] = "Form gruba baþarýyla atandý.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteMappingAsync(int mappingId)
        {
            _logger.LogInformation("Admin deleting mapping - MappingId: {MappingId}", mappingId);

            var result = await _mappingRepository.DeleteAsync(mappingId);

            if (result)
            {
                TempData["SuccessMessage"] = "Eþleþtirme baþarýyla silindi.";
            }
            else
            {
                TempData["ErrorMessage"] = "Eþleþtirme silinemedi.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateMappingAsync(
            int mappingId,
            bool isFree,
            bool requiresPackage,
            int sortOrder)
        {
            _logger.LogInformation("Admin updating mapping - MappingId: {MappingId}", mappingId);

            var mappings = await _mappingRepository.GetByGroupIdAsync(0); // Tüm mappings
            var mapping = mappings.FirstOrDefault(m => m.MappingId == mappingId);

            if (mapping == null)
            {
                // Alternatif: Tüm formlarý kontrol et
                var allForms = await _formRepository.GetAllAsync();
                foreach (var form in allForms)
                {
                    var formMappings = await _mappingRepository.GetByFormIdAsync(form.FormId);
                    mapping = formMappings.FirstOrDefault(m => m.MappingId == mappingId);
                    if (mapping != null) break;
                }
            }

            if (mapping == null)
            {
                TempData["ErrorMessage"] = "Eþleþtirme bulunamadý.";
                return RedirectToPage();
            }

            mapping.IsFreeInGroup = isFree;
            mapping.RequiresPackage = requiresPackage;
            mapping.SortOrder = sortOrder;

            await _mappingRepository.UpdateAsync(mapping);

            TempData["SuccessMessage"] = "Eþleþtirme baþarýyla güncellendi.";
            return RedirectToPage();
        }

        private async Task LoadDataAsync()
        {
            // Tüm formlarý ve gruplarý yükle
            var allForms = await _formRepository.GetAllAsync();
            var allGroups = await _groupRepository.GetAllAsync();

            // Dropdown listeleri oluþtur
            AvailableForms = allForms
                .OrderBy(f => f.FormName)
                .Select(f => new SelectListItem
                {
                    Value = f.FormId.ToString(),
                    Text = f.FormName
                }).ToList();

            AvailableGroups = allGroups
                .OrderBy(g => g.SortOrder)
                .Select(g => new SelectListItem
                {
                    Value = g.GroupId.ToString(),
                    Text = g.GroupName
                }).ToList();

            // Mappings'leri yükle
            Mappings = new List<MappingViewModel>();

            if (FilterGroupId.HasValue)
            {
                var groupMappings = await _mappingRepository.GetByGroupIdAsync(FilterGroupId.Value);
                foreach (var mapping in groupMappings.OrderBy(m => m.SortOrder))
                {
                    var form = await _formRepository.GetByIdAsync(mapping.FormId);
                    var group = await _groupRepository.GetByIdAsync(mapping.GroupId);

                    if (form != null && group != null)
                    {
                        Mappings.Add(CreateViewModel(mapping, form, group));
                    }
                }
            }
            else if (FilterFormId.HasValue)
            {
                var formMappings = await _mappingRepository.GetByFormIdAsync(FilterFormId.Value);
                foreach (var mapping in formMappings.OrderBy(m => m.SortOrder))
                {
                    var form = await _formRepository.GetByIdAsync(mapping.FormId);
                    var group = await _groupRepository.GetByIdAsync(mapping.GroupId);

                    if (form != null && group != null)
                    {
                        Mappings.Add(CreateViewModel(mapping, form, group));
                    }
                }
            }
            else
            {
                // Tüm mappings'leri yükle
                foreach (var group in allGroups)
                {
                    var groupMappings = await _mappingRepository.GetByGroupIdAsync(group.GroupId);
                    foreach (var mapping in groupMappings.OrderBy(m => m.SortOrder))
                    {
                        var form = await _formRepository.GetByIdAsync(mapping.FormId);
                        if (form != null)
                        {
                            Mappings.Add(CreateViewModel(mapping, form, group));
                        }
                    }
                }
            }

            // Ýstatistikler
            TotalMappings = Mappings.Count;
            FreeMappings = Mappings.Count(m => m.IsFreeInGroup);
            PackageRequiredMappings = Mappings.Count(m => m.RequiresPackage);
        }

        private MappingViewModel CreateViewModel(FormGroupMapping mapping, Form form, FormGroup group)
        {
            return new MappingViewModel
            {
                MappingId = mapping.MappingId,
                FormId = mapping.FormId,
                FormName = form.FormName,
                GroupId = mapping.GroupId,
                GroupName = group.GroupName,
                IsFreeInGroup = mapping.IsFreeInGroup,
                RequiresPackage = mapping.RequiresPackage,
                SortOrder = mapping.SortOrder
            };
        }

        public class MappingViewModel
        {
            public int MappingId { get; set; }
            public int FormId { get; set; }
            public string FormName { get; set; } = string.Empty;
            public int GroupId { get; set; }
            public string GroupName { get; set; } = string.Empty;
            public bool IsFreeInGroup { get; set; }
            public bool RequiresPackage { get; set; }
            public int SortOrder { get; set; }
        }

        public class MappingCreateViewModel
        {
            public int FormId { get; set; }
            public int GroupId { get; set; }
            public bool IsFreeInGroup { get; set; }
            public bool RequiresPackage { get; set; }
            public int SortOrder { get; set; } = 1;
        }
    }
}