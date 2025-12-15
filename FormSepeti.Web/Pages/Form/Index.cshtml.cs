using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Web.Pages.Form
{
    public class IndexModel : PageModel
    {
        private readonly IFormService _formService;
        private readonly IFormGroupRepository _groupRepo;

        public IndexModel(IFormService formService, IFormGroupRepository groupRepo)
        {
            _formService = formService;
            _groupRepo = groupRepo;
        }

        [BindProperty(SupportsGet = true)] public int? GroupId { get; set; }
        public string GroupName { get; private set; } = "Grup";
        public List<(int Id, string Title, string Description, bool IsPaid)> Forms { get; private set; } = new();

        // Added property to satisfy Razor references
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

            var forms = await _formService.GetFormsByGroupIdAsync(0, GroupId.Value); // userId not required here, use 0 or pass real userId
            Forms = new List<(int, string, string, bool)>();
            foreach (var f in forms)
            {
                Forms.Add((f.Form.FormId, f.Form.FormName, f.Form.FormDescription ?? "", f.Form.IsActive));
            }

            // Optionally set SpreadsheetUrl if you can resolve it here
            // SpreadsheetUrl = GetSpreadsheetUrlForGroup(GroupId.Value);

            return Page();
        }
    }
}
