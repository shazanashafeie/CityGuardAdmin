using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CityGuardAdmin.Models;
using CityGuardAdmin.Services;

namespace CityGuardAdmin.Pages
{
    public class DangerZonesModel : PageModel
    {
        private readonly FirebaseService _firebase;

        public DangerZonesModel(FirebaseService firebase)
        {
            _firebase = firebase;
        }

        public List<DangerZoneModel> DangerZones { get; set; } = new();
        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public string SuggestedSeverity { get; set; } = "medium";
        public string SuggestionReason { get; set; } = "";

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(
                HttpContext.Session.GetString("AdminEmail")))
                return RedirectToPage("/Login");

            SuccessMessage = TempData["Success"]?.ToString() ?? "";
            ErrorMessage = TempData["Error"]?.ToString() ?? "";
            SuggestedSeverity = TempData["SeveritySuggestion"]
                ?.ToString() ?? "medium";
            SuggestionReason = TempData["SeverityReason"]
                ?.ToString() ?? "";

            DangerZones = await _firebase.GetDangerZonesAsync();
            return Page();
        }

        // ── ADD ZONE ─────────────────────────────────────────
        public async Task<IActionResult> OnPostAddAsync(
            string label,
            string locationName,
            double radiusMeters,
            string severity,
            List<string>? reasons)
        {
            if (string.IsNullOrEmpty(
                HttpContext.Session.GetString("AdminEmail")))
                return RedirectToPage("/Login");

            try
            {
                var (lat, lng) = await GeocodeLocationAsync(locationName);

                if (lat == 0 && lng == 0)
                {
                    TempData["Error"] =
                        $"Could not find location '{locationName}'. " +
                        "Please be more specific.";
                    return RedirectToPage();
                }

                var zone = new DangerZoneModel
                {
                    Label = label,
                    Latitude = lat,
                    Longitude = lng,
                    RadiusMeters = radiusMeters,
                    Severity = severity,
                    Reasons = reasons ?? new List<string>(),
                    IsActive = true
                };

                await _firebase.AddDangerZoneAsync(zone);
                TempData["Success"] =
                    $"Danger zone '{label}' added at {locationName}!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to add zone: {ex.Message}";
            }

            return RedirectToPage();
        }

        // ── EDIT ZONE ─────────────────────────────────────────
        public async Task<IActionResult> OnPostEditAsync(
            string zoneId,
            string label,
            string severity,
            double radiusMeters,
            List<string>? reasons)
        {
            if (string.IsNullOrEmpty(
                HttpContext.Session.GetString("AdminEmail")))
                return RedirectToPage("/Login");

            try
            {
                await _firebase.UpdateDangerZoneAsync(
                    zoneId, label, severity, radiusMeters,
                    reasons ?? new List<string>());

                TempData["Success"] =
                    $"Danger zone '<strong>{label}</strong>' updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to update zone: {ex.Message}";
            }

            return RedirectToPage();
        }

        // ── TOGGLE ACTIVE ────────────────────────────────────
        public async Task<IActionResult> OnPostToggleAsync(
            string zoneId, bool isActive)
        {
            if (string.IsNullOrEmpty(
                HttpContext.Session.GetString("AdminEmail")))
                return RedirectToPage("/Login");

            try
            {
                await _firebase.ToggleDangerZoneAsync(zoneId, isActive);
                TempData["Success"] = isActive
                    ? "Zone activated." : "Zone deactivated.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed: {ex.Message}";
            }

            return RedirectToPage();
        }

        // ── DELETE ZONE ──────────────────────────────────────
        public async Task<IActionResult> OnPostDeleteAsync(
            string zoneId)
        {
            if (string.IsNullOrEmpty(
                HttpContext.Session.GetString("AdminEmail")))
                return RedirectToPage("/Login");

            try
            {
                await _firebase.DeleteDangerZoneAsync(zoneId);
                TempData["Success"] = "Danger zone deleted.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed: {ex.Message}";
            }

            return RedirectToPage();
        }

        // ── RECALCULATE SEVERITY ──────────────────────────────
        public async Task<IActionResult> OnPostRecalculateAsync(
            string zoneId, double lat, double lng)
        {
            if (string.IsNullOrEmpty(
                HttpContext.Session.GetString("AdminEmail")))
                return RedirectToPage("/Login");

            try
            {
                var (severity, reason) = await _firebase
                    .SuggestSeverityAsync(lat, lng);

                await _firebase.UpdateZoneSeverityAsync(
                    zoneId, severity);

                TempData["Success"] =
                    $"Severity recalculated to " +
                    $"<strong>{severity.ToUpper()}</strong>. " +
                    $"Reason: {reason}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed: {ex.Message}";
            }

            return RedirectToPage();
        }

        // ── AJAX: Get severity suggestion ─────────────────────
        public async Task<IActionResult> OnGetSuggestSeverityAsync(
            double lat, double lng)
        {
            if (string.IsNullOrEmpty(
                HttpContext.Session.GetString("AdminEmail")))
                return Unauthorized();

            try
            {
                var (severity, reason) = await _firebase
                    .SuggestSeverityAsync(lat, lng);

                return new JsonResult(new { severity, reason });
            }
            catch
            {
                return new JsonResult(new
                {
                    severity = "medium",
                    reason = "Could not calculate suggestion. " +
                             "Please select manually."
                });
            }
        }

        // ── GEOCODE HELPER ────────────────────────────────────
        private async Task<(double lat, double lng)> GeocodeLocationAsync(
            string locationName)
        {
            try
            {
                using var client = new HttpClient();
                var apiKey = "AIzaSyDrKStt5JDq3mrf8BewV6SDBH_ygxnSqwY";
                var encoded = Uri.EscapeDataString(
                    locationName + ", Malaysia");
                var url =
                    $"https://maps.googleapis.com/maps/api/geocode/json" +
                    $"?address={encoded}&key={apiKey}";

                var response = await client.GetStringAsync(url);
                var json = System.Text.Json.JsonDocument.Parse(response);
                var results = json.RootElement.GetProperty("results");

                if (results.GetArrayLength() == 0)
                    return (0, 0);

                var location = results[0]
                    .GetProperty("geometry")
                    .GetProperty("location");

                var lat = location.GetProperty("lat").GetDouble();
                var lng = location.GetProperty("lng").GetDouble();
                return (lat, lng);
            }
            catch
            {
                return (0, 0);
            }
        }
    }
}