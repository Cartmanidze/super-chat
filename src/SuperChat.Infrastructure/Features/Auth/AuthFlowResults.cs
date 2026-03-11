using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Abstractions;

public sealed record MagicLinkRequestResult(bool Accepted, string Message, Uri? DevelopmentLink);

public sealed record AuthVerificationResult(bool Accepted, string Message, AppUser? User);
