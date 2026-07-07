using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CityGuardAdmin.Models;
using CityGuardAdmin.Services;

namespace CityGuardAdmin.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly FirebaseService _firebase;
        public DashboardStats Stats { get; set; } = new();

        public DashboardModel(FirebaseService firebase)
        {
            _firebase = firebase;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Auth guard
            if (string.IsNullOrEmpty(
                HttpContext.Session.GetString("AdminEmail")))
            {
                return RedirectToPage("/Login");
            }

            Stats = await _firebase.GetDashboardStatsAsync();
            return Page();
        }
    }
}