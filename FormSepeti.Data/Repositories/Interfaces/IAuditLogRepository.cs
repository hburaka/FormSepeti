// FormSepeti.Data/Repositories/Interfaces/IAuditLogRepository.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;

namespace FormSepeti.Data.Repositories.Interfaces
{
    public interface IAuditLogRepository
    {
        Task<AuditLog> CreateAsync(AuditLog auditLog);
        Task<List<AuditLog>> GetRecentLogsAsync(int count = 50);
        Task<List<AuditLog>> GetLogsByAdminIdAsync(int adminId, int count = 100);
        Task<List<AuditLog>> GetLogsByUserIdAsync(int userId, int count = 100);
        Task<List<AuditLog>> GetLogsByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<List<AuditLog>> GetLogsByEntityAsync(string entityType, int entityId);
        Task<int> GetTotalLogCountAsync();
        Task<List<AuditLog>> GetAllAsync();
    }
}