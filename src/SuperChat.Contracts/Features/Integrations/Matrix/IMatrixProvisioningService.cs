using SuperChat.Domain.Features.Auth;
using SuperChat.Domain.Features.Integrations.Matrix;

namespace SuperChat.Contracts.Features.Integrations.Matrix;

public interface IMatrixProvisioningService
{
    Task<MatrixIdentity> EnsureIdentityAsync(AppUser user, CancellationToken cancellationToken);

    Task<MatrixIdentity?> GetIdentityAsync(Guid userId, CancellationToken cancellationToken);
}
