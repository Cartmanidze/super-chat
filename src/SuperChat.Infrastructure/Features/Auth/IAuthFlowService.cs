using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Abstractions;

public interface IAuthFlowService
{
    Task<MagicLinkRequestResult> RequestMagicLinkAsync(string email, CancellationToken cancellationToken);

    Task<AuthVerificationResult> VerifyAsync(string token, CancellationToken cancellationToken);

    Task<AppUser?> FindUserAsync(string email, CancellationToken cancellationToken);
}
