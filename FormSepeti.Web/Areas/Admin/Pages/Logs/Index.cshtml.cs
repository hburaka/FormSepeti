using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Web.Areas.Admin.Pages.Logs
{
    [Authorize(Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IUserRepository _userRepository;
        private readonly IAdminUserRepository _adminUserRepository;
        private readonly IExportService _exportService; // ✅ Ekleyin
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IAuditLogRepository auditLogRepository,
            IUserRepository userRepository,
            IAdminUserRepository adminUserRepository,
            IExportService exportService, // ✅ Ekleyin
            ILogger<IndexModel> logger)
        {
            _auditLogRepository = auditLogRepository;
            _userRepository = userRepository;
            _adminUserRepository = adminUserRepository;
            _exportService = exportService; // ✅ Ekleyin
            _logger = logger;
        }

        public List<AuditLogViewModel> Logs { get; set; } = new();
        public int TotalLogs { get; set; }
        public int TodayLogs { get; set; }
        public int UserActions { get; set; }
        public int AdminActions { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ActionFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? DateFilter { get; set; } // today, week, month, all

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 50;
        public int TotalPages { get; set; }

        public async Task OnGetAsync()
        {
            _logger.LogInformation("Admin viewing audit logs - Page: {PageNumber}, SearchTerm: {SearchTerm}",
                PageNumber, SearchTerm);

            // Tüm logları al
            var allLogs = await _auditLogRepository.GetAllAsync();

            // Filtreleme
            var filteredLogs = allLogs.AsQueryable();

            // Tarih filtresi
            var today = DateTime.UtcNow.Date;
            if (DateFilter == "today")
            {
                filteredLogs = filteredLogs.Where(l => l.CreatedDate.Date == today);
            }
            else if (DateFilter == "week")
            {
                var weekAgo = today.AddDays(-7);
                filteredLogs = filteredLogs.Where(l => l.CreatedDate >= weekAgo);
            }
            else if (DateFilter == "month")
            {
                var monthAgo = today.AddMonths(-1);
                filteredLogs = filteredLogs.Where(l => l.CreatedDate >= monthAgo);
            }

            // Arama filtresi
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                filteredLogs = filteredLogs.Where(l =>
                    l.Action.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (l.EntityType != null && l.EntityType.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (l.Details != null && l.Details.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)));
            }

            // Action filtresi
            if (!string.IsNullOrWhiteSpace(ActionFilter))
            {
                filteredLogs = filteredLogs.Where(l => l.Action.Contains(ActionFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Sayfalama
            TotalLogs = filteredLogs.Count();
            TotalPages = (int)Math.Ceiling(TotalLogs / (double)PageSize);

            var pagedLogs = filteredLogs
                .OrderByDescending(l => l.CreatedDate)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize);

            // View model'e dönüştür
            Logs = new List<AuditLogViewModel>();
            foreach (var log in pagedLogs)
            {
                string userName = "-";
                if (log.UserId.HasValue)
                {
                    var user = await _userRepository.GetByIdAsync(log.UserId.Value);
                    userName = user?.Email ?? "Kullanıcı Bulunamadı";
                }
                else if (log.AdminId.HasValue)
                {
                    var admin = await _adminUserRepository.GetByIdAsync(log.AdminId.Value);
                    userName = admin?.FullName ?? "Admin Bulunamadı";
                }

                Logs.Add(new AuditLogViewModel
                {
                    LogId = log.LogId,
                    Action = log.Action,
                    EntityType = log.EntityType ?? "-",
                    EntityId = log.EntityId,
                    Details = log.Details ?? "-",
                    UserName = userName,
                    IsAdmin = log.AdminId.HasValue,
                    IpAddress = log.IpAddress ?? "-",
                    CreatedDate = log.CreatedDate
                });
            }

            // İstatistikler
            TodayLogs = allLogs.Count(l => l.CreatedDate.Date == today);
            UserActions = allLogs.Count(l => l.UserId.HasValue);
            AdminActions = allLogs.Count(l => l.AdminId.HasValue);
        }

        public async Task<IActionResult> OnGetExportExcelAsync()
        {
            _logger.LogInformation("Admin exporting logs to Excel");

            var logs = await _auditLogRepository.GetAllAsync();
            
            foreach (var log in logs)
            {
                if (log.UserId.HasValue)
                {
                    log.User = await _userRepository.GetByIdAsync(log.UserId.Value);
                }
                else if (log.AdminId.HasValue)
                {
                    log.AdminUser = await _adminUserRepository.GetByIdAsync(log.AdminId.Value);
                }
            }

            var excelData = _exportService.ExportLogsToExcel(logs); // ✅ Service kullan

            return File(excelData, 
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                $"AuditLogs_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> OnGetExportCsvAsync()
        {
            _logger.LogInformation("Admin exporting logs to CSV");

            var logs = await _auditLogRepository.GetAllAsync();
            
            foreach (var log in logs)
            {
                if (log.UserId.HasValue)
                {
                    log.User = await _userRepository.GetByIdAsync(log.UserId.Value);
                }
                else if (log.AdminId.HasValue)
                {
                    log.AdminUser = await _adminUserRepository.GetByIdAsync(log.AdminId.Value);
                }
            }

            var csvData = _exportService.ExportLogsToCsv(logs); // ✅ Service kullan

            return File(Encoding.UTF8.GetBytes(csvData), 
                "text/csv", 
                $"AuditLogs_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }

        public class AuditLogViewModel
        {
            public int LogId { get; set; }
            public string Action { get; set; } = string.Empty;
            public string EntityType { get; set; } = string.Empty;
            public int? EntityId { get; set; }
            public string Details { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public bool IsAdmin { get; set; }
            public string IpAddress { get; set; } = string.Empty;
            public DateTime CreatedDate { get; set; }
        }
    }
}