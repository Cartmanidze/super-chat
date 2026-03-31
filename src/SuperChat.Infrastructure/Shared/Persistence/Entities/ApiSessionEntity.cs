namespace SuperChat.Infrastructure.Shared.Persistence;

internal sealed class ApiSessionEntity
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
