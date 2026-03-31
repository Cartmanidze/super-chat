namespace SuperChat.Infrastructure.Shared.Persistence;

internal sealed class MatrixIdentityEntity
{
    public Guid UserId { get; set; }
    public string MatrixUserId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public DateTimeOffset ProvisionedAt { get; set; }
}
