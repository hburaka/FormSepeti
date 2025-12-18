using FormSepeti.Services.Implementations;
using FormSepeti.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FormSepeti.Web.Pages.Form
{
    public class ViewModel : PageModel
    {
        private readonly IFormService _formService;
        private readonly IGoogleSheetsService _googleSheetsService;
        private readonly ILogger<ViewModel> _logger;

        public ViewModel(
            IFormService formService, 
            IGoogleSheetsService googleSheetsService,
            ILogger<ViewModel> logger)
        {
            _formService = formService;
            _googleSheetsService = googleSheetsService;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)] public int FormId { get; set; }
        [BindProperty(SupportsGet = true)] public int? GroupId { get; set; }
        
        // ? YENÝ PROPERTY'LER
        public int UserId { get; set; }
        public string JotFormId { get; set; }
        
        public string FormTitle { get; private set; } = "Form";
        public string WebhookUrl { get; private set; } = string.Empty;
        public string SpreadsheetUrl { get; private set; } = string.Empty;

        public string JotFormEmbedHtml { get; private set; } = string.Empty;
        public string JotFormJsUrl { get; private set; } = string.Empty;
        public string JotFormIFrameSrc { get; private set; } = "about:blank";
        public string JotFormIFrameId { get; private set; } = $"JotFormIFrame-{Guid.NewGuid():N}";
        public string JotFormEmbedHandlerUrl { get; private set; } = string.Empty;
        public string JotFormBaseUrl { get; private set; } = string.Empty;

        private int GetCurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        public async Task<IActionResult> OnGetAsync(int id, int? groupId = null)
        {
            FormId = id;

            // ? UserId'yi al
            UserId = GetCurrentUserId();
            if (UserId == 0)
            {
                _logger.LogWarning("User not authenticated, redirecting to login");
                return RedirectToPage("/Account/Login");
            }

            // 1. URL'den groupId gelmiþ mi kontrol et
            // 2. Yoksa session'dan al
            // 3. O da yoksa varsayýlan 1 kullan
            var sessionGroupId = HttpContext.Session.GetInt32("ActiveGroupId");
            GroupId = groupId ?? sessionGroupId ?? 1;

            _logger.LogInformation($"FormId={FormId}, URL GroupId={groupId}, Session GroupId={sessionGroupId}, Final GroupId={GroupId}");

            var form = await _formService.GetFormByIdAsync(id);
            if (form == null)
            {
                _logger.LogWarning($"Form not found: FormId={id}");
                return NotFound();
            }

            FormTitle = form.FormName;
            JotFormId = form.JotFormId; // ? JotFormId'yi set et

            var actualGroupId = GroupId.Value;
            
            // ? Generic webhook URL (artýk userId/formId/groupId path'te yok)
            var secret = "9oq8r838ihaq"; // TODO: appsettings'ten oku
            WebhookUrl = $"{Request.Scheme}://{Request.Host}/api/webhook/jotform?secret={secret}";

            // Google Sheet URL'sini al
            try
            {
                var userSheet = await _googleSheetsService.GetUserGoogleSheetAsync(UserId, actualGroupId);
                SpreadsheetUrl = userSheet?.SpreadsheetUrl ?? string.Empty;
                
                if (string.IsNullOrEmpty(SpreadsheetUrl))
                {
                    _logger.LogWarning($"No Google Sheet found for UserId={UserId}, GroupId={actualGroupId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting Google Sheet for UserId={UserId}, GroupId={actualGroupId}");
            }

            // JotForm iframe src oluþtur (hidden field parametreleri ile)
            if (!string.IsNullOrWhiteSpace(JotFormId))
            {
                JotFormIFrameSrc = $"https://form.jotform.com/{JotFormId}?userId={UserId}&formId={FormId}&groupId={actualGroupId}";
                JotFormBaseUrl = "https://form.jotform.com";
                JotFormEmbedHandlerUrl = string.Empty;
                
                _logger.LogInformation($"JotForm iframe URL: {JotFormIFrameSrc}");
            }
            else
            {
                _logger.LogWarning($"JotForm ID is empty for FormId={id}");
                JotFormIFrameSrc = "about:blank";
            }

            return Page();
        }
    }
}
