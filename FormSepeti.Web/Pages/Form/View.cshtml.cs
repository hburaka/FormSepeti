using FormSepeti.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration; // ? EKLE
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
        private readonly IConfiguration _configuration; // ? EKLE
        private readonly ILogger<ViewModel> _logger;

        public ViewModel(
            IFormService formService, 
            IGoogleSheetsService googleSheetsService,
            IConfiguration configuration, // ? EKLE
            ILogger<ViewModel> logger)
        {
            _formService = formService;
            _googleSheetsService = googleSheetsService;
            _configuration = configuration; // ? EKLE
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)] public int FormId { get; set; }
        [BindProperty(SupportsGet = true)] public int? GroupId { get; set; }
        
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

            UserId = GetCurrentUserId();
            if (UserId == 0)
            {
                _logger.LogWarning("User not authenticated, redirecting to login");
                return RedirectToPage("/Account/Login");
            }

            var form = await _formService.GetFormByIdAsync(id);
            if (form == null)
            {
                _logger.LogWarning($"Form not found: FormId={id}");
                return NotFound();
            }

            FormTitle = form.FormName;
            JotFormId = form.JotFormId;

            // ? GroupId'yi belirle
            int actualGroupId;

            if (groupId.HasValue)
            {
                actualGroupId = groupId.Value;
                _logger.LogInformation($"GroupId from URL parameter: {actualGroupId}");
            }
            else
            {
                actualGroupId = await _formService.GetFormGroupIdAsync(FormId);
                _logger.LogInformation($"GroupId from FormGroupMapping: {actualGroupId} for FormId={FormId}");
            }

            GroupId = actualGroupId;

            // ? Secret'ý appsettings.json'dan al
            var secret = _configuration["JotForm:WebhookSecret"];
            if (string.IsNullOrEmpty(secret))
            {
                _logger.LogError("JotForm:WebhookSecret is not configured in appsettings.json!");
                secret = "default-secret-change-me"; // Fallback (production'da olmamalý)
            }

            WebhookUrl = $"{Request.Scheme}://{Request.Host}/api/webhook/jotform?secret={secret}";

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

            if (!string.IsNullOrWhiteSpace(JotFormId))
            {
                JotFormIFrameSrc = $"https://form.jotform.com/{JotFormId}?userId={UserId}&formId={FormId}&groupId={actualGroupId}";
                JotFormBaseUrl = "https://form.jotform.com";
                JotFormEmbedHandlerUrl = string.Empty;
                
                _logger.LogInformation($"? JotForm iframe URL: {JotFormIFrameSrc}");
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
