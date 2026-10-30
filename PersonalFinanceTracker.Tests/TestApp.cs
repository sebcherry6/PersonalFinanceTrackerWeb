using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PersonalFinanceTracker.Tests;

/// <summary>
/// Starts the real application in memory with its own temporary SQLite file, so tests
/// exercise the actual middleware, auth, rate limiting and database - never your real data.
/// </summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"financetracker-test-{Guid.NewGuid():N}.db");

    public const string Password = "correct horse battery staple";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:AppDbContext"] = $"Data Source={_dbPath}",
            // High limits so the suite itself isn't throttled; the dedicated rate-limit test lowers them.
            ["RateLimiting:AuthPermitLimit"] = "1000",
            ["RateLimiting:ApiPermitLimit"] = "1000"
        }));
    }

    /// <summary>HTTPS client that doesn't follow redirects, so tests can assert on them.</summary>
    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    public async Task<string> CreateUserAsync(string? email = null)
    {
        email ??= $"user-{Guid.NewGuid():N}@example.test";
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var result = await users.CreateAsync(new IdentityUser { UserName = email, Email = email }, Password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        return email;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch (IOException) { /* best effort */ }
    }
}
