using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FormSepeti.Services.Interfaces;
using FormSepeti.Services.Models;

namespace FormSepeti.Web.Areas.Admin.Pages.Dashboard
{
    [Authorize(Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        private readonly IDashboardService _dashboardService;

        public IndexModel(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        public AdminDashboardStats Stats { get; set; } = new();
        public List<RecentActivity> RecentActivities { get; set; } = new();

        public async Task OnGetAsync()
        {
            Stats = await _dashboardService.GetDashboardStatsAsync();
            RecentActivities = await _dashboardService.GetRecentActivitiesAsync(15);
        }
    }
}