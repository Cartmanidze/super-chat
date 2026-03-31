using SuperChat.Domain.Shared;

namespace SuperChat.Domain.Features.Integrations.Matrix;

public sealed record MatrixIdentity(
    Guid UserId,
    string MatrixUserId,
    string AccessToken,
    DateTimeOffset ProvisionedAt)
{
    private readonly bool _validated = Validate(UserId, MatrixUserId, AccessToken);

    private static bool Validate(Guid userId, string matrixUserId, string accessToken)
    {
        DomainGuard.NotEmpty(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(matrixUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        return true;
    }
}
