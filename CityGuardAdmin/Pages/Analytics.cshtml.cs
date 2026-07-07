using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CityGuardAdmin.Models;
using CityGuardAdmin.Services;

namespace CityGuardAdmin.Pages
{
    public class AnalyticsModel : PageModel
    {
        private readonly FirebaseService _firebase;

        public AnalyticsModel(FirebaseService firebase)
        {
            _firebase = firebase;
        }

        public AnalyticsData Data { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(
                HttpContext.Session.GetString("AdminEmail")))
                return RedirectToPage("/Login");

            Data = await _firebase.GetAnalyticsAsync();
            return Page();
        }
    }
}