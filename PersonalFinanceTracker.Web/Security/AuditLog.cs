namespace PersonalFinanceTracker.Web.Security;

/// <summary>
/// Security events, written to the normal app log with fixed event IDs (1000-1099) so they're
/// easy to filter and alert on in whatever log viewer the host gives you
/// (e.g. Azure Log Analytics: EventId between 1000 and 1099).
///
/// Rules I stick to here: log the user ID (a GUID), never the password, never amounts or
/// descriptions, and never the email on failed sign-ins (that would turn the log into a
/// list of who has an account).
/// </summary>
public static partial class AuditLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Audit: sign-in succeeded for user {UserId} via {Channel}")]
    public static partial void SignInSucceeded(ILogger logger, string userId, string channel);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Audit: sign-in failed via {Channel} from {RemoteIp}")]
    public static partial void SignInFailed(ILogger logger, string channel, string? remoteIp);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Audit: sign-in blocked by lockout for user {UserId} via {Channel} from {RemoteIp}")]
    public static partial void SignInLockedOut(ILogger logger, string userId, string channel, string? remoteIp);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Information, Message = "Audit: account created for user {UserId}")]
    public static partial void AccountCreated(ILogger logger, string userId);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Information, Message = "Audit: password changed for user {UserId}")]
    public static partial void PasswordChanged(ILogger logger, string userId);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Information, Message = "Audit: user {UserId} signed out of all devices")]
    public static partial void SignedOutEverywhere(ILogger logger, string userId);

    [LoggerMessage(EventId = 1013, Level = LogLevel.Warning, Message = "Audit: account deleted for user {UserId}")]
    public static partial void AccountDeleted(ILogger logger, string userId);

    [LoggerMessage(EventId = 1020, Level = LogLevel.Warning, Message = "Audit: stale API token rejected for user {UserId}")]
    public static partial void StaleApiTokenRejected(ILogger logger, string userId);

    [LoggerMessage(EventId = 1030, Level = LogLevel.Warning, Message = "Audit: ReverseProxy is enabled but no KnownProxies/KnownNetworks are set, so X-Forwarded-For is trusted from any source")]
    public static partial void ProxyNotRestricted(ILogger logger);
}
