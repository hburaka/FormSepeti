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

namespace FormSepeti.Web.Areas.Admin.Pages.Forms
{
    [Authorize(Policy = "AdminOnly")]
    public class EditModel : PageModel
    {
        private readonly IFormRepository _formRepository;
        private readonly IFormGroupMappingRepository _formGroupMappingRepository;
        private readonly IFormGroupRepository _formGroupRepository; // ✅ Eklendi
        private readonly ILogger<EditModel> _logger;

        public EditModel(
            IFormRepository formRepository,
            IFormGroupMappingRepository formGroupMappingRepository,
            IFormGroupRepository formGroupRepository, // ✅ Eklendi
            ILogger<EditModel> logger)
        {
            _formRepository = formRepository;
            _formGroupMappingRepository = formGroupMappingRepository;
            _formGroupRepository = formGroupRepository; // ✅ Eklendi
            _logger = logger;
        }

        [BindProperty]
        public FormEditViewModel Input { get; set; } = new();

        public int GroupCount { get; set; }
        public List<GroupMappingViewModel> AssignedGroups { get; set; } = new(); // ✅ Eklendi

        public async Task<IActionResult> OnGetAsync(int id)
        {
            _logger.LogInformation("Admin editing form - FormId: {FormId}", id);

            var form = await _formRepository.GetByIdAsync(id);
            if (form == null)
            {
                TempData["ErrorMessage"] = "Form bulunamadı.";
                return RedirectToPage("/Forms/Index");
            }

            Input = new FormEditViewModel
            {
                FormId = form.FormId,
                FormName = form.FormName,
                FormDescription = form.FormDescription ?? string.Empty,
                JotFormId = form.JotFormId,
                JotFormEmbedCode = form.JotFormEmbedCode ?? string.Empty,
                GoogleSheetName = form.GoogleSheetName ?? string.Empty,
                IsActive = form.IsActive
            };

            // ✅ Grup eşleştirmelerini al
            await LoadAssignedGroupsAsync(id);
            GroupCount = AssignedGroups.Count;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                // ✅ Hata durumunda grupları yeniden yükle
                await LoadAssignedGroupsAsync(Input.FormId);
                GroupCount = AssignedGroups.Count;
                return Page();
            }

            _logger.LogInformation("Admin saving form changes - FormId: {FormId}", Input.FormId);

            var form = await _formRepository.GetByIdAsync(Input.FormId);
            if (form == null)
            {
                TempData["ErrorMessage"] = "Form bulunamadı.";
                return RedirectToPage("/Forms/Index");
            }

            // JotForm ID değişikliği kontrolü
            if (form.JotFormId != Input.JotFormId)
            {
                var existingForm = await _formRepository.GetByJotFormIdAsync(Input.JotFormId);
                if (existingForm != null && existingForm.FormId != Input.FormId)
                {
                    ModelState.AddModelError("Input.JotFormId", "Bu JotForm ID başka bir form tarafından kullanılıyor.");
                    
                    // ✅ Hata durumunda grupları yeniden yükle
                    await LoadAssignedGroupsAsync(Input.FormId);
                    GroupCount = AssignedGroups.Count;
                    
                    return Page();
                }
            }

            // Güncelleme
            form.FormName = Input.FormName;
            form.FormDescription = string.IsNullOrWhiteSpace(Input.FormDescription) ? null : Input.FormDescription;
            form.JotFormId = Input.JotFormId;
            form.JotFormEmbedCode = string.IsNullOrWhiteSpace(Input.JotFormEmbedCode) ? string.Empty : Input.JotFormEmbedCode;
            form.GoogleSheetName = string.IsNullOrWhiteSpace(Input.GoogleSheetName) ? null : Input.GoogleSheetName;
            form.IsActive = Input.IsActive;

            await _formRepository.UpdateAsync(form);

            TempData["SuccessMessage"] = "Form başarıyla güncellendi.";
            return RedirectToPage("/Forms/Index");
        }

        // ✅ YENİ METOT: Atanmış grupları yükle
        private async Task LoadAssignedGroupsAsync(int formId)
        {
            var mappings = await _formGroupMappingRepository.GetByFormIdAsync(formId);
            AssignedGroups = new List<GroupMappingViewModel>();

            foreach (var mapping in mappings.OrderBy(m => m.SortOrder))
            {
                var group = await _formGroupRepository.GetByIdAsync(mapping.GroupId);
                if (group != null)
                {
                    AssignedGroups.Add(new GroupMappingViewModel
                    {
                        GroupId = group.GroupId,
                        GroupName = group.GroupName,
                        IsFreeInGroup = mapping.IsFreeInGroup,
                        RequiresPackage = mapping.RequiresPackage,
                        SortOrder = mapping.SortOrder
                    });
                }
            }
        }

        // ✅ YENİ VIEW MODEL
        public class GroupMappingViewModel
        {
            public int GroupId { get; set; }
            public string GroupName { get; set; } = string.Empty;
            public bool IsFreeInGroup { get; set; }
            public bool RequiresPackage { get; set; }
            public int SortOrder { get; set; }
        }

        public class FormEditViewModel
        {
            public int FormId { get; set; }

            [Required(ErrorMessage = "Form adı zorunludur.")]
            [StringLength(200, ErrorMessage = "Form adı en fazla 200 karakter olabilir.")]
            [Display(Name = "Form Adı")]
            public string FormName { get; set; } = string.Empty;

            [StringLength(1000, ErrorMessage = "Açıklama en fazla 1000 karakter olabilir.")]
            [Display(Name = "Form Açıklaması")]
            public string FormDescription { get; set; } = string.Empty;

            [Required(ErrorMessage = "JotForm ID zorunludur.")]
            [StringLength(50, ErrorMessage = "JotForm ID en fazla 50 karakter olabilir.")]
            [Display(Name = "JotForm ID")]
            public string JotFormId { get; set; } = string.Empty;

            [Display(Name = "JotForm Embed Kodu")]
            public string JotFormEmbedCode { get; set; } = string.Empty;

            [StringLength(200, ErrorMessage = "Google Sheet adı en fazla 200 karakter olabilir.")]
            [Display(Name = "Google Sheet Adı")]
            public string GoogleSheetName { get; set; } = string.Empty;

            [Display(Name = "Aktif")]
            public bool IsActive { get; set; }
        }
    }
}