using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace FormSepeti.Web.Areas.Admin.Pages.Forms
{
    [Authorize(Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly IFormRepository _formRepository;
        private readonly IFormGroupMappingRepository _formGroupMappingRepository;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IFormRepository formRepository,
            IFormGroupMappingRepository formGroupMappingRepository,
            ILogger<IndexModel> logger)
        {
            _formRepository = formRepository;
            _formGroupMappingRepository = formGroupMappingRepository;
            _logger = logger;
        }

        public List<FormViewModel> Forms { get; set; } = new();
        public int TotalForms { get; set; }
        public int ActiveForms { get; set; }
        public int InactiveForms { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; } // "all", "active", "inactive"

        public async Task OnGetAsync()
        {
            _logger.LogInformation("Admin viewing forms list - SearchTerm: {SearchTerm}, StatusFilter: {StatusFilter}",
                SearchTerm, StatusFilter);

            // Tüm formlarý al
            var allForms = await _formRepository.GetAllAsync();

            // Filtreleme
            var filteredForms = allForms.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                filteredForms = filteredForms.Where(f =>
                    f.FormName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (f.FormDescription != null && f.FormDescription.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    f.JotFormId.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));
            }

            if (StatusFilter == "active")
            {
                filteredForms = filteredForms.Where(f => f.IsActive);
            }
            else if (StatusFilter == "inactive")
            {
                filteredForms = filteredForms.Where(f => !f.IsActive);
            }

            // Her form için grup sayýsýný al
            Forms = new List<FormViewModel>();
            foreach (var form in filteredForms.OrderByDescending(f => f.CreatedDate))
            {
                var mappings = await _formGroupMappingRepository.GetByFormIdAsync(form.FormId);

                Forms.Add(new FormViewModel
                {
                    FormId = form.FormId,
                    FormName = form.FormName,
                    FormDescription = form.FormDescription ?? "-",
                    JotFormId = form.JotFormId,
                    GoogleSheetName = form.GoogleSheetName ?? "-",
                    IsActive = form.IsActive,
                    CreatedDate = form.CreatedDate,
                    GroupCount = mappings.Count
                });
            }

            // Ýstatistikler
            TotalForms = allForms.Count;
            ActiveForms = allForms.Count(f => f.IsActive);
            InactiveForms = TotalForms - ActiveForms;
        }

        public async Task<IActionResult> OnPostToggleStatusAsync(int formId)
        {
            _logger.LogInformation("Admin toggling form status - FormId: {FormId}", formId);

            var form = await _formRepository.GetByIdAsync(formId);
            if (form == null)
            {
                TempData["ErrorMessage"] = "Form bulunamadý.";
                return RedirectToPage();
            }

            form.IsActive = !form.IsActive;
            await _formRepository.UpdateAsync(form);

            TempData["SuccessMessage"] = $"Form {(form.IsActive ? "aktif" : "pasif")} edildi.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int formId)
        {
            _logger.LogInformation("Admin deleting form - FormId: {FormId}", formId);

            var form = await _formRepository.GetByIdAsync(formId);
            if (form == null)
            {
                TempData["ErrorMessage"] = "Form bulunamadý.";
                return RedirectToPage();
            }

            // Grup eþleþtirmelerini kontrol et
            var mappings = await _formGroupMappingRepository.GetByFormIdAsync(form.FormId);
            if (mappings.Any())
            {
                TempData["ErrorMessage"] = $"Bu form {mappings.Count} gruba atanmýþ. Önce grup eþleþtirmelerini kaldýrýn.";
                return RedirectToPage();
            }

            await _formRepository.DeleteAsync(formId);

            TempData["SuccessMessage"] = "Form baþarýyla silindi.";
            return RedirectToPage();
        }

        public class FormViewModel
        {
            public int FormId { get; set; }
            public string FormName { get; set; } = string.Empty;
            public string FormDescription { get; set; } = string.Empty;
            public string JotFormId { get; set; } = string.Empty;
            public string GoogleSheetName { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public DateTime CreatedDate { get; set; }
            public int GroupCount { get; set; }
        }
    }
}