using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FormSepeti.Web.Areas.Admin.Pages.Admins
{
    [Authorize(Policy = "SuperAdminOnly")] // Sadece SuperAdmin eriþebilir
    public class IndexModel : PageModel
    {
        private readonly IAdminService _adminService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IAdminService adminService,
            ILogger<IndexModel> logger)
        {
            _adminService = adminService;
            _logger = logger;
        }

        public List<AdminViewModel> Admins { get; set; } = new();
        public int TotalAdmins { get; set; }
        public int ActiveAdmins { get; set; }
        public int InactiveAdmins { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? RoleFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public async Task OnGetAsync()
        {
            _logger.LogInformation("SuperAdmin viewing admins list - SearchTerm: {SearchTerm}", SearchTerm);

            var allAdmins = await _adminService.GetAllAdminsAsync();
            var filteredAdmins = allAdmins.AsQueryable();

            // Arama filtresi
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                filteredAdmins = filteredAdmins.Where(a =>
                    a.Username.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    a.FullName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    a.Email.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));
            }

            // Rol filtresi
            if (!string.IsNullOrWhiteSpace(RoleFilter))
            {
                filteredAdmins = filteredAdmins.Where(a => a.Role == RoleFilter);
            }

            // Durum filtresi
            if (StatusFilter == "active")
            {
                filteredAdmins = filteredAdmins.Where(a => a.IsActive);
            }
            else if (StatusFilter == "inactive")
            {
                filteredAdmins = filteredAdmins.Where(a => !a.IsActive);
            }

            Admins = filteredAdmins.OrderByDescending(a => a.CreatedDate).Select(a => new AdminViewModel
            {
                AdminId = a.AdminId,
                Username = a.Username,
                FullName = a.FullName,
                Email = a.Email,
                Role = a.Role,
                IsActive = a.IsActive,
                CreatedDate = a.CreatedDate,
                LastLoginDate = a.LastLoginDate
            }).ToList();

            // Ýstatistikler
            TotalAdmins = allAdmins.Count;
            ActiveAdmins = allAdmins.Count(a => a.IsActive);
            InactiveAdmins = TotalAdmins - ActiveAdmins;
        }

        public async Task<IActionResult> OnPostToggleStatusAsync(int adminId)
        {
            _logger.LogInformation("SuperAdmin toggling admin status - AdminId: {AdminId}", adminId);

            // Kendini pasifleþtirmeyi engelle
            var currentAdminId = int.Parse(User.FindFirst("AdminId")?.Value ?? "0");
            if (currentAdminId == adminId)
            {
                TempData["ErrorMessage"] = "Kendi hesabýnýzý pasifleþtiremezsiniz.";
                return RedirectToPage();
            }

            var result = await _adminService.ToggleAdminStatusAsync(adminId);
            if (result)
            {
                TempData["SuccessMessage"] = "Admin durumu baþarýyla deðiþtirildi.";
            }
            else
            {
                TempData["ErrorMessage"] = "Admin durumu deðiþtirilemedi.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int adminId)
        {
            _logger.LogInformation("SuperAdmin deleting admin - AdminId: {AdminId}", adminId);

            // Kendini silmeyi engelle
            var currentAdminId = int.Parse(User.FindFirst("AdminId")?.Value ?? "0");
            if (currentAdminId == adminId)
            {
                TempData["ErrorMessage"] = "Kendi hesabýnýzý silemezsiniz.";
                return RedirectToPage();
            }

            // Son SuperAdmin'i silmeyi engelle
            var admin = await _adminService.GetAdminByIdAsync(adminId);
            if (admin?.Role == "SuperAdmin")
            {
                var allAdmins = await _adminService.GetAllAdminsAsync();
                var superAdminCount = allAdmins.Count(a => a.Role == "SuperAdmin");

                if (superAdminCount <= 1)
                {
                    TempData["ErrorMessage"] = "Son SuperAdmin silinemez.";
                    return RedirectToPage();
                }
            }

            var result = await _adminService.DeleteAdminAsync(adminId);
            if (result)
            {
                TempData["SuccessMessage"] = "Admin baþarýyla silindi.";
            }
            else
            {
                TempData["ErrorMessage"] = "Admin silinemedi.";
            }

            return RedirectToPage();
        }

        public class AdminViewModel
        {
            public int AdminId { get; set; }
            public string Username { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public DateTime CreatedDate { get; set; }
            public DateTime? LastLoginDate { get; set; }
        }
    }
}