using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CityGuardAdmin.Models;
using CityGuardAdmin.Services;

namespace CityGuardAdmin.Pages
{
    public class UsersModel : PageModel
    {
        private readonly FirebaseService _firebase;

        public UsersModel(FirebaseService firebase)
        {
            _firebase = firebase;
        }

        public List<UserModel> Users { get; set; } = new();
        public Dictionary<string, int> ReportCounts { get; set; } = new();
        public string CurrentFilter { get; set; } = "all";
        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage { get; set; } = "";

        public async Task<IActionResult> OnGetAsync(
            string? filter = "all")
        {
            if (string.IsNullOrEmpty(
                HttpContext.Session.GetString("AdminEmail")))
                return RedirectToPage("/Login");

            SuccessMessage = TempData["Success"]?.ToString() ?? "";
            ErrorMessage = TempData["Error"]?.ToString() ?? "";

            CurrentFilter = filter ?? "all";

            var allUsers = await _firebase.GetUsersAsync();

            // Apply filter
            Users = CurrentFilter switch
            {
                "active" => allUsers
                    .Where(u => u.IsActive && u.Role != "admin")
                    .ToList(),
                "banned" => allUsers
                    .Where(u => !u.IsActive)
                    .ToList(),
                "admin" => allUsers
                    .Where(u => u.Role == "admin")
                    .ToList(),
                _ => allUsers
            };

            // Get report counts for each user
            ReportCounts = await _firebase.GetReportCountsPerUserAsync();

            return Page();
        }

        // ── BAN / UNBAN ──────────────────────────────────────
        public async Task<IActionResult> OnPostToggleBanAsync(
            string userId, bool isActive)
        {
            if (string.IsNullOrEmpty(
                HttpContext.Session.GetString("AdminEmail")))
                return RedirectToPage("/Login");

            try
            {
                await _firebase.UpdateUserStatusAsync(userId, isActive);
                TempData["Success"] = isActive
                    ? "User unbanned successfully."
                    : "User banned successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed: {ex.Message}";
            }

            return RedirectToPage();
        }

        // ── PROMOTE TO ADMIN ─────────────────────────────────
        public async Task<IActionResult> OnPostPromoteAsync(
            string userId)
        {
            if (string.IsNullOrEmpty(
                HttpContext.Session.GetString("AdminEmail")))
                return RedirectToPage("/Login");

            try
            {
                await _firebase.PromoteToAdminAsync(userId);
                TempData["Success"] =
                    "User promoted to admin successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}