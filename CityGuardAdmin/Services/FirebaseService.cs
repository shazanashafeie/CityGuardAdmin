using Google.Cloud.Firestore;
using CityGuardAdmin.Models;

namespace CityGuardAdmin.Services
{
    public class FirebaseService
    {
        private readonly FirestoreDb _db;

        public FirebaseService(FirestoreDb db)
        {
            _db = db;
        }

        // ── DASHBOARD STATS ──────────────────────────────────
        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            var stats = new DashboardStats();

            var pendingTask = _db.Collection("reports")
                .WhereEqualTo("status", "pending")
                .GetSnapshotAsync();

            var totalReportsTask = _db.Collection("reports")
                .GetSnapshotAsync();

            var usersTask = _db.Collection("users")
                .GetSnapshotAsync();

            var zonesTask = _db.Collection("dangerZones")
                .WhereEqualTo("isActive", true)
                .GetSnapshotAsync();

            await Task.WhenAll(
                pendingTask, totalReportsTask,
                usersTask, zonesTask
            );

            stats.PendingReports = pendingTask.Result.Count;
            stats.TotalReports = totalReportsTask.Result.Count;
            stats.TotalUsers = usersTask.Result.Count;
            stats.ActiveDangerZones = zonesTask.Result.Count;

            var recentSnap = await _db.Collection("reports")
                .OrderByDescending("createdAt")
                .Limit(5)
                .GetSnapshotAsync();

            stats.RecentReports = recentSnap.Documents
                .Select(MapReport)
                .ToList();

            return stats;
        }

        // ── REPORTS ──────────────────────────────────────────
        public async Task<List<ReportModel>> GetReportsAsync(
            string? statusFilter = null)
        {
            Query query = _db.Collection("reports")
                .OrderByDescending("createdAt");

            if (!string.IsNullOrEmpty(statusFilter))
                query = _db.Collection("reports")
                    .WhereEqualTo("status", statusFilter)
                    .OrderByDescending("createdAt");

            var snap = await query.GetSnapshotAsync();
            return snap.Documents.Select(MapReport).ToList();
        }

        public async Task UpdateReportStatusAsync(
            string reportId, string status)
        {
            await _db.Collection("reports")
                .Document(reportId)
                .UpdateAsync(new Dictionary<string, object>
                {
                    { "status", status },
                    { "updatedAt", Timestamp.GetCurrentTimestamp() }
                });
        }

        public async Task DeleteReportAsync(string reportId)
        {
            await _db.Collection("reports")
                .Document(reportId)
                .DeleteAsync();
        }

        // ── USERS ────────────────────────────────────────────
        public async Task<List<UserModel>> GetUsersAsync()
        {
            var snap = await _db.Collection("users")
                .OrderByDescending("createdAt")
                .GetSnapshotAsync();

            return snap.Documents.Select(MapUser).ToList();
        }

        public async Task UpdateUserStatusAsync(
            string userId, bool isActive)
        {
            await _db.Collection("users")
                .Document(userId)
                .UpdateAsync("isActive", isActive);
        }

        // ── DANGER ZONES ─────────────────────────────────────
        public async Task<List<DangerZoneModel>> GetDangerZonesAsync()
        {
            var snap = await _db.Collection("dangerZones")
                .GetSnapshotAsync();

            return snap.Documents.Select(MapDangerZone).ToList();
        }

        public async Task AddDangerZoneAsync(DangerZoneModel zone)
        {
            var data = new Dictionary<string, object>
            {
                { "label", zone.Label },
                { "center", new GeoPoint(zone.Latitude, zone.Longitude) },
                { "radiusMeters", zone.RadiusMeters },
                { "severity", zone.Severity },
                { "reasons", zone.Reasons },
                { "isActive", true },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            };

            await _db.Collection("dangerZones").AddAsync(data);
        }

        public async Task DeleteDangerZoneAsync(string zoneId)
        {
            await _db.Collection("dangerZones")
                .Document(zoneId)
                .DeleteAsync();
        }

        public async Task ToggleDangerZoneAsync(
            string zoneId, bool isActive)
        {
            await _db.Collection("dangerZones")
                .Document(zoneId)
                .UpdateAsync("isActive", isActive);
        }

        // ── UPDATE DANGER ZONE ────────────────────────────────
        public async Task UpdateDangerZoneAsync(
            string zoneId, string label, string severity,
            double radiusMeters, List<string> reasons)
        {
            await _db.Collection("dangerZones")
                .Document(zoneId)
                .UpdateAsync(new Dictionary<string, object>
                {
                    { "label", label },
                    { "severity", severity },
                    { "radiusMeters", radiusMeters },
                    { "reasons", reasons },
                    { "updatedAt", Timestamp.GetCurrentTimestamp() }
                });
        }

        // ── POST ALERT TO COMMUNITY CHAT ──────────────────────
        public async Task PostAlertToChatAsync(string type, string address)
        {
            var message = new Dictionary<string, object>
            {
                { "userId", "SYSTEM" },
                { "displayName", "CityGuard Admin" },
                { "areaId", "general" },
                { "text", $"⚠️ Verified report: {type} near {address}. Stay alert." },
                { "type", "alert" },
                { "reportId", "" },
                { "mediaUrl", "" },
                { "createdAt", Timestamp.GetCurrentTimestamp() },
                { "deletedAt", (object)null! }
            };

            await _db.Collection("chatMessages").AddAsync(message);
        }

        // ── GET REPORT COUNTS PER USER ────────────────────────
        public async Task<Dictionary<string, int>> GetReportCountsPerUserAsync()
        {
            var snap = await _db.Collection("reports").GetSnapshotAsync();
            var counts = new Dictionary<string, int>();

            foreach (var doc in snap.Documents)
            {
                var data = doc.ToDictionary();
                var userId = GetString(data, "userId");
                if (string.IsNullOrEmpty(userId)) continue;

                if (counts.ContainsKey(userId))
                    counts[userId]++;
                else
                    counts[userId] = 1;
            }

            return counts;
        }

        // ── PROMOTE TO ADMIN ──────────────────────────────────
        public async Task PromoteToAdminAsync(string userId)
        {
            await _db.Collection("users")
                .Document(userId)
                .UpdateAsync("role", "admin");
        }

        // ── SMART SEVERITY SUGGESTION ─────────────────────────
        public async Task<(string severity, string reason)>
            SuggestSeverityAsync(double lat, double lng)
        {
            var now = DateTime.UtcNow;

            var reportsSnap = await _db.Collection("reports")
                .WhereEqualTo("status", "verified")
                .GetSnapshotAsync();

            int recentReports = 0;
            int oldReports = 0;

            foreach (var doc in reportsSnap.Documents)
            {
                var data = doc.ToDictionary();
                if (data["location"] is not GeoPoint geo) continue;

                var dist = CalculateDistance(
                    lat, lng, geo.Latitude, geo.Longitude);
                if (dist > 200) continue;

                if (data.ContainsKey("createdAt") &&
                    data["createdAt"] is Timestamp ts)
                {
                    if (ts.ToDateTime() >= now.AddDays(-30))
                        recentReports++;
                    else
                        oldReports++;
                }
            }

            var ratingsSnap = await _db.Collection("safetyRatings")
                .GetSnapshotAsync();

            int recentGoodRatings = 0;
            int recentBadRatings = 0;

            foreach (var doc in ratingsSnap.Documents)
            {
                var data = doc.ToDictionary();
                if (data["location"] is not GeoPoint geo) continue;

                var dist = CalculateDistance(
                    lat, lng, geo.Latitude, geo.Longitude);
                if (dist > 200) continue;

                if (data["createdAt"] is not Timestamp ts) continue;
                if (ts.ToDateTime() < now.AddDays(-30)) continue;

                var score = data.ContainsKey("score")
                    ? Convert.ToDouble(data["score"]) : 3;

                if (score >= 4) recentGoodRatings++;
                else if (score <= 2) recentBadRatings++;
            }

            double riskScore =
                (recentReports * 3.0) +
                (oldReports * 1.0) +
                (recentBadRatings * 1.5) -
                (recentGoodRatings * 2.0);

            string severity;
            string reason;

            if (riskScore >= 8)
            {
                severity = "high";
                reason = recentReports > 0
                    ? $"{recentReports} recent verified incident(s) " +
                      $"reported within 200m in the last 30 days."
                    : $"{oldReports} historical incident(s) on record " +
                      $"near this location.";
            }
            else if (riskScore >= 4)
            {
                severity = "medium";
                reason = recentGoodRatings > 0 && recentReports > 0
                    ? $"Some incidents reported but {recentGoodRatings} " +
                      $"user(s) recently rated this area as safe."
                    : recentReports > 0
                        ? $"{recentReports} incident(s) reported. " +
                          $"Monitor closely."
                        : "Low recent activity but historical " +
                          "incidents on record.";
            }
            else
            {
                severity = "low";
                reason = recentGoodRatings > 0
                    ? $"{recentGoodRatings} user(s) recently rated " +
                      $"this area as safe with no new incidents."
                    : "No recent incidents or negative ratings " +
                      "reported near this location.";
            }

            return (severity, reason);
        }

        // ── UPDATE ZONE SEVERITY ──────────────────────────────
        public async Task UpdateZoneSeverityAsync(
            string zoneId, string severity)
        {
            await _db.Collection("dangerZones")
                .Document(zoneId)
                .UpdateAsync(new Dictionary<string, object>
                {
                    { "severity", severity },
                    { "updatedAt", Timestamp.GetCurrentTimestamp() }
                });
        }

        // ── ANALYTICS DATA ────────────────────────────────────
        public async Task<AnalyticsData> GetAnalyticsAsync()
        {
            var analytics = new AnalyticsData();

            var reportsSnap = await _db.Collection("reports")
                .GetSnapshotAsync();

            var reports = reportsSnap.Documents
                .Select(MapReport)
                .ToList();

            analytics.PendingCount = reports
                .Count(r => r.Status == "pending");
            analytics.VerifiedCount = reports
                .Count(r => r.Status == "verified");
            analytics.RejectedCount = reports
                .Count(r => r.Status == "rejected");

            analytics.ReportsByType = reports
                .GroupBy(r => r.Type)
                .ToDictionary(g => g.Key, g => g.Count());

            var last30Days = Enumerable.Range(0, 30)
                .Select(i => DateTime.Today.AddDays(-29 + i))
                .ToList();

            analytics.ReportsByDay = last30Days
                .ToDictionary(
                    day => day.ToString("d MMM"),
                    day => reports.Count(r =>
                        r.CreatedAt.Date == day.Date)
                );

            var ratingsSnap = await _db.Collection("safetyRatings")
                .GetSnapshotAsync();

            var ratings = ratingsSnap.Documents
                .Select(doc => {
                    var data = doc.ToDictionary();
                    return new
                    {
                        Score = data.ContainsKey("score")
                            ? Convert.ToDouble(data["score"]) : 0,
                        TimeOfDay = GetString(data, "timeOfDay"),
                        CreatedAt = data.ContainsKey("createdAt") &&
                            data["createdAt"] is Timestamp ts
                            ? ts.ToDateTime() : DateTime.Now
                    };
                }).ToList();

            analytics.TotalRatings = ratings.Count;
            analytics.AverageRating = ratings.Any()
                ? Math.Round(ratings.Average(r => r.Score), 1) : 0;

            analytics.RatingDistribution = new Dictionary<string, int>
            {
                { "Very Unsafe (1)", ratings.Count(r => r.Score < 2) },
                { "Unsafe (2)", ratings.Count(r => r.Score >= 2 && r.Score < 3) },
                { "Moderate (3)", ratings.Count(r => r.Score >= 3 && r.Score < 4) },
                { "Safe (4)", ratings.Count(r => r.Score >= 4 && r.Score < 5) },
                { "Very Safe (5)", ratings.Count(r => r.Score == 5) },
            };

            var usersSnap = await _db.Collection("users")
                .GetSnapshotAsync();

            var users = usersSnap.Documents
                .Select(doc => {
                    var data = doc.ToDictionary();
                    return data.ContainsKey("createdAt") &&
                        data["createdAt"] is Timestamp ts
                        ? ts.ToDateTime() : DateTime.Now;
                }).ToList();

            analytics.TotalUsers = users.Count;
            analytics.UserGrowthByDay = last30Days
                .ToDictionary(
                    day => day.ToString("d MMM"),
                    day => users.Count(u => u.Date <= day.Date)
                );

            var zonesSnap = await _db.Collection("dangerZones")
                .WhereEqualTo("isActive", true)
                .GetSnapshotAsync();

            analytics.ActiveDangerZones = zonesSnap.Count;

            return analytics;
        }

        // ── MAPPERS ──────────────────────────────────────────
        private ReportModel MapReport(DocumentSnapshot doc)
        {
            var data = doc.ToDictionary();
            var rawAddress = GetString(data, "address");

            var address = IsCoordinateString(rawAddress)
                ? GetString(data, "type") + " incident — address pending"
                : rawAddress;

            return new ReportModel
            {
                Id = doc.Id,
                UserId = GetString(data, "userId"),
                Type = GetString(data, "type"),
                Description = GetString(data, "description"),
                Address = address,
                Status = GetString(data, "status"),
                MediaUrls = data.ContainsKey("mediaUrls")
                    ? (data["mediaUrls"] as List<object> ?? new())
                        .Select(x => x.ToString() ?? "").ToList()
                    : new List<string>(),
                CreatedAt = data.ContainsKey("createdAt") &&
                    data["createdAt"] is Timestamp ts
                    ? ts.ToDateTime() : DateTime.Now,
            };
        }

        private UserModel MapUser(DocumentSnapshot doc)
        {
            var data = doc.ToDictionary();
            return new UserModel
            {
                Id = doc.Id,
                Email = GetString(data, "email"),
                DisplayName = GetString(data, "displayName"),
                Role = GetString(data, "role"),
                IsActive = data.ContainsKey("isActive") &&
                    (bool)data["isActive"],
                CreatedAt = data.ContainsKey("createdAt") &&
                    data["createdAt"] is Timestamp ts
                    ? ts.ToDateTime() : DateTime.Now,
            };
        }

        private DangerZoneModel MapDangerZone(DocumentSnapshot doc)
        {
            var data = doc.ToDictionary();
            var geo = data.ContainsKey("center")
                ? (GeoPoint)data["center"]
                : new GeoPoint(0, 0);

            return new DangerZoneModel
            {
                Id = doc.Id,
                Label = GetString(data, "label"),
                Latitude = geo.Latitude,
                Longitude = geo.Longitude,
                RadiusMeters = data.ContainsKey("radiusMeters")
                    ? Convert.ToDouble(data["radiusMeters"]) : 100,
                Severity = GetString(data, "severity"),
                Reasons = data.ContainsKey("reasons")
                    ? (data["reasons"] as List<object> ?? new())
                        .Select(x => x.ToString() ?? "").ToList()
                    : new List<string>(),
                IsActive = data.ContainsKey("isActive") &&
                    (bool)data["isActive"],
            };
        }

        // ── HELPERS ───────────────────────────────────────────
        private bool IsCoordinateString(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return System.Text.RegularExpressions.Regex.IsMatch(
                value, @"^-?\d+\.\d+,\s*-?\d+\.\d+$");
        }

        private double CalculateDistance(
            double lat1, double lng1,
            double lat2, double lng2)
        {
            const double R = 6371000;
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLng = (lng2 - lng1) * Math.PI / 180;
            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) *
                Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return R * 2 * Math.Atan2(
                Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private string GetString(
            Dictionary<string, object> data, string key)
        {
            return data.ContainsKey(key)
                ? data[key]?.ToString() ?? "" : "";
        }
    }
}