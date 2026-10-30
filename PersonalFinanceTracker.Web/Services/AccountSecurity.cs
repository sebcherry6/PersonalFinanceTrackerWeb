using Microsoft.AspNetCore.Identity;

namespace PersonalFinanceTracker.Web.Services;

/// <summary>
/// Password sign-in shared by the web login form and the API token endpoint.
///
/// Two protections on top of ASP.NET Identity's defaults:
///  1. Lockout is always on - 5 wrong passwords pauses sign-in for 15 minutes
///     (configured in Program.cs). The original code passed lockoutOnFailure: false.
///  2. Constant-ish timing. Identity returns instantly for an unknown email but spends
///     ~100 ms hashing for a real one, so an attacker could time responses to learn which
///     emails have accounts. For unknown emails we hash against a dummy value so both paths
///     cost the same.
/// </summary>
public sealed class AccountSecurity(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn)
{
    // Created once per process; the plaintext is random and thrown away.
    private static readonly Lazy<string> DummyHash = new(() =>
        new PasswordHasher<IdentityUser>().HashPassword(new IdentityUser(), Guid.NewGuid().ToString("N")));

    /// <param name="scheme">
    /// Which authentication scheme receives the sign-in: null for the normal web cookie,
    /// or ApiAuth.Scheme to have the bearer-token handler write a token response instead.
    /// </param>
    public async Task<SignInResult> PasswordSignInAsync(
        string email, string password, bool rememberMe, string? scheme = null)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            users.PasswordHasher.VerifyHashedPassword(new IdentityUser(), DummyHash.Value, password);
            return SignInResult.Failed;
        }

        if (scheme is not null)
        {
            // SignInManager is created per request, so this only affects this one sign-in.
            signIn.AuthenticationScheme = scheme;
        }

        return await signIn.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);
    }
}
