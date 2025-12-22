// FormSepeti.Services/Interfaces/IAuditLogService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;

namespace FormSepeti.Services.Interfaces
{
    public interface IAuditLogService
    {
        Task LogAsync(int? adminId, string action, string entityType, int? entityId, string details, string ipAddress);
        Task LogUserActionAsync(int userId, string action, string entityType, int? entityId, string details, string ipAddress);
        Task<List<AuditLog>> GetRecentLogsAsync(int count = 50);
        Task<List<AuditLog>> GetLogsByAdminAsync(int adminId, int count = 100);
        Task<List<AuditLog>> GetLogsByUserAsync(int userId, int count = 100);
        Task<List<AuditLog>> GetLogsByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<List<AuditLog>> GetLogsByEntityAsync(string entityType, int entityId);
        Task<int> GetTotalLogCountAsync();
    }
}