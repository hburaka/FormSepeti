using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Web.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IPackageService _packageService;
        private readonly IUserService _userService;
        private readonly IGoogleSheetsService _googleSheetsService;
        private readonly IFormSubmissionRepository _submissionRepository;

        public DashboardController(
            IPackageService packageService,
            IUserService userService,
            IGoogleSheetsService googleSheetsService,
            IFormSubmissionRepository submissionRepository)
        {
            _packageService = packageService;
            _userService = userService;
            _googleSheetsService = googleSheetsService;
            _submissionRepository = submissionRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            var user = await _userService.GetUserByIdAsync(userId);

            if (user == null)
            {
                return RedirectToAction("Logout", "Account");
            }

            var availableGroups = await _packageService.GetAvailableGroupsForUserAsync(userId);
            var activePackages = await _packageService.GetUserActivePackagesAsync(userId);
            var recentSubmissions = await _submissionRepository.GetRecentByUserIdAsync(userId, 10);

            var model = new DashboardViewModel
            {
                User = user,
                AvailableGroups = availableGroups,
                ActivePackages = activePackages,
                RecentSubmissions = recentSubmissions,
                IsGoogleConnected = !string.IsNullOrEmpty(user.GoogleRefreshToken),
                TotalSubmissions = await _submissionRepository.GetCountByUserIdAsync(userId)
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> SelectGroup()
        {
            var userId = GetCurrentUserId();
            var availableGroups = await _packageService.GetAvailableGroupsForUserAsync(userId);

            if (availableGroups.Count == 0)
            {
                TempData["Warning"] = "Henüz erişiminiz olan bir grup bulunmuyor.";
                return RedirectToAction("Index", "Package");
            }

            return View(availableGroups);
        }

        [HttpPost]
        public async Task<IActionResult> SetActiveGroup(int groupId)
        {
            var userId = GetCurrentUserId();
            var availableGroups = await _packageService.GetAvailableGroupsForUserAsync(userId);
            var hasAccess = availableGroups.Any(g => g.GroupId == groupId);

            if (!hasAccess)
            {
                TempData["Error"] = "Bu gruba erişim izniniz yok.";
                return RedirectToAction("SelectGroup");
            }

            HttpContext.Session.SetInt32("ActiveGroupId", groupId);
            TempData["Success"] = "Grup başarıyla seçildi!";
            return RedirectToAction("Index", "Form");
        }

        [HttpGet]
        public async Task<IActionResult> Statistics()
        {
            var userId = GetCurrentUserId();

            var stats = new StatisticsViewModel
            {
                TotalSubmissions = await _submissionRepository.GetCountByUserIdAsync(userId),
                SubmissionsThisMonth = await _submissionRepository.GetCountByUserIdThisMonthAsync(userId),
                SubmissionsToday = await _submissionRepository.GetCountByUserIdTodayAsync(userId),
                ActivePackagesCount = (await _packageService.GetUserActivePackagesAsync(userId)).Count,
                SubmissionsByGroup = await _submissionRepository.GetSubmissionCountByGroupAsync(userId),
                SubmissionsByMonth = await _submissionRepository.GetSubmissionCountByMonthAsync(userId, 6)
            };

            return View(stats);
        }

        [HttpGet]
        public async Task<IActionResult> CheckGoogleConnection()
        {
            var userId = GetCurrentUserId();
            var user = await _userService.GetUserByIdAsync(userId);
            var isConnected = !string.IsNullOrEmpty(user?.GoogleRefreshToken);

            return Json(new
            {
                isConnected = isConnected,
                message = isConnected ? "Google Sheets bağlı" : "Google Sheets bağlı değil."
            });
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return int.Parse(userIdClaim ?? "0");
        }
    }

    public class DashboardViewModel
    {
        public User User { get; set; }
        public List<FormGroup> AvailableGroups { get; set; }
        public List<UserPackage> ActivePackages { get; set; }
        public List<FormSubmission> RecentSubmissions { get; set; }
        public bool IsGoogleConnected { get; set; }
        public int TotalSubmissions { get; set; }
    }

    public class StatisticsViewModel
    {
        public int TotalSubmissions { get; set; }
        public int SubmissionsThisMonth { get; set; }
        public int SubmissionsToday { get; set; }
        public int ActivePackagesCount { get; set; }
        public Dictionary<string, int> SubmissionsByGroup { get; set; }
        public Dictionary<string, int> SubmissionsByMonth { get; set; }
    }
}