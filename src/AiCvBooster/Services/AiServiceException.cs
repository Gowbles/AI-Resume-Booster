namespace AiCvBooster.Services;

/// <summary>
/// Reason taxonomy for a failed AI call. Used by the UI to pick an icon / tone,
/// and by the service layer to decide whether a retry makes sense.
/// </summary>
public enum AiFailureKind
{
    /// <summary>Caller cancelled (e.g. user hit "Cancel" in the loading view).</summary>
    Cancelled,
    /// <summary>HTTP timeout or underlying socket problem.</summary>
    Network,
    /// <summary>Server responded 429 / quota exhausted.</summary>
    RateLimited,
    /// <summary>API key missing, invalid, or revoked (401/403).</summary>
    Authentication,
    /// <summary>Upstream 5xx.</summary>
    ServerError,
    /// <summary>Response body did not match the expected shape.</summary>
    InvalidResponse,
    /// <summary>Caller sent bad input (e.g. empty CV).</summary>
    InvalidRequest,
    /// <summary>Anything else.</summary>
    Unknown
}

/// <summary>
/// A single exception type the UI can catch to display a clean, user-friendly
/// error — without ever leaking raw JSON / stack traces. The technical detail
/// is kept on <see cref="TechnicalDetail"/> for logging.
/// </summary>
public sealed class AiServiceException : Exception
{
    public AiFailureKind Kind { get; }
    public string FriendlyMessage { get; }
    public string? TechnicalDetail { get; }
    public bool IsRetryable { get; }

    public AiServiceException(
        AiFailureKind kind,
        string friendlyMessage,
        string? technicalDetail = null,
        bool isRetryable = false,
        Exception? inner = null)
        : base(friendlyMessage, inner)
    {
        Kind = kind;
        FriendlyMessage = friendlyMessage;
        TechnicalDetail = technicalDetail;
        IsRetryable = isRetryable;
    }
}
