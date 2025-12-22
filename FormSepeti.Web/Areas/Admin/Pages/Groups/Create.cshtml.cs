using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace FormSepeti.Web.Areas.Admin.Pages.Groups
{
    [Authorize(Policy = "AdminOnly")]
    public class CreateModel : PageModel
    {
        private readonly IFormGroupRepository _formGroupRepository;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(
            IFormGroupRepository formGroupRepository,
            ILogger<CreateModel> logger)
        {
            _formGroupRepository = formGroupRepository;
            _logger = logger;
        }

        [BindProperty]
        public GroupCreateViewModel Input { get; set; } = new();

        public void OnGet()
        {
            _logger.LogInformation("Admin creating new group");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _logger.LogInformation("Admin saving new group - GroupName: {GroupName}", Input.GroupName);

            // Yeni grup oluþtur
            var group = new FormGroup
            {
                GroupName = Input.GroupName,
                Description = string.IsNullOrWhiteSpace(Input.Description) ? string.Empty : Input.Description,
                IconUrl = string.IsNullOrWhiteSpace(Input.IconUrl) ? string.Empty : Input.IconUrl,
                SortOrder = Input.SortOrder,
                IsActive = Input.IsActive,
                CreatedDate = DateTime.UtcNow
            };

            await _formGroupRepository.CreateAsync(group);

            TempData["SuccessMessage"] = "Grup baþarýyla oluþturuldu.";
            return RedirectToPage("/Groups/Index");
        }

        public class GroupCreateViewModel
        {
            [Required(ErrorMessage = "Grup adý zorunludur.")]
            [StringLength(100, ErrorMessage = "Grup adý en fazla 100 karakter olabilir.")]
            [Display(Name = "Grup Adý")]
            public string GroupName { get; set; } = string.Empty;

            [StringLength(500, ErrorMessage = "Açýklama en fazla 500 karakter olabilir.")]
            [Display(Name = "Grup Açýklamasý")]
            public string? Description { get; set; }

            [Display(Name = "Ýkon CSS Class")]
            public string? IconUrl { get; set; }

            [Required(ErrorMessage = "Sýra numarasý zorunludur.")]
            [Range(1, 999, ErrorMessage = "Sýra numarasý 1 ile 999 arasýnda olmalýdýr.")]
            [Display(Name = "Sýra Numarasý")]
            public int SortOrder { get; set; } = 1;

            [Display(Name = "Aktif")]
            public bool IsActive { get; set; } = true;
        }
    }
}