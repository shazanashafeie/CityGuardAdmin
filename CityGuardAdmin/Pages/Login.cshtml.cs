using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FirebaseAdmin.Auth;

namespace CityGuardAdmin.Pages
{
    public class LoginModel : PageModel
    {
        private readonly Google.Cloud.Firestore.FirestoreDb _db;

        public LoginModel(Google.Cloud.Firestore.FirestoreDb db)
        {
            _db = db;
        }

        public string ErrorMessage { get; set; } = "";

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync(
            string email, string password)
        {
            try
            {
                // Verify credentials via Firebase Auth REST API
                using var client = new HttpClient();
                var apiKey = "AIzaSyAbko3X_oA0HqFS5DrFql158AMFhq8g8Os";
                var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={apiKey}";

                var payload = new
                {
                    email,
                    password,
                    returnSecureToken = true
                };

                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json,
                    System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    ErrorMessage = "Invalid email or password.";
                    return Page();
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var authResult = System.Text.Json.JsonSerializer.Deserialize
                    <Dictionary<string, object>>(responseBody);

                var localId = authResult?["localId"]?.ToString() ?? "";

                // Check if user is admin in Firestore
                var userDoc = await _db.Collection("users")
                    .Document(localId)
                    .GetSnapshotAsync();

                if (!userDoc.Exists ||
                    userDoc.GetValue<string>("role") != "admin")
                {
                    ErrorMessage = "Access denied. Admin accounts only.";
                    return Page();
                }

                // Store admin session
                HttpContext.Session.SetString("AdminEmail", email);
                HttpContext.Session.SetString("AdminUid", localId);

                return RedirectToPage("/Dashboard");
            }
            catch (Exception ex)
            {
                ErrorMessage = "Login failed. Please try again.";
                Console.WriteLine($"Login error: {ex.Message}");
                return Page();
            }
        }
    }
}