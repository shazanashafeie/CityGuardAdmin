namespace CityGuardAdmin.Models
{
    public class ReportModel
    {
        public string Id { get; set; } = "";
        public string UserId { get; set; } = "";
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public string Address { get; set; } = "";
        public string Status { get; set; } = "pending";
        public List<string> MediaUrls { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}