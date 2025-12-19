using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using FormSepeti.Services.Interfaces;
using FormSepeti.Data.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FormSepeti.Web.Pages.Form
{
    public class ViewModel : PageModel
    {
        private readonly IFormService _formService;
        private readonly IGoogleSheetsService _googleSheetsService;
        private readonly IUserService _userService;
        private readonly IFormGroupRepository _formGroupRepository;
        private readonly IPackageService _packageService;
        private readonly IFormSubmissionRepository _formSubmissionRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ViewModel> _logger;

        public ViewModel(
            IFormService formService, 
            IGoogleSheetsService googleSheetsService,
            IUserService userService,
            IFormGroupRepository formGroupRepository,
            IPackageService packageService,
            IFormSubmissionRepository formSubmissionRepository,
            IConfiguration configuration,
            ILogger<ViewModel> logger)
        {
            _formService = formService;
            _googleSheetsService = googleSheetsService;
            _userService = userService;
            _formGroupRepository = formGroupRepository;
            _packageService = packageService;
            _formSubmissionRepository = formSubmissionRepository;
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)] 
        public int FormId { get; set; }
        
        [BindProperty(SupportsGet = true)] 
        public int? GroupId { get; set; }
        
        public int UserId { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string JotFormId { get; set; } = string.Empty;
        
        public string FormTitle { get; private set; } = "Form";
        public string WebhookUrl { get; private set; } = string.Empty;
        public string SpreadsheetUrl { get; private set; } = string.Empty;

        // ✅ İSTATİSTİK PROPERTİLERİ
        public int TotalSubmissions { get; private set; }
        public int MonthlySubmissions { get; private set; }
        public string LastSubmissionDate { get; private set; } = "Henüz yok";

        public string JotFormEmbedHtml { get; private set; } = string.Empty;
        public string JotFormJsUrl { get; private set; } = string.Empty;
        public string JotFormIFrameSrc { get; private set; } = "about:blank";
        public string JotFormIFrameId { get; private set; } = $"JotFormIFrame-{Guid.NewGuid():N}";
        public string JotFormEmbedHandlerUrl { get; private set; } = string.Empty;
        public string JotFormBaseUrl { get; private set; } = string.Empty;

        private int GetCurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        // ✅ PROJE STANDARDINA UYGUN: Query string ile id parametresi
        public async Task<IActionResult> OnGetAsync(int? groupId = null)
        {
            // ✅ FormId otomatik olarak [BindProperty(SupportsGet = true)] ile query string'den gelecek
            // URL: /Form/View?id=16&groupId=5
            
            if (FormId == 0)
            {
                _logger.LogWarning("FormId is 0 or missing");
                return NotFound();
            }

            UserId = GetCurrentUserId();
            if (UserId == 0)
            {
                _logger.LogWarning("User not authenticated, redirecting to login");
                return RedirectToPage("/Account/Login");
            }

            // ✅ Kullanıcı bilgilerini al
            var user = await _userService.GetUserByIdAsync(UserId);
            if (user == null)
            {
                _logger.LogWarning($"User not found: UserId={UserId}");
                return RedirectToPage("/Account/Login");
            }
            
            UserEmail = user.Email ?? string.Empty;

            // ✅ Google bağlı mı kontrol et
            var isGoogleConnected = !string.IsNullOrEmpty(user.GoogleRefreshToken);
            if (!isGoogleConnected)
            {
                TempData["Error"] = "Formları kullanabilmek için önce Google Sheets hesabınızı bağlamalısınız.";
                return RedirectToPage("/Sheets/Connect");
            }

            var form = await _formService.GetFormByIdAsync(FormId);
            if (form == null)
            {
                _logger.LogWarning($"Form not found: FormId={FormId}");
                return NotFound();
            }

            FormTitle = form.FormName;
            JotFormId = form.JotFormId;

            // GroupId'yi belirle
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

            // ✅ ERİŞİM KONTROLÜ
            var accessInfo = await _packageService.GetUserAccessToFormAsync(UserId, FormId, actualGroupId);
            
            if (!accessInfo.HasAccess)
            {
                _logger.LogWarning($"❌ Access denied! UserId={UserId}, FormId={FormId}, GroupId={actualGroupId}");
                _logger.LogWarning($"   IsFree={accessInfo.IsFree}, RequiresPackage={accessInfo.RequiresPackage}");
                
                TempData["Error"] = "Bu forma erişim yetkiniz yok. Lütfen paketi satın alın.";
                return RedirectToPage("/Package/Index");
            }

            _logger.LogInformation($"✅ Access granted! UserId={UserId}, FormId={FormId}, IsFree={accessInfo.IsFree}");

            // ✅ İSTATİSTİKLERİ ÇEKME
            try
            {
                var submissions = await _formSubmissionRepository.GetByUserAndFormIdAsync(UserId, FormId);
                TotalSubmissions = submissions.Count;
                
                var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                MonthlySubmissions = submissions.Count(s => s.SubmittedDate >= startOfMonth);
                
                var lastSubmission = submissions.OrderByDescending(s => s.SubmittedDate).FirstOrDefault();
                if (lastSubmission != null)
                {
                    var timeDiff = DateTime.UtcNow - lastSubmission.SubmittedDate;
                    if (timeDiff.TotalMinutes < 60)
                        LastSubmissionDate = $"{(int)timeDiff.TotalMinutes} dakika önce";
                    else if (timeDiff.TotalHours < 24)
                        LastSubmissionDate = $"{(int)timeDiff.TotalHours} saat önce";
                    else if (timeDiff.TotalDays < 7)
                        LastSubmissionDate = $"{(int)timeDiff.TotalDays} gün önce";
                    else
                        LastSubmissionDate = lastSubmission.SubmittedDate.ToString("dd.MM.yyyy HH:mm");
                }
                
                _logger.LogInformation($"📊 Statistics - Total: {TotalSubmissions}, Monthly: {MonthlySubmissions}, Last: {LastSubmissionDate}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching form statistics");
            }

            // Secret'i appsettings.json'dan al
            var secret = _configuration["JotForm:WebhookSecret"];
            if (string.IsNullOrEmpty(secret))
            {
                _logger.LogError("JotForm:WebhookSecret is not configured in appsettings.json!");
                secret = "default-secret-change-me";
            }

            WebhookUrl = $"{Request.Scheme}://{Request.Host}/api/webhook/jotform?secret={secret}";

            try
            {
                var userSheet = await _googleSheetsService.GetUserGoogleSheetAsync(UserId, actualGroupId);
                
                // ✅ Sheet yoksa oluştur
                if (userSheet == null)
                {
                    _logger.LogWarning($"No Google Sheet found for UserId={UserId}, GroupId={actualGroupId}. Creating new spreadsheet...");
                    
                    var group = await _formGroupRepository.GetByIdAsync(actualGroupId);
                    if (group != null)
                    {
                        var newSheetUrl = await _googleSheetsService.CreateSpreadsheetForUserGroup(
                            UserId, 
                            actualGroupId, 
                            group.GroupName
                        );
                        
                        if (!string.IsNullOrEmpty(newSheetUrl))
                        {
                            SpreadsheetUrl = newSheetUrl;
                            _logger.LogInformation($"✅ Created Google Spreadsheet: {newSheetUrl}");
                        }
                        else
                        {
                            _logger.LogError($"Failed to create Google Spreadsheet for UserId={UserId}, GroupId={actualGroupId}");
                        }
                    }
                }
                else
                {
                    SpreadsheetUrl = userSheet.SpreadsheetUrl;
                    _logger.LogInformation($"✅ Existing Google Sheet found: {SpreadsheetUrl}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling Google Sheet for UserId={UserId}, GroupId={actualGroupId}");
            }

            if (!string.IsNullOrWhiteSpace(JotFormId))
            {
                var encodedEmail = System.Web.HttpUtility.UrlEncode(UserEmail);
                JotFormIFrameSrc = $"https://form.jotform.com/{JotFormId}?userId={UserId}&formId={FormId}&groupId={actualGroupId}&userEmail={encodedEmail}";
                JotFormBaseUrl = "https://form.jotform.com";
                JotFormEmbedHandlerUrl = string.Empty;
                
                _logger.LogInformation($"📋 JotForm iframe URL: {JotFormIFrameSrc}");
            }
            else
            {
                _logger.LogWarning($"JotForm ID is empty for FormId={FormId}");
                JotFormIFrameSrc = "about:blank";
            }

            return Page();
        }
    }
}