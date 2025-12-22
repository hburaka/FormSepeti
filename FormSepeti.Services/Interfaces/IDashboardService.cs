// FormSepeti.Services/Interfaces/IDashboardService.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Services.Models;

namespace FormSepeti.Services.Interfaces
{
    public interface IDashboardService
    {
        Task<AdminDashboardStats> GetDashboardStatsAsync();
        Task<List<RecentActivity>> GetRecentActivitiesAsync(int count = 10);
    }
}