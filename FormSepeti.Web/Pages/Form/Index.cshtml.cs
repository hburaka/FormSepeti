// FormSepeti.Web\Pages\Form\Index.cshtml.cs
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Services.Interfaces;
using FormSepeti.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FormSepeti.Web.Pages.Form
{
    public class IndexModel : PageModel
    {
        private readonly IFormService _formService;
        private readonly IPackageService _packageService;
        private readonly IGoogleSheetsService _googleSheetsService;
        private readonly IFormGroupRepository _formGroupRepository;

        public IndexModel(
            IFormService formService,
            IPackageService packageService,
            IGoogleSheetsService googleSheetsService,
            IFormGroupRepository formGroupRepository)
        {
            _formService = formService;
            _packageService = packageService;
            _googleSheetsService = googleSheetsService;
            _formGroupRepository = formGroupRepository;
        }

        [BindProperty(SupportsGet = true)]
        public int? GroupId { get; set; }

        public string GroupName { get; set; } = string.Empty;
        public string SpreadsheetUrl { get; set; } = string.Empty;
        public List<FormWithAccessInfo> Forms { get; set; } = new();

        private int GetCurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return RedirectToPage("/Account/Login");
            }

            if (!GroupId.HasValue)
            {
                TempData["Warning"] = "Lütfen önce bir grup seçin.";
                return RedirectToPage("/Dashboard/SelectGroup");
            }

            var group = await _formGroupRepository.GetByIdAsync(GroupId.Value);
            if (group == null)
            {
                return RedirectToPage("/Dashboard/SelectGroup");
            }

            GroupName = group.GroupName;

            // ? Formlarý getir (artýk SubmissionCount dahil)
            Forms = await _formService.GetFormsByGroupIdAsync(userId, GroupId.Value);

            // Google Sheets URL'ini al
            try
            {
                var userSheet = await _googleSheetsService.GetUserGoogleSheetAsync(userId, GroupId.Value);
                SpreadsheetUrl = userSheet?.SpreadsheetUrl ?? string.Empty;
            }
            catch
            {
                SpreadsheetUrl = string.Empty;
            }

            return Page();
        }

        // ? Session'a kaydet ve yönlendir
        public async Task<IActionResult> OnPostSetActiveFormAsync(int formId, int groupId)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return RedirectToPage("/Account/Login");
            }

            // ? Eriþim kontrolü
            var accessInfo = await _packageService.GetUserAccessToFormAsync(userId, formId, groupId);
            if (!accessInfo.HasAccess)
            {
                TempData["Error"] = "Bu forma eriþim yetkiniz yok.";
                return RedirectToPage("/Package/Index");
            }

            // ? Session'a kaydet
            HttpContext.Session.SetInt32("ActiveFormId", formId);
            HttpContext.Session.SetInt32("ActiveGroupId", groupId);

            // ? View sayfasýna yönlendir (parametre YOK!)
            return RedirectToPage("/Form/View");
        }
    }
}