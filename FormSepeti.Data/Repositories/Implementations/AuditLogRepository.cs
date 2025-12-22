// FormSepeti.Data/Repositories/Implementations/AuditLogRepository.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;

namespace FormSepeti.Data.Repositories.Implementations
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly ApplicationDbContext _context;

        public AuditLogRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AuditLog> CreateAsync(AuditLog auditLog)
        {
            auditLog.CreatedDate = DateTime.UtcNow;
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
            return auditLog;
        }

        public async Task<List<AuditLog>> GetRecentLogsAsync(int count = 50)
        {
            return await _context.AuditLogs
                .Include(a => a.AdminUser)
                .Include(a => a.User)
                .OrderByDescending(a => a.CreatedDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetLogsByAdminIdAsync(int adminId, int count = 100)
        {
            return await _context.AuditLogs
                .Include(a => a.AdminUser)
                .Where(a => a.AdminId == adminId)
                .OrderByDescending(a => a.CreatedDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetLogsByUserIdAsync(int userId, int count = 100)
        {
            return await _context.AuditLogs
                .Include(a => a.User)
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetLogsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.AuditLogs
                .Include(a => a.AdminUser)
                .Include(a => a.User)
                .Where(a => a.CreatedDate >= startDate && a.CreatedDate <= endDate)
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetLogsByEntityAsync(string entityType, int entityId)
        {
            return await _context.AuditLogs
                .Include(a => a.AdminUser)
                .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();
        }

        public async Task<int> GetTotalLogCountAsync()
        {
            return await _context.AuditLogs.CountAsync();
        }
        public async Task<List<AuditLog>> GetAllAsync()
        {
            return await _context.AuditLogs
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();
        }
    }
}