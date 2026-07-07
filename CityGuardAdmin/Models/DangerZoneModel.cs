namespace CityGuardAdmin.Models
{
    public class DangerZoneModel
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double RadiusMeters { get; set; }
        public string Severity { get; set; } = "low";
        public List<string> Reasons { get; set; } = new();
        public bool IsActive { get; set; } = true;
    }
}