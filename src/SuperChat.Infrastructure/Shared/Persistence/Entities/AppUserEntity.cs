namespace SuperChat.Infrastructure.Shared.Persistence;

internal sealed class AppUserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? TimeZoneId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}
