using SuperChat.Domain.Features.Auth;

namespace SuperChat.Infrastructure.Features.Auth;

public interface IAuthFlowService
{
    Task<MagicLinkRequestResult> RequestMagicLinkAsync(string email, CancellationToken cancellationToken);

    Task<AuthVerificationResult> VerifyAsync(string token, CancellationToken cancellationToken);

    Task<AppUser?> FindUserAsync(string email, CancellationToken cancellationToken);
}
