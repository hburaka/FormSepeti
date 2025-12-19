using System;
using System.Threading.Tasks;

namespace FormSepeti.Services.Interfaces
{
    public interface ILoginAttemptService
    {
        bool IsLockedOut(string identifier);
        void RecordFailedAttempt(string identifier);
        void ResetAttempts(string identifier);
        int GetRemainingAttempts(string identifier);
        TimeSpan GetLockoutTimeRemaining(string identifier);
        void CleanupExpiredAttempts();
        Task<bool> IsIpBlockedAsync(string ipAddress);
        Task RecordFailedLoginAsync(string email, string ipAddress);
    }
}