using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace FormSepeti.Web.Areas.Admin.Pages.Forms
{
    [Authorize(Policy = "AdminOnly")]
    public class CreateModel : PageModel
    {
        private readonly IFormRepository _formRepository;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(
            IFormRepository formRepository,
            ILogger<CreateModel> logger)
        {
            _formRepository = formRepository;
            _logger = logger;
        }

        [BindProperty]
        public FormCreateViewModel Input { get; set; } = new();

        public void OnGet()
        {
            _logger.LogInformation("Admin creating new form");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _logger.LogInformation("Admin saving new form - FormName: {FormName}", Input.FormName);

            // JotForm ID benzersizlik kontrolü
            var existingForm = await _formRepository.GetByJotFormIdAsync(Input.JotFormId);
            if (existingForm != null)
            {
                ModelState.AddModelError("Input.JotFormId", "Bu JotForm ID zaten kullanýlýyor.");
                return Page();
            }

            // Yeni form oluþtur
            var form = new Form
            {
                FormName = Input.FormName,
                FormDescription = string.IsNullOrWhiteSpace(Input.FormDescription) ? null : Input.FormDescription,
                JotFormId = Input.JotFormId,
                JotFormEmbedCode = string.IsNullOrWhiteSpace(Input.JotFormEmbedCode) ? string.Empty : Input.JotFormEmbedCode,
                GoogleSheetName = string.IsNullOrWhiteSpace(Input.GoogleSheetName) ? null : Input.GoogleSheetName,
                IsActive = Input.IsActive,
                CreatedDate = DateTime.UtcNow
            };

            await _formRepository.CreateAsync(form);

            TempData["SuccessMessage"] = "Form baþarýyla oluþturuldu.";
            return RedirectToPage("/Forms/Index");
        }

        public class FormCreateViewModel
        {
            [Required(ErrorMessage = "Form adý zorunludur.")]
            [StringLength(200, ErrorMessage = "Form adý en fazla 200 karakter olabilir.")]
            [Display(Name = "Form Adý")]
            public string FormName { get; set; } = string.Empty;

            [StringLength(1000, ErrorMessage = "Açýklama en fazla 1000 karakter olabilir.")]
            [Display(Name = "Form Açýklamasý")]
            public string? FormDescription { get; set; }

            [Required(ErrorMessage = "JotForm ID zorunludur.")]
            [StringLength(50, ErrorMessage = "JotForm ID en fazla 50 karakter olabilir.")]
            [Display(Name = "JotForm ID")]
            public string JotFormId { get; set; } = string.Empty;

            [Display(Name = "JotForm Embed Kodu")]
            public string? JotFormEmbedCode { get; set; }

            [StringLength(200, ErrorMessage = "Google Sheet adý en fazla 200 karakter olabilir.")]
            [Display(Name = "Google Sheet Adý")]
            public string? GoogleSheetName { get; set; }

            [Display(Name = "Aktif")]
            public bool IsActive { get; set; } = true;
        }
    }
}