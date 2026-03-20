using SuperChat.Domain.Features.Auth;

namespace SuperChat.Infrastructure.Features.Auth;

public enum MagicLinkRequestStatus
{
    Created = 1,
    NotInvited = 2
}

public enum AuthVerificationStatus
{
    Success = 1,
    InvalidOrExpired = 2
}

public sealed record MagicLinkRequestResult(
    bool Accepted,
    MagicLinkRequestStatus Status,
    string Message,
    Uri? DevelopmentLink);

public sealed record AuthVerificationResult(
    bool Accepted,
    AuthVerificationStatus Status,
    string Message,
    AppUser? User);
