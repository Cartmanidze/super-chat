namespace SuperChat.Infrastructure.Shared.Persistence;

internal sealed class AppUserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}
