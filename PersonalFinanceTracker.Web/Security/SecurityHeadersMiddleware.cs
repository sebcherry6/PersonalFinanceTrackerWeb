namespace PersonalFinanceTracker.Web.Security;

/// <summary>
/// Adds browser security headers to every response.
///
/// The big one is the Content-Security-Policy (CSP). It tells the browser to only run
/// scripts and load styles that come from this site's own files - no inline &lt;script&gt;,
/// no inline style="", no onclick="" and nothing from other domains. If an attacker ever
/// managed to get HTML into a page, the browser would refuse to run it.
///
/// That's why this app has no inline scripts, no CDN links (Bootstrap and Chart.js were
/// loaded from jsdelivr before) and draws its charts as SVG with plain attributes.
/// If you add a script, put it in wwwroot/js and reference it with &lt;script src&gt;.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers.XContentTypeOptions = "nosniff";             // don't guess file types
        headers.XFrameOptions = "DENY";                       // no clickjacking via iframes (older browsers)
        headers["Referrer-Policy"] = "same-origin";          // don't leak URLs to other sites
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";

        return next(context);
    }
}

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
