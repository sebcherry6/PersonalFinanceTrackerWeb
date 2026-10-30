using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersonalFinanceTracker.Web.Security;
using PersonalFinanceTracker.Web.Services;

namespace PersonalFinanceTracker.Web.Controllers.Api;

/// <summary>
/// POST /api/auth/token  { "email": "...", "password": "..." }
///   200 -> { "tokenType": "Bearer", "accessToken": "...", "expiresIn": 3600, "refreshToken": "..." }
///   401 -> wrong email/password, or account temporarily locked
///
/// Same lockout, timing protection and rate limit as the web sign-in form.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[IgnoreAntiforgeryToken]   // no cookies involved; see Security/ApiAuth.cs
public sealed class AuthApiController(AccountSecurity account) : ControllerBase
{
    [HttpPost("token")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Token([FromBody] TokenRequest request)
    {
        var result = await account.PasswordSignInAsync(request.Email.Trim(), request.Password, rememberMe: false, ApiAuth.Scheme);

        // On success the bearer-token handler has already written the token JSON to the
        // response during sign-in, so there's nothing more to send.
        if (result.Succeeded) return new EmptyResult();

        return Problem(
            title: "Sign-in failed",
            detail: "Email or password is incorrect, or the account is temporarily locked.",
            statusCode: StatusCodes.Status401Unauthorized);
    }
}
