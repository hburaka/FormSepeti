using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Web.Areas.Admin.Pages.Users
{
    [Authorize(Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserPackageRepository _userPackageRepository;
        private readonly IExportService _exportService; 
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IUserRepository userRepository,
            IUserPackageRepository userPackageRepository,
            IExportService exportService, 
            ILogger<IndexModel> logger)
        {
            _userRepository = userRepository;
            _userPackageRepository = userPackageRepository;
            _exportService = exportService; 
            _logger = logger;
        }

        public List<UserViewModel> Users { get; set; } = new();
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; } // "all", "active", "inactive"

        public async Task OnGetAsync()
        {
            _logger.LogInformation("Admin viewing users list - SearchTerm: {SearchTerm}, StatusFilter: {StatusFilter}",
                SearchTerm, StatusFilter);

            // Tüm kullanıcıları al
            var allUsers = await _userRepository.GetAllAsync();

            // Filtreleme
            var filteredUsers = allUsers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                filteredUsers = filteredUsers.Where(u =>
                    u.Email.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(SearchTerm)));
            }

            if (StatusFilter == "active")
            {
                filteredUsers = filteredUsers.Where(u => u.IsActive && u.IsActivated);
            }
            else if (StatusFilter == "inactive")
            {
                filteredUsers = filteredUsers.Where(u => !u.IsActive || !u.IsActivated);
            }

            // Her kullanıcı için aktif paket sayısını al
            Users = new List<UserViewModel>();
            foreach (var user in filteredUsers.OrderByDescending(u => u.CreatedDate))
            {
                var activePackages = await _userPackageRepository.GetActivePackagesByUserIdAsync(user.UserId);

                Users.Add(new UserViewModel
                {
                    UserId = user.UserId,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber ?? "-",
                    IsActive = user.IsActive,
                    IsActivated = user.IsActivated,
                    CreatedDate = user.CreatedDate,
                    LastLoginDate = user.LastLoginDate,
                    ActivePackageCount = activePackages.Count
                });
            }

            // İstatistikler
            TotalUsers = allUsers.Count;
            ActiveUsers = allUsers.Count(u => u.IsActive && u.IsActivated);
            InactiveUsers = TotalUsers - ActiveUsers;
        }

        public async Task<IActionResult> OnPostToggleStatusAsync(int userId)
        {
            _logger.LogInformation("Admin toggling user status - UserId: {UserId}", userId);

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
                return RedirectToPage();
            }

            user.IsActive = !user.IsActive;
            await _userRepository.UpdateAsync(user);

            TempData["SuccessMessage"] = $"Kullanıcı {(user.IsActive ? "aktif" : "pasif")} edildi.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetExportExcelAsync()
        {
            var users = await _userRepository.GetAllAsync();
            var excelData = _exportService.ExportUsersToExcel(users); // ✅ Service kullan

            return File(excelData, 
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                $"Users_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> OnGetExportCsvAsync()
        {
            var users = await _userRepository.GetAllAsync();
            var csvData = _exportService.ExportUsersToCsv(users); // ✅ Service kullan

            return File(Encoding.UTF8.GetBytes(csvData), 
                "text/csv", 
                $"Users_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }

        public class UserViewModel
        {
            public int UserId { get; set; }
            public string Email { get; set; } = string.Empty;
            public string PhoneNumber { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public bool IsActivated { get; set; }
            public DateTime CreatedDate { get; set; }
            public DateTime? LastLoginDate { get; set; }
            public int ActivePackageCount { get; set; }
        }
    }
}