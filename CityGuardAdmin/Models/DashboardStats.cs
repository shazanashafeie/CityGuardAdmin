namespace CityGuardAdmin.Models
{
    public class DashboardStats
    {
        public int PendingReports { get; set; }
        public int TotalReports { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveDangerZones { get; set; }
        public List<ReportModel> RecentReports { get; set; } = new();
    }
}