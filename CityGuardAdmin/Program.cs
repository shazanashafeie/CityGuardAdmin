using CityGuardAdmin.Services;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;

var builder = WebApplication.CreateBuilder(args);

// ── Firebase Admin SDK ──────────────────────────────────────
FirebaseApp.Create(new AppOptions
{
    Credential = GoogleCredential.FromFile("firebase-adminsdk.json")
});

// Set environment variable for Firestore
string credPath = Path.Combine(
    Directory.GetCurrentDirectory(),
    "firebase-adminsdk.json"
);
Environment.SetEnvironmentVariable(
    "GOOGLE_APPLICATION_CREDENTIALS", credPath
);

// ── Register Firestore as singleton ─────────────────────────
builder.Services.AddSingleton(provider =>
{
    return FirestoreDb.Create("cityguardshazlai");
});
builder.Services.AddScoped<FirebaseService>();

// ── Add Razor Pages + Session ────────────────────────────────
builder.Services.AddRazorPages();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();
app.MapRazorPages();

// Default route goes to login
app.MapGet("/", context =>
{
    context.Response.Redirect("/Login");
    return Task.CompletedTask;
});

app.Run();