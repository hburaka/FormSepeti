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

            // 1. URL'den groupId gelmiþ mi kontrol et
            // 2. Yoksa session'dan al
            // 3. O da yoksa varsayýlan 1 kullan (veya grup seçme sayfasýna yönlendir)
            var sessionGroupId = HttpContext.Session.GetInt32("ActiveGroupId");
            GroupId = groupId ?? sessionGroupId ?? 1;

            // Debug log
            _logger.LogInformation($"FormId={FormId}, URL GroupId={groupId}, Session GroupId={sessionGroupId}, Final GroupId={GroupId}");

            // Eðer session'da groupId yoksa ve URL'den de gelmemiþse kullanýcýyý grup seçmeye yönlendir
            // (Ýsterseniz bu kontrolü açabilirsiniz)
            /*
            if (!groupId.HasValue && !sessionGroupId.HasValue)
            {
                TempData["Warning"] = "Lütfen önce bir grup seçin.";
                return RedirectToPage("/Dashboard/SelectGroup");
            }
            */

            var userId = GetCurrentUserId();
            if (userId == 0)
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

            var actualGroupId = GroupId.Value;
            var secret = "9oq8r838ihaq"; // appsettings'ten oku (daha güvenli)
            WebhookUrl = $"{Request.Scheme}://{Request.Host}/api/webhook/jotform/{userId}/{FormId}/{actualGroupId}?secret={secret}";

            // Google Sheet URL'sini al
            try
            {
                var userSheet = await _googleSheetsService.GetUserGoogleSheetAsync(userId, GroupId.Value);
                SpreadsheetUrl = userSheet?.SpreadsheetUrl ?? string.Empty;
                
                if (string.IsNullOrEmpty(SpreadsheetUrl))
                {
                    _logger.LogWarning($"No Google Sheet found for UserId={userId}, GroupId={GroupId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting Google Sheet for UserId={userId}, GroupId={GroupId}");
            }

            // JotForm iframe src oluþtur
            if (form.JotFormId != null)
            {
                var idString = form.JotFormId.ToString();
                if (!string.IsNullOrWhiteSpace(idString))
                {
                    // Hidden field'lar için query parametreleri ekle
                    JotFormIFrameSrc = $"https://form.jotform.com/{idString}?userId={userId}&formId={FormId}&groupId={actualGroupId}";
                    JotFormBaseUrl = "https://form.jotform.com";
                    JotFormEmbedHandlerUrl = string.Empty;
                    
                    _logger.LogInformation($"JotForm iframe URL: {JotFormIFrameSrc}");
                    return Page();
                }
            }

            _logger.LogWarning($"JotForm ID is empty for FormId={id}");
            JotFormIFrameSrc = "about:blank";
            return Page();
        }
    }
}
