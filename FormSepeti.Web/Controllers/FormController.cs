using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Services.Interfaces;
using FormSepeti.Services.Models;

namespace FormSepeti.Web.Controllers
{
    [Authorize]
    public class FormController : Controller
    {
        private readonly IFormService _formService;
        private readonly IPackageService _packageService;
        private readonly IGoogleSheetsService _googleSheetsService;
        private readonly IUserService _userService;
        private readonly IFormGroupRepository _formGroupRepository;

        public FormController(
            IFormService formService,
            IPackageService packageService,
            IGoogleSheetsService googleSheetsService,
            IUserService userService,
            IFormGroupRepository formGroupRepository)
        {
            _formService = formService;
            _packageService = packageService;
            _googleSheetsService = googleSheetsService;
            _userService = userService;
            _formGroupRepository = formGroupRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            var groupId = HttpContext.Session.GetInt32("ActiveGroupId");

            if (!groupId.HasValue)
            {
                TempData["Warning"] = "Lütfen önce bir grup seçin.";
                return RedirectToAction("SelectGroup", "Dashboard");
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (string.IsNullOrEmpty(user.GoogleRefreshToken))
            {
                TempData["Warning"] = "Formları kullanabilmek için önce Google Sheets hesabınızı bağlamalısınız.";
                return RedirectToAction("Index", "Dashboard");
            }

            var group = await _formGroupRepository.GetByIdAsync(groupId.Value);
            if (group == null)
            {
                return RedirectToAction("SelectGroup", "Dashboard");
            }

            var forms = await _formService.GetFormsByGroupIdAsync(userId, groupId.Value);

            var model = new FormListViewModel
            {
                GroupId = groupId.Value,
                GroupName = group.GroupName,
                Forms = forms
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> View(int id)
        {
            var userId = GetCurrentUserId();
            var groupId = HttpContext.Session.GetInt32("ActiveGroupId");

            if (!groupId.HasValue)
            {
                TempData["Error"] = "Grup seçilmemiş.";
                return RedirectToAction("Index");
            }

            var form = await _formService.GetFormByIdAsync(id);
            if (form == null)
            {
                return NotFound();
            }

            var accessInfo = await _packageService.GetUserAccessToFormAsync(userId, id, groupId.Value);
            if (!accessInfo.HasAccess)
            {
                TempData["Error"] = "Bu forma erişim izniniz yok. Lütfen paket satın alın.";
                return RedirectToAction("Index", "Package");
            }

            await EnsureGoogleSheetExists(userId, groupId.Value, form);

            var webhookUrl = GenerateWebhookUrl(userId, id, groupId.Value);

            var model = new FormViewModel
            {
                Form = form,
                GroupId = groupId.Value,
                JotFormEmbedUrl = $"https://form.jotform.com/{form.JotFormId}",
                WebhookUrl = webhookUrl,
                IsFree = accessInfo.IsFree
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> History(int id)
        {
            var userId = GetCurrentUserId();
            var submissions = await _formService.GetSubmissionsByFormIdAsync(userId, id);
            var form = await _formService.GetFormByIdAsync(id);

            var model = new FormHistoryViewModel
            {
                Form = form,
                Submissions = submissions
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> AllSubmissions()
        {
            var userId = GetCurrentUserId();
            var submissions = await _formService.GetAllSubmissionsByUserIdAsync(userId);
            return View(submissions);
        }

        [HttpGet]
        public async Task<IActionResult> OpenGoogleSheet(int formId)
        {
            var userId = GetCurrentUserId();
            var groupId = HttpContext.Session.GetInt32("ActiveGroupId");

            if (!groupId.HasValue)
            {
                return Json(new { success = false, message = "Grup seçilmemiş." });
            }

            var userSheet = await _googleSheetsService.GetUserGoogleSheetAsync(userId, groupId.Value);

            if (userSheet == null)
            {
                return Json(new { success = false, message = "Google Sheets bulunamadı." });
            }

            return Json(new { success = true, url = userSheet.SpreadsheetUrl });
        }

        [HttpGet]
        public IActionResult GetWebhookUrl(int formId)
        {
            var userId = GetCurrentUserId();
            var groupId = HttpContext.Session.GetInt32("ActiveGroupId");

            if (!groupId.HasValue)
            {
                return Json(new { success = false, message = "Grup seçilmemiş." });
            }

            var webhookUrl = GenerateWebhookUrl(userId, formId, groupId.Value);
            return Json(new { success = true, webhookUrl = webhookUrl });
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return int.Parse(userIdClaim ?? "0");
        }

        private string GenerateWebhookUrl(int userId, int formId, int groupId)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            return $"{baseUrl}/api/webhook/jotform/{userId}/{formId}/{groupId}";
        }

        private async Task EnsureGoogleSheetExists(int userId, int groupId, Form form)
        {
            var userSheet = await _googleSheetsService.GetUserGoogleSheetAsync(userId, groupId);

            if (userSheet == null)
            {
                var group = await _formGroupRepository.GetByIdAsync(groupId);
                await _googleSheetsService.CreateSpreadsheetForUserGroup(userId, groupId, group.GroupName);
            }
        }
    }

    public class FormListViewModel
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; }
        public List<FormWithAccessInfo> Forms { get; set; }
    }

    public class FormViewModel
    {
        public Form Form { get; set; }
        public int GroupId { get; set; }
        public string JotFormEmbedUrl { get; set; }
        public string WebhookUrl { get; set; }
        public bool IsFree { get; set; }
    }

    public class FormHistoryViewModel
    {
        public Form Form { get; set; }
        public List<FormSubmission> Submissions { get; set; }
    }
}