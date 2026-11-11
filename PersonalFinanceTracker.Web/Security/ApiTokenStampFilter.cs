using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PersonalFinanceTracker.Web.Security;

/// <summary>
/// Kills API tokens as soon as the account's security stamp changes.
///
/// The bearer token handler only checks that a token is genuine and not expired. On its own
/// that means a stolen token keeps working for its full hour even after the owner changes
/// their password, signs out everywhere or deletes the account. The cookie gets re-checked
/// every 5 minutes by Identity, but tokens don't get that for free, so I do it here.
///
/// Every token carries the security stamp it was issued with (Identity puts it in the claims
/// at sign-in). On each API call I compare it with the stamp in the database. Password change,
/// "sign out everywhere" and account deletion all change or remove the stamp, so old tokens
/// get a 401 straight away. Costs one small user lookup per request, which is fine here.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ValidateApiTokenStampAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Authenticate with the bearer scheme myself rather than relying on filter order.
        // No token / bad token: leave it to [Authorize] to send the 401.
        var auth = await context.HttpContext.AuthenticateAsync(ApiAuth.Scheme);
        if (!auth.Succeeded || auth.Principal is null) return;

        var signIn = context.HttpContext.RequestServices.GetRequiredService<SignInManager<IdentityUser>>();
        var user = await signIn.ValidateSecurityStampAsync(auth.Principal);

        if (user is null)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ValidateApiTokenStampAttribute>>();
            AuditLog.StaleApiTokenRejected(logger, auth.Principal.GetRequiredUserId());

            context.Result = new ObjectResult(new ProblemDetails
            {
                Title = "Token no longer valid",
                Detail = "This token was issued before a password change or sign-out. Request a new one.",
                Status = StatusCodes.Status401Unauthorized
            })
            { StatusCode = StatusCodes.Status401Unauthorized };
        }
    }
}
