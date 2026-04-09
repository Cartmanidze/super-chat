using SuperChat.Domain.Features.Auth;

namespace SuperChat.Contracts.Features.Auth;

public interface IAuthFlowService
{
    Task<SendCodeResult> SendCodeAsync(string email, CancellationToken cancellationToken);

    Task<AuthVerificationResult> VerifyCodeAsync(string email, string code, CancellationToken cancellationToken);

    Task<AuthVerificationResult> VerifyCodeAsync(string email, string code, string? timeZoneId, CancellationToken cancellationToken);

    Task<AppUser?> FindUserAsync(string email, CancellationToken cancellationToken);
}
