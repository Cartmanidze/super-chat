using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Abstractions;

public interface IMatrixProvisioningService
{
    Task<MatrixIdentity> EnsureIdentityAsync(AppUser user, CancellationToken cancellationToken);
}
