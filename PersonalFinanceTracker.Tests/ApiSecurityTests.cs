using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalFinanceTracker.Tests;

/// <summary>
/// "Prove the control holds" tests: each security rule has a test that would fail if the
/// rule were removed - a negative case (blocked) alongside the positive case (allowed).
/// </summary>
public sealed class ApiSecurityTests(TestApp app) : IClassFixture<TestApp>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed record TokenResponse(string AccessToken, int ExpiresIn);
    private sealed record TransactionDto(Guid Id, DateOnly Date, string Description, decimal Amount, string Type, string Category);
    private sealed record Page(List<TransactionDto> Items, int TotalCount);

    private async Task<HttpClient> SignedInApiClientAsync(string email, string password = TestApp.Password)
    {
        var client = app.Browser();
        var response = await client.PostAsJsonAsync("/api/auth/token", new { email, password });
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    private static object NewTransaction(string description = "Groceries", decimal amount = 42.10m) => new
    {
        description,
        amount,
        type = "Expense",
        category = "Food",
        date = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
    };

    // ---------------- Authentication ----------------

    [Fact]
    public async Task Api_without_token_is_rejected()
    {
        var response = await app.Browser().GetAsync("/api/transactions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Api_with_valid_token_can_create_and_read()
    {
        var client = await SignedInApiClientAsync(await app.CreateUserAsync());

        var created = await client.PostAsJsonAsync("/api/transactions", NewTransaction("Coffee", 3.20m));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var page = await client.GetFromJsonAsync<Page>("/api/transactions", Json);
        Assert.Single(page!.Items);
        Assert.Equal("Coffee", page.Items[0].Description);
        Assert.Equal(3.20m, page.Items[0].Amount);
    }

    [Fact]
    public async Task Wrong_password_gets_no_token()
    {
        var email = await app.CreateUserAsync();
        var response = await app.Browser().PostAsJsonAsync("/api/auth/token", new { email, password = "not the password at all" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_look_identical()
    {
        var email = await app.CreateUserAsync();
        var client = app.Browser();

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/token", new { email, password = "wrong-password-123" });
        var unknownUser = await client.PostAsJsonAsync("/api/auth/token", new { email = "nobody@example.test", password = "wrong-password-123" });

        Assert.Equal(wrongPassword.StatusCode, unknownUser.StatusCode);

        // Same title and message (bodies also carry a per-request traceId, so compare fields).
        var a = await wrongPassword.Content.ReadFromJsonAsync<JsonElement>();
        var b = await unknownUser.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(a.GetProperty("title").GetString(), b.GetProperty("title").GetString());
        Assert.Equal(a.GetProperty("detail").GetString(), b.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Account_locks_after_five_failed_attempts()
    {
        var email = await app.CreateUserAsync();
        var client = app.Browser();

        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/token", new { email, password = "guess-" + i });
        }

        // Even the correct password is refused while locked out.
        var response = await client.PostAsJsonAsync("/api/auth/token", new { email, password = TestApp.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------------- Authorization: users can't reach each other's data ----------------

    [Fact]
    public async Task User_cannot_read_update_or_delete_another_users_transaction()
    {
        var alice = await SignedInApiClientAsync(await app.CreateUserAsync());
        var bob = await SignedInApiClientAsync(await app.CreateUserAsync());

        var created = await alice.PostAsJsonAsync("/api/transactions", NewTransaction("Alice's rent", 900m));
        var alicesTx = await created.Content.ReadFromJsonAsync<TransactionDto>(Json);
        var url = $"/api/transactions/{alicesTx!.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync(url, NewTransaction("hacked", 1m))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync(url)).StatusCode);

        // Bob's list doesn't include it, and Alice's transaction is untouched.
        var bobsPage = await bob.GetFromJsonAsync<Page>("/api/transactions", Json);
        Assert.Empty(bobsPage!.Items);

        var stillThere = await alice.GetFromJsonAsync<TransactionDto>(url, Json);
        Assert.Equal("Alice's rent", stillThere!.Description);
        Assert.Equal(900m, stillThere.Amount);
    }

    // ---------------- Validation ----------------

    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    [InlineData(20_000_000)]
    public async Task Out_of_range_amounts_are_rejected(double amount)
    {
        var client = await SignedInApiClientAsync(await app.CreateUserAsync());
        var response = await client.PostAsJsonAsync("/api/transactions", NewTransaction(amount: (decimal)amount));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Category_must_match_type()
    {
        var client = await SignedInApiClientAsync(await app.CreateUserAsync());
        var response = await client.PostAsJsonAsync("/api/transactions", new
        {
            description = "Salary filed as rent",
            amount = 100m,
            type = "Income",
            category = "Rent",
            date = "2026-01-15"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------- Web pages ----------------

    [Fact]
    public async Task Pages_require_sign_in()
    {
        var response = await app.Browser().GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Auth/Login", response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Sign_in_page_sends_security_headers()
    {
        var response = await app.Browser().GetAsync("/Auth/Login");
        response.EnsureSuccessStatusCode();

        var csp = string.Join(";", response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("script-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task Form_post_without_antiforgery_token_is_rejected()
    {
        var response = await app.Browser().PostAsync("/Auth/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "someone@example.test",
            ["Password"] = "whatever-password"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Health_check_is_public()
    {
        var response = await app.Browser().GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
