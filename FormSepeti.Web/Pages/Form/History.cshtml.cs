using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Web.Pages.Form
{
    public class HistoryModel : PageModel
    {
        private readonly IFormSubmissionRepository _submissionRepo;
        private readonly IFormService _formService;

        public HistoryModel(IFormSubmissionRepository submissionRepo, IFormService formService)
        {
            _submissionRepo = submissionRepo;
            _formService = formService;
        }

        [BindProperty(SupportsGet = true)] public int FormId { get; set; }
        public string FormTitle { get; private set; } = "Form";
        public List<(int Index, string Date, int Row, string Status)> Entries { get; private set; } = new();
        public int GroupId { get; set; } // or string, depending on your usage

        public async Task<IActionResult> OnGetAsync(int id)
        {
            FormId = id;
            var form = await _formService.GetFormByIdAsync(id);
            if (form == null) return NotFound();
            FormTitle = form.FormName;

            var submissions = await _submissionRepo.GetByUserAndFormIdAsync(0, FormId); // use real userId if needed
            Entries = new List<(int, string, int, string)>();
            var idx = 1;
            foreach (var s in submissions)
            {
                Entries.Add((idx++, s.SubmittedDate.ToString("yyyy-MM-dd HH:mm"), s.GoogleSheetRowNumber ?? 0, s.Status ?? "Bilinmiyor"));
            }

            return Page();
        }
    }
}
