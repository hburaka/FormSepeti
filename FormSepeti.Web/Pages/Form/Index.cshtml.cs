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

        public IndexModel(
            IFormService formService, 
            IFormGroupRepository groupRepo,
            IFormSubmissionRepository submissionRepo)
        {
            _formService = formService;
            _groupRepo = groupRepo;
            _submissionRepo = submissionRepo;
        }

        [BindProperty(SupportsGet = true)] public int? GroupId { get; set; }
        public string GroupName { get; private set; } = "Grup";
        public List<(int Id, string Title, string Description, bool IsPaid, int SubmissionCount)> Forms { get; private set; } = new();
        public string SpreadsheetUrl { get; private set; } = "";

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

            var forms = await _formService.GetFormsByGroupIdAsync(userId, GroupId.Value);
            Forms = new List<(int, string, string, bool, int)>();
            
            foreach (var f in forms)
            {
                // ? Her form için gönderim sayýsýný al
                var submissions = await _submissionRepo.GetByUserAndFormIdAsync(userId, f.Form.FormId);
                var submissionCount = submissions.Count;
                
                Forms.Add((
                    f.Form.FormId, 
                    f.Form.FormName, 
                    f.Form.FormDescription ?? "", 
                    f.Form.IsActive,
                    submissionCount
                ));
            }

            return Page();
        }
    }
}
