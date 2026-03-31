namespace SuperChat.Infrastructure.Shared.Persistence;

internal sealed class VerificationCodeEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public string CodeSalt { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Consumed { get; set; }
    public Guid? ConsumedByUserId { get; set; }
    public int FailedAttempts { get; set; }
}
