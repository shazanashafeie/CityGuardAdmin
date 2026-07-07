namespace CityGuardAdmin.Models
{
    public class UserModel
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Role { get; set; } = "user";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}