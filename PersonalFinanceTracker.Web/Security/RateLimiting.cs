using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace PersonalFinanceTracker.Web.Security;

/// <summary>Names of the rate-limit policies used with [EnableRateLimiting(...)].</summary>
public static class RateLimitPolicies
{
    /// <summary>Sign-in, registration and token requests: slows down password guessing.</summary>
    public const string Auth = "auth";

    /// <summary>JSON API calls.</summary>
    public const string Api = "api";
}

/// <summary>Bound from the "RateLimiting" section of appsettings.json.</summary>
public sealed class RateLimitSettings
{
    public int AuthPermitLimit { get; set; } = 10;
    public int ApiPermitLimit { get; set; } = 120;
}

public static class RateLimitingSetup
{
    /// <summary>
    /// Requests are counted per client IP in fixed one-minute windows. Going over returns
    /// 429 Too Many Requests.
    ///
    /// Behind a reverse proxy / load balancer every request appears to come from the proxy's
    /// IP, so everyone would share one bucket. Turn on "ReverseProxy:Enabled" in that case
    /// (see Program.cs) so the real client IP is used.
    ///
    /// Limits are read per request from IOptions so tests and environments can override them.
    /// </summary>
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<RateLimitSettings>(config.GetSection("RateLimiting"));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(RateLimitPolicies.Auth, context => FixedWindowPerIp(
                context, s => s.AuthPermitLimit));

            options.AddPolicy(RateLimitPolicies.Api, context => FixedWindowPerIp(
                context, s => s.ApiPermitLimit));
        });

        return services;
    }

    private static RateLimitPartition<string> FixedWindowPerIp(HttpContext context, Func<RateLimitSettings, int> limit)
    {
        var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit(settings),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    }
}
