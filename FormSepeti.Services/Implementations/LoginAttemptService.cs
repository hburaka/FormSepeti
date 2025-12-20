using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using FormSepeti.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FormSepeti.Services.Implementations
{
    public class LoginAttemptService : ILoginAttemptService
    {
        private readonly ConcurrentDictionary<string, LoginAttemptRecord> _attempts = new();
        private readonly int _maxAttempts;
        private readonly int _lockoutMinutes;

        public LoginAttemptService(IConfiguration configuration)
        {
            _maxAttempts = int.TryParse(configuration["Security:LoginAttempts:MaxAttempts"], out var max) ? max : 5;
            _lockoutMinutes = int.TryParse(configuration["Security:LoginAttempts:LockoutMinutes"], out var lockout) ? lockout : 5;
        }
        
        public bool IsLockedOut(string identifier)
        {
            if (_attempts.TryGetValue(identifier, out var record))
            {
                if (record.Attempts >= _maxAttempts)
                {
                    var lockoutEnd = record.LastAttempt.AddMinutes(_lockoutMinutes);
                    if (DateTime.UtcNow < lockoutEnd)
                    {
                        return true;
                    }
                    else
                    {
                        // Lockout süresi dolmuş, sıfırla
                        _attempts.TryRemove(identifier, out _);
                    }
                }
            }
            return false;
        }

        public void RecordFailedAttempt(string identifier)
        {
            _attempts.AddOrUpdate(
                identifier,
                new LoginAttemptRecord { Attempts = 1, LastAttempt = DateTime.UtcNow },
                (key, existing) => new LoginAttemptRecord
                {
                    Attempts = existing.Attempts + 1,
                    LastAttempt = DateTime.UtcNow
                }
            );
        }

        public void ResetAttempts(string identifier)
        {
            _attempts.TryRemove(identifier, out _);
        }

        public int GetRemainingAttempts(string identifier)
        {
            if (_attempts.TryGetValue(identifier, out var record))
            {
                return Math.Max(0, _maxAttempts - record.Attempts);
            }
            return _maxAttempts;
        }

        public TimeSpan GetLockoutTimeRemaining(string identifier)
        {
            if (_attempts.TryGetValue(identifier, out var record))
            {
                var lockoutEnd = record.LastAttempt.AddMinutes(_lockoutMinutes);
                var remaining = lockoutEnd - DateTime.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            return TimeSpan.Zero;
        }

        // Arka planda expired kayıtları temizle
        public void CleanupExpiredAttempts()
        {
            var expiredKeys = _attempts
                .Where(kvp => DateTime.UtcNow.Subtract(kvp.Value.LastAttempt).TotalMinutes > _lockoutMinutes)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _attempts.TryRemove(key, out _);
            }
        }

        // ✅ Async metodlar - Login.cshtml.cs için
        public Task<bool> IsIpBlockedAsync(string ipAddress)
        {
            return Task.FromResult(IsLockedOut(ipAddress));
        }

        public Task RecordFailedLoginAsync(string email, string ipAddress)
        {
            RecordFailedAttempt(ipAddress);
            return Task.CompletedTask;
        }
    }

    public class LoginAttemptRecord
    {
        public int Attempts { get; set; }
        public DateTime LastAttempt { get; set; }
    }
}