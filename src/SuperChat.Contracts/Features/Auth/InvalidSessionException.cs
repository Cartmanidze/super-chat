namespace SuperChat.Contracts.Features.Auth;

public enum InvalidSessionFailureReason
{
    MissingUserIdClaim,
    MalformedUserIdClaim,
    EmptyUserIdClaim
}

public sealed class InvalidSessionException(
    InvalidSessionFailureReason failureReason,
    string? userIdClaimValue = null)
    : InvalidOperationException("User session is missing or corrupted.")
{
    public InvalidSessionFailureReason FailureReason { get; } = failureReason;

    public string? UserIdClaimValue { get; } = userIdClaimValue;
}
