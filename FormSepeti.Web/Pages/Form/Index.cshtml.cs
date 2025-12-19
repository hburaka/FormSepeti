using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Web.Pages.Form
{
    public class IndexModel : PageModel
    {
        private readonly IFormService _formService;
        private readonly IFormGroupRepository _groupRepo;
        private readonly IFormSubmissionRepository _submissionRepo;
        private readonly IPackageService _packageService;

        public IndexModel(
            IFormService formService, 
            IFormGroupRepository groupRepo,
            IFormSubmissionRepository submissionRepo,
            IPackageService packageService)
        {
            _formService = formService;
            _groupRepo = groupRepo;
            _submissionRepo = submissionRepo;
            _packageService = packageService;
        }

        [BindProperty(SupportsGet = true)] public int? GroupId { get; set; }
        public string GroupName { get; private set; } = "Grup";
        public List<FormItemViewModel> Forms { get; private set; } = new();
        public string SpreadsheetUrl { get; private set; } = "";

        // ? Form item view model
        public class FormItemViewModel
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public bool IsPaid { get; set; }
            public int SubmissionCount { get; set; }
            public bool HasAccess { get; set; }  // ? Eriþim kontrolü
            public bool IsFree { get; set; }      // ? Ücretsiz mi?
        }

        public async Task<IActionResult> OnGetAsync(int? groupId)
        {
            GroupId = groupId ?? GroupId;
            if (!GroupId.HasValue)
            {
                TempData["Warning"] = "Grup belirtilmedi.";
                return RedirectToPage("/Dashboard/SelectGroup");
            }

            var group = await _groupRepo.GetByIdAsync(GroupId.Value);
            if (group == null) return NotFound();

            GroupName = group.GroupName;

            // ? UserId'yi al
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("UserId")?.Value ?? "0");

            var formsWithAccess = await _formService.GetFormsByGroupIdAsync(userId, GroupId.Value);
            Forms = new List<FormItemViewModel>();
            
            foreach (var f in formsWithAccess)
            {
                // ? Her form için gönderim sayýsýný al
                var submissions = await _submissionRepo.GetByUserAndFormIdAsync(userId, f.Form.FormId);
                var submissionCount = submissions.Count;
                
                Forms.Add(new FormItemViewModel
                {
                    Id = f.Form.FormId,
                    Title = f.Form.FormName,
                    Description = f.Form.FormDescription ?? "",
                    IsPaid = !f.IsFree,  // ? Ücretsizse IsPaid=false
                    SubmissionCount = submissionCount,
                    HasAccess = f.HasAccess,  // ? Eriþim var mý?
                    IsFree = f.IsFree          // ? Ücretsiz mi?
                });
            }

            return Page();
        }
    }
}
