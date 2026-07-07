using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CityGuardAdmin.Models;
using CityGuardAdmin.Services;

namespace CityGuardAdmin.Pages
{
    public class ReportsModel : PageModel
    {
        private readonly FirebaseService _firebase;

        public ReportsModel(FirebaseService firebase)
        {
            _firebase = firebase;
        }

        public List<ReportModel> Reports { get; set; } = new();
        public string CurrentFilter { get; set; } = "all";
        public int PendingCount { get; set; }
        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage { get; set; } = "";

        public async Task<IActionResult> OnGetAsync(string? status = "all")
        {
            if (string.IsNullOrEmpty(
                HttpContext.Session.GetString("AdminEmail")))
                return RedirectToPage("/Login");

            // Load TempData messages
            SuccessMessage = TempData["Success"]?.ToString() ?? "";
            ErrorMessage = TempData["Error"]?.ToString() ?? "";

            CurrentFilter = status ?? "all";

            var allReports = await _firebase.GetReportsAsync();
            PendingCount = allReports.Count(r => r.Status == "pending");

            Reports = CurrentFilter == "all"
                ? allReports
                : allReports
                    .Where(r => r.Status == CurrentFilter)
                    .ToList();

            return Page();
        }

        // ── VERIFY ───────────────────────────────────────────
        public async Task<IActionResult> OnPostVerifyAsync(
            string reportId, string address, string type)
        {
            if (string.IsNullOrEmpty(
                HttpContext.Session.GetString("AdminEmail")))
                return RedirectToPage("/Login");

            try
            {
                await _firebase.UpdateReportStatusAsync(
                    reportId, "verified");

                await _firebase.PostAlertToChatAsync(
                    type, address);

                TempData["Success"] =
                    "Report verified and community alerted!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed: {ex.Message}";
            }

            return RedirectToPage(new { status = "pending" });
        }

        // ── REJECT ───────────────────────────────────────────
        public async Task<IActionResult> OnPostRejectAsync(
            string reportId)
        {
            if (string.IsNullOrEmpty(
                HttpContext.Session.GetString("AdminEmail")))
                return RedirectToPage("/Login");

            try
            {
                await _firebase.UpdateReportStatusAsync(
                    reportId, "rejected");
                TempData["Success"] = "Report rejected.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed: {ex.Message}";
            }

            return RedirectToPage(new { status = "pending" });
        }

        // ── DELETE ───────────────────────────────────────────
        public async Task<IActionResult> OnPostDeleteAsync(
            string reportId)
        {
            if (string.IsNullOrEmpty(
                HttpContext.Session.GetString("AdminEmail")))
                return RedirectToPage("/Login");

            try
            {
                await _firebase.DeleteReportAsync(reportId);
                TempData["Success"] = "Report deleted.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}