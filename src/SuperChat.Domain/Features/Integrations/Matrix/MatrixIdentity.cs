namespace SuperChat.Domain.Model;

public sealed record MatrixIdentity(
    Guid UserId,
    string MatrixUserId,
    string AccessToken,
    DateTimeOffset ProvisionedAt);
