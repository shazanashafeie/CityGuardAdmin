using CityGuardAdmin.Services;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;

var builder = WebApplication.CreateBuilder(args);

// ── Firebase Admin SDK ──────────────────────────────────────
string? firebaseCredentialJson = Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_JSON");

GoogleCredential credential;

if (!string.IsNullOrEmpty(firebaseCredentialJson))
{
    // Used on Render (or any environment with the env var set)
    credential = GoogleCredential.FromJson(firebaseCredentialJson);
}
else
{
    // Used locally on your PC, reads the file directly
    credential = GoogleCredential.FromFile("firebase-adminsdk.json");
}

FirebaseApp.Create(new AppOptions
{
    Credential = credential
});

// ── Register Firestore as singleton ─────────────────────────
builder.Services.AddSingleton(provider =>
{
    var firestoreBuilder = new FirestoreDbBuilder
    {
        ProjectId = "cityguardshazlai",
        Credential = credential
    };
    return firestoreBuilder.Build();
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
