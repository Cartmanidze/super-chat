using SuperChat.Domain.Features.Auth;

namespace SuperChat.Contracts.Features.Auth;

public enum SendCodeStatus
{
    Sent = 1,
    NotInvited = 2,
    TooManyRequests = 3,
    DeliveryFailed = 4
}

public enum AuthVerificationStatus
{
    Success = 1,
    InvalidOrExpired = 2,
    TooManyAttempts = 3
}

public sealed record SendCodeResult(
    SendCodeStatus Status,
    string Message)
{
    public bool Accepted => Status == SendCodeStatus.Sent;
}

public sealed record AuthVerificationResult
{
    public AuthVerificationStatus Status { get; }
    public string Message { get; }
    public AppUser? User { get; }
    public bool Accepted => Status == AuthVerificationStatus.Success;

    private AuthVerificationResult(AuthVerificationStatus status, string message, AppUser? user)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Status = status;
        Message = message;
        User = user;
    }

    public static AuthVerificationResult Success(string message, AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new(AuthVerificationStatus.Success, message, user);
    }

    public static AuthVerificationResult Failure(AuthVerificationStatus status, string message)
    {
        if (status == AuthVerificationStatus.Success)
            throw new ArgumentException("Failure result cannot use Success status.", nameof(status));

        return new(status, message, null);
    }
}
