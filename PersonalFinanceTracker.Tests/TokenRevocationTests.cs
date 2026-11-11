using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace PersonalFinanceTracker.Tests;

/// <summary>
/// API tokens have to die when the account's security stamp changes, not an hour later.
/// Each test gets a token, proves it works, changes the account, then checks the same
/// token is refused (Security/ApiTokenStampFilter.cs).
/// </summary>
public sealed class TokenRevocationTests(TestApp app) : IClassFixture<TestApp>
{
    private sealed record TokenResponse(string AccessToken);

    private async Task<(HttpClient Client, string Email)> ClientWithTokenAsync()
    {
        var email = await app.CreateUserAsync();
        var client = app.Browser();
        var response = await client.PostAsJsonAsync("/api/auth/token", new { email, password = TestApp.Password });
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        // Sanity check: the token works before we touch the account.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/summary")).StatusCode);
        return (client, email);
    }

    private async Task WithUserAsync(string email, Func<UserManager<IdentityUser>, IdentityUser, Task> change)
    {
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        await change(users, user!);
    }

    [Fact]
    public async Task Token_stops_working_after_password_change()
    {
        var (client, email) = await ClientWithTokenAsync();

        await WithUserAsync(email, async (users, user) =>
        {
            var result = await users.ChangePasswordAsync(user, TestApp.Password, "a brand new long passphrase");
            Assert.True(result.Succeeded);
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/summary")).StatusCode);
    }

    [Fact]
    public async Task Token_stops_working_after_sign_out_everywhere()
    {
        var (client, email) = await ClientWithTokenAsync();

        await WithUserAsync(email, (users, user) => users.UpdateSecurityStampAsync(user));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/transactions")).StatusCode);
    }

    [Fact]
    public async Task Token_stops_working_after_account_deletion()
    {
        var (client, email) = await ClientWithTokenAsync();

        await WithUserAsync(email, (users, user) => users.DeleteAsync(user));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/summary")).StatusCode);
    }
}
