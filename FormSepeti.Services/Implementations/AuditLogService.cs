// FormSepeti.Services/Implementations/AuditLogService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Services.Implementations
{
    public class AuditLogService : IAuditLogService
    {
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ILogger<AuditLogService> _logger;

        public AuditLogService(
            IAuditLogRepository auditLogRepository,
            ILogger<AuditLogService> logger)
        {
            _auditLogRepository = auditLogRepository;
            _logger = logger;
        }

        public async Task LogAsync(int? adminId, string action, string entityType, int? entityId, string details, string ipAddress)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    AdminId = adminId,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    Details = details,
                    IpAddress = ipAddress,
                    CreatedDate = DateTime.UtcNow
                };

                await _auditLogRepository.CreateAsync(auditLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating audit log");
            }
        }

        public async Task LogUserActionAsync(int userId, string action, string entityType, int? entityId, string details, string ipAddress)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    Details = details,
                    IpAddress = ipAddress,
                    CreatedDate = DateTime.UtcNow
                };

                await _auditLogRepository.CreateAsync(auditLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user action audit log");
            }
        }

        public async Task<List<AuditLog>> GetRecentLogsAsync(int count = 50)
        {
            return await _auditLogRepository.GetRecentLogsAsync(count);
        }

        public async Task<List<AuditLog>> GetLogsByAdminAsync(int adminId, int count = 100)
        {
            return await _auditLogRepository.GetLogsByAdminIdAsync(adminId, count);
        }

        public async Task<List<AuditLog>> GetLogsByUserAsync(int userId, int count = 100)
        {
            return await _auditLogRepository.GetLogsByUserIdAsync(userId, count);
        }

        public async Task<List<AuditLog>> GetLogsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _auditLogRepository.GetLogsByDateRangeAsync(startDate, endDate);
        }

        public async Task<List<AuditLog>> GetLogsByEntityAsync(string entityType, int entityId)
        {
            return await _auditLogRepository.GetLogsByEntityAsync(entityType, entityId);
        }

        public async Task<int> GetTotalLogCountAsync()
        {
            return await _auditLogRepository.GetTotalLogCountAsync();
        }
    }
}