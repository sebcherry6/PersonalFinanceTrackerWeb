// =============================================================================
//  FinanceTracker by WebCherry - application startup
//
//  This file wires everything together, in two halves:
//    1. SERVICES   - what the app can use (database, sign-in, rate limits, ...)
//    2. PIPELINE   - the order every HTTP request passes through middleware
//
//  Security choices are commented where they're made, so you can see why each exists.
// =============================================================================

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonalFinanceTracker.Web.Data;
using PersonalFinanceTracker.Web.Infrastructure;
using PersonalFinanceTracker.Web.Security;
using PersonalFinanceTracker.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Don't advertise "Server: Kestrel" - it only helps attackers fingerprint the stack.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// -----------------------------------------------------------------------------
// 1. SERVICES
// -----------------------------------------------------------------------------

// --- Database ---------------------------------------------------------------
// SQLite file, path from ConnectionStrings:AppDbContext (App_Data/finance.db locally,
// /data/finance.db in the Docker image). The connection string is read when the context
// is created, so tests and environment variables can override it.
builder.Services.AddDbContext<AppDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("AppDbContext")
        ?? throw new InvalidOperationException("Connection string 'AppDbContext' is missing.");
    var contentRoot = services.GetRequiredService<IHostEnvironment>().ContentRootPath;

    options.UseSqlite(ResolveSqlitePath(connectionString, contentRoot));

    // If the model and the migration snapshot ever drift, log a warning instead of refusing
    // to start. Run `dotnet ef migrations has-pending-model-changes` to check, and
    // `dotnet ef migrations add <Name> --output-dir Data/Migrations` to fix.
    options.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));
});

// --- Identity (user accounts) ---------------------------------------------------
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        // Passwords: length beats complexity. 12+ characters with some variety;
        // no forced symbols (they push people toward "Password1!" patterns).
        options.Password.RequiredLength = 12;
        options.Password.RequiredUniqueChars = 4;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        // Lockout: 5 wrong passwords -> 15 minute pause. Stops online password guessing.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;

        // No email service is configured yet, so accounts can't require confirmation.
        // When you add one (e.g. SendGrid / Azure Communication Services), set this to true.
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Re-check the user's security stamp every 5 minutes (default 30). Changing the password
// or deleting the account then logs out other sessions quickly.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));

// --- The sign-in cookie -----------------------------------------------------------
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Auth/Login";
    options.LogoutPath = "/Auth/Logout";
    options.AccessDeniedPath = "/Auth/Login";

    // "__Host-" prefix: browsers only accept the cookie over HTTPS, for this exact host, path "/".
    options.Cookie.Name = "__Host-webcherry-auth";
    options.Cookie.HttpOnly = true;                              // JavaScript can't read it
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;     // HTTPS only
    options.Cookie.SameSite = SameSiteMode.Strict;               // never sent on cross-site requests

    // A finance app should not stay signed in forever. 8 hours, extended while active.
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// --- Bearer tokens for the JSON API -------------------------------------------------
// The API uses tokens (see Security/ApiAuth.cs for why). POST /api/auth/token returns one;
// it's valid for 1 hour and is sent as "Authorization: Bearer <token>".
builder.Services.AddAuthentication()
    .AddBearerToken(ApiAuth.Scheme, options => options.BearerTokenExpiration = TimeSpan.FromHours(1));

// --- Authorization: secure by default ----------------------------------------------
// Every endpoint requires a signed-in user unless it explicitly says [AllowAnonymous]
// (login, register, error pages, health check). A new controller can't be left open by mistake.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

// --- Anti-forgery (CSRF) and TempData cookies -----------------------------------------
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-webcherry-csrf";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.Configure<CookieTempDataProviderOptions>(options =>
{
    options.Cookie.Name = "__Host-webcherry-msg";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// --- Data protection keys ----------------------------------------------------------
// These keys encrypt the sign-in cookie and API tokens. In a container they must live on
// persistent storage, otherwise every restart/redeploy signs everybody out.
// Set DataProtection:KeysPath (the Dockerfile uses /data/keys).
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("WebCherry.FinanceTracker");
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

// --- MVC + global filters ------------------------------------------------------------
builder.Services
    .AddControllersWithViews(options =>
    {
        // Every POST/PUT/DELETE from a form must carry a valid anti-forgery token.
        // Forms built with the <form> tag helper include one automatically.
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());

        // Pages show financial data: tell browsers and proxies never to cache them,
        // so pressing Back after signing out on a shared computer shows nothing.
        options.Filters.Add(new ResponseCacheAttribute { NoStore = true, Location = ResponseCacheLocation.None });

        // Loads the signed-in user's theme and currency before each action.
        options.Filters.Add<LoadPreferencesFilter>();
    })
    .AddJsonOptions(options =>
    {
        // API sends/accepts enums as text: "Expense", not 0.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// --- Rate limiting, proxy, HTTPS, health ---------------------------------------------
builder.Services.AddAppRateLimiting(builder.Configuration);

// When hosted behind a reverse proxy / load balancer that terminates HTTPS (Azure App Service,
// Container Apps, Fly.io, nginx...), set ReverseProxy:Enabled=true so the app sees the real
// client IP and scheme. Only do this if the app is NOT directly reachable from the internet,
// otherwise clients could fake X-Forwarded-For.
var behindProxy = builder.Configuration.GetValue<bool>("ReverseProxy:Enabled");
if (behindProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

builder.Services.AddHealthChecks();

// --- Application services --------------------------------------------------------------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<UserPreferences>();
builder.Services.AddScoped<AccountSecurity>();

var app = builder.Build();

// -----------------------------------------------------------------------------
// Database: create the folder and apply any pending migrations on startup.
// Fine for a single SQLite instance. If you move to SQL Server/PostgreSQL with several
// instances, run migrations as a deployment step instead.
// -----------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var dataSource = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
    if (!string.IsNullOrEmpty(dataSource) && dataSource != ":memory:")
    {
        var folder = Path.GetDirectoryName(dataSource);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
    }

    db.Database.Migrate();
}

// -----------------------------------------------------------------------------
// 2. PIPELINE - order matters. Each request flows top to bottom.
// -----------------------------------------------------------------------------

if (behindProxy)
{
    app.UseForwardedHeaders();               // must run first so everything sees the real IP/scheme
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");       // friendly page, no stack traces for users
    app.UseHsts();                           // browsers remember: this site is HTTPS-only
}

// Turn empty 404/403/429 responses into a friendly page - but not for the JSON API,
// whose clients expect plain status codes.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/error/{0}"));

app.UseSecurityHeaders();                    // CSP and friends (Security/SecurityHeadersMiddleware.cs)
app.UseHttpsRedirection();
app.UseStaticFiles();                        // wwwroot: css, js, images (public, no sign-in needed)

app.UseRouting();
app.UseRateLimiter();                        // after routing, so [EnableRateLimiting] is visible
app.UseAuthentication();                     // who are you? (cookie or bearer token)
app.UseAuthorization();                      // are you allowed? (fallback policy above)

app.MapHealthChecks("/healthz").AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();

// SQLite resolves a relative "Data Source" against the *current directory*, which changes
// depending on how the app is started (`dotnet run --project ...` from the solution folder,
// Visual Studio, a service...). Anchoring it to the project's content root means the app
// always uses the same database file: App_Data/finance.db inside the project.
static string ResolveSqlitePath(string connectionString, string contentRoot)
{
    var builder = new SqliteConnectionStringBuilder(connectionString);
    if (!string.IsNullOrEmpty(builder.DataSource)
        && builder.DataSource != ":memory:"
        && !Path.IsPathRooted(builder.DataSource))
    {
        builder.DataSource = Path.Combine(contentRoot, builder.DataSource);
    }
    return builder.ToString();
}

// Lets the test project start the app in memory (WebApplicationFactory<Program>).
public partial class Program { }
