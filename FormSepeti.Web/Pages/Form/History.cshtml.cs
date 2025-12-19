using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Web.Pages.Form
{
    public class HistoryModel : PageModel
    {
        private readonly IFormSubmissionRepository _submissionRepo;
        private readonly IFormService _formService;
        private readonly IFormGroupRepository _groupRepo;
        private readonly IGoogleSheetsService _googleSheetsService;

        public HistoryModel(
            IFormSubmissionRepository submissionRepo, 
            IFormService formService,
            IFormGroupRepository groupRepo,
            IGoogleSheetsService googleSheetsService)
        {
            _submissionRepo = submissionRepo;
            _formService = formService;
            _groupRepo = groupRepo;
            _googleSheetsService = googleSheetsService;
        }

        [BindProperty(SupportsGet = true)] public int FormId { get; set; }
        public string FormTitle { get; private set; } = "Form";
        public string FormSheetName { get; private set; } = "";
        public string SpreadsheetUrl { get; private set; } = "";
        public int GroupId { get; set; }
        public List<SubmissionEntry> Entries { get; private set; } = new();

        public class SubmissionEntry
        {
            public int Index { get; set; }
            public string SubmissionId { get; set; }
            public DateTime SubmittedDate { get; set; }
            public int RowNumber { get; set; }
            public string Status { get; set; }
            public string StatusColor { get; set; }
            public string GoogleSheetRowUrl { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            FormId = id;
            
            // ✅ UserId'yi doğru şekilde al
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("UserId")?.Value ?? "0");
            
            var form = await _formService.GetFormByIdAsync(id);
            if (form == null) return NotFound();
            
            FormTitle = form.FormName;
            FormSheetName = form.GoogleSheetName ?? form.FormName;
            GroupId = await _formService.GetFormGroupIdAsync(id);

            // ✅ Google Sheets URL'ini al
            var userSheet = await _googleSheetsService.GetUserGoogleSheetAsync(userId, GroupId);
            if (userSheet != null)
            {
                SpreadsheetUrl = userSheet.SpreadsheetUrl;
            }

            // ✅ Doğru userId ile submissions al
            var submissions = await _submissionRepo.GetByUserAndFormIdAsync(userId, FormId);
            Entries = new List<SubmissionEntry>();
            var idx = 1;
            
            foreach (var s in submissions)
            {
                // ✅ Durum rengini belirle
                var statusColor = "secondary";
                var statusText = s.Status ?? "Bilinmiyor";
                
                if (statusText.Contains("Başarılı", StringComparison.OrdinalIgnoreCase) || 
                    statusText.Contains("Success", StringComparison.OrdinalIgnoreCase))
                {
                    statusColor = "success";
                }
                else if (statusText.Contains("Başarısız", StringComparison.OrdinalIgnoreCase) || 
                         statusText.Contains("Failed", StringComparison.OrdinalIgnoreCase))
                {
                    statusColor = "danger";
                }
                else if (statusText.Contains("Pending", StringComparison.OrdinalIgnoreCase))
                {
                    statusColor = "warning";
                }

                // ✅ Google Sheets satır URL'ini oluştur
                var rowUrl = "";
                if (!string.IsNullOrEmpty(SpreadsheetUrl) && s.GoogleSheetRowNumber.HasValue && s.GoogleSheetRowNumber > 0)
                {
                    // Google Sheets'te belirli bir satırı açmak için gid ve range parametresi
                    // Örnek: https://docs.google.com/spreadsheets/d/SPREADSHEET_ID/edit#gid=SHEET_ID&range=A5
                    var spreadsheetId = SpreadsheetUrl.Split("/d/")[1].Split("/")[0];
                    rowUrl = $"https://docs.google.com/spreadsheets/d/{spreadsheetId}/edit#gid=0&range=A{s.GoogleSheetRowNumber}";
                }

                Entries.Add(new SubmissionEntry
                {
                    Index = idx++,
                    SubmissionId = s.JotFormSubmissionId ?? "N/A",
                    SubmittedDate = s.SubmittedDate,
                    RowNumber = s.GoogleSheetRowNumber ?? 0,
                    Status = statusText,
                    StatusColor = statusColor,
                    GoogleSheetRowUrl = rowUrl
                });
            }

            return Page();
        }
    }
}
