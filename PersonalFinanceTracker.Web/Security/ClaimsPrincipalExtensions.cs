using System.Security.Claims;

namespace PersonalFinanceTracker.Web.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The signed-in user's ID, taken from their authentication cookie or bearer token -
    /// i.e. something the server issued and signed, not something the browser can edit.
    /// Only call this on endpoints that require authentication.
    /// </summary>
    public static string GetRequiredUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No user ID on an authenticated request.");
}
