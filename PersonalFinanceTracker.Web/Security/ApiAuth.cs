namespace PersonalFinanceTracker.Web.Security;

/// <summary>
/// The JSON API authenticates with bearer tokens only, never with the browser cookie.
///
/// That split is deliberate: cookie-authenticated endpoints need anti-forgery (CSRF) tokens
/// because browsers attach cookies automatically to cross-site requests. A bearer token has
/// to be added by code on purpose, so a malicious site can't make the browser send it.
/// That's why API controllers can safely use [IgnoreAntiforgeryToken].
/// </summary>
public static class ApiAuth
{
    /// <summary>Name of the bearer-token authentication scheme (a const so attributes can use it).</summary>
    public const string Scheme = "Identity.Bearer";
}
