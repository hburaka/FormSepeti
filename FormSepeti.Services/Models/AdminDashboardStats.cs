// FormSepeti.Services/Models/AdminDashboardStats.cs
namespace FormSepeti.Services.Models
{
    public class AdminDashboardStats
    {
        // User Stats
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int NewUsersThisMonth { get; set; }
        public int NewUsersToday { get; set; }

        // Package Stats
        public int TotalPackages { get; set; }
        public int ActivePackages { get; set; }
        public int TotalUserPackages { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal RevenueThisMonth { get; set; }

        // Form Stats
        public int TotalForms { get; set; }
        public int TotalGroups { get; set; }
        public int TotalSubmissions { get; set; }
        public int SubmissionsToday { get; set; }

        // System Stats
        public int TotalAdmins { get; set; }
        public int TotalAuditLogs { get; set; }
    }

    public class RecentActivity
    {
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string? PerformedBy { get; set; }
    }
}