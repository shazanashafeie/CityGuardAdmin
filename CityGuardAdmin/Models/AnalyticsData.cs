namespace CityGuardAdmin.Models
{
    public class AnalyticsData
    {
        public int PendingCount { get; set; }
        public int VerifiedCount { get; set; }
        public int RejectedCount { get; set; }
        public int TotalRatings { get; set; }
        public double AverageRating { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveDangerZones { get; set; }

        public Dictionary<string, int> ReportsByType { get; set; }
            = new();
        public Dictionary<string, int> ReportsByDay { get; set; }
            = new();
        public Dictionary<string, int> RatingDistribution { get; set; }
            = new();
        public Dictionary<string, int> UserGrowthByDay { get; set; }
            = new();
    }
}