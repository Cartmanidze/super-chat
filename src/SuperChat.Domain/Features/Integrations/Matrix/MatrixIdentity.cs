namespace SuperChat.Domain.Features.Integrations.Matrix;

public sealed record MatrixIdentity(
    Guid UserId,
    string MatrixUserId,
    string AccessToken,
    DateTimeOffset ProvisionedAt);
