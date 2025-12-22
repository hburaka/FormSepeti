// FormSepeti.Services/Implementations/DashboardService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FormSepeti.Data;
using FormSepeti.Services.Interfaces;
using FormSepeti.Services.Models;

namespace FormSepeti.Services.Implementations
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;

        public DashboardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AdminDashboardStats> GetDashboardStatsAsync()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfDay = now.Date;

            var stats = new AdminDashboardStats
            {
                // User Stats
                TotalUsers = await _context.Users.CountAsync(),
                ActiveUsers = await _context.Users.CountAsync(u => u.IsActive && u.IsActivated),
                NewUsersThisMonth = await _context.Users.CountAsync(u => u.CreatedDate >= startOfMonth),
                NewUsersToday = await _context.Users.CountAsync(u => u.CreatedDate >= startOfDay),

                // Package Stats
                TotalPackages = await _context.Packages.CountAsync(),
                ActivePackages = await _context.Packages.CountAsync(p => p.IsActive),
                TotalUserPackages = await _context.UserPackages.CountAsync(up => up.IsActive),
                TotalRevenue = await _context.UserPackages.SumAsync(up => up.PaymentAmount),
                RevenueThisMonth = await _context.UserPackages
                    .Where(up => up.PurchaseDate >= startOfMonth)
                    .SumAsync(up => up.PaymentAmount),

                // Form Stats
                TotalForms = await _context.Forms.CountAsync(),
                TotalGroups = await _context.FormGroups.CountAsync(),
                TotalSubmissions = await _context.FormSubmissions.CountAsync(),
                SubmissionsToday = await _context.FormSubmissions
                    .CountAsync(s => s.SubmittedDate >= startOfDay),

                // System Stats
                TotalAdmins = await _context.AdminUsers.CountAsync(),
                TotalAuditLogs = await _context.AuditLogs.CountAsync()
            };

            return stats;
        }

        public async Task<List<RecentActivity>> GetRecentActivitiesAsync(int count = 10)
        {
            var logs = await _context.AuditLogs
                .Include(a => a.AdminUser)
                .Include(a => a.User)
                .OrderByDescending(a => a.CreatedDate)
                .Take(count)
                .ToListAsync();

            return logs.Select(log => new RecentActivity
            {
                Action = log.Action,
                EntityType = log.EntityType,
                Details = log.Details,
                CreatedDate = log.CreatedDate,
                PerformedBy = log.AdminUser != null 
                    ? $"Admin: {log.AdminUser.FullName}" 
                    : log.User != null 
                        ? $"User: {log.User.Email}" 
                        : "System"
            }).ToList();
        }
    }
}