namespace SuperChat.Domain.Features.Integrations.Matrix;

public interface IMatrixIdentityRepository
{
    Task<MatrixIdentity?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task SaveAsync(MatrixIdentity identity, CancellationToken cancellationToken);
}
