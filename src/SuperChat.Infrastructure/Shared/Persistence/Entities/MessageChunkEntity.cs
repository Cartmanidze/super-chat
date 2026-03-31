namespace SuperChat.Infrastructure.Shared.Persistence;

internal sealed class MessageChunkEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Transport { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public string? PeerId { get; set; }
    public string? ThreadId { get; set; }
    public string Kind { get; set; } = "dialog_chunk";
    public string Text { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public Guid? FirstNormalizedMessageId { get; set; }
    public Guid? LastNormalizedMessageId { get; set; }
    public DateTimeOffset TsFrom { get; set; }
    public DateTimeOffset TsTo { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public int ChunkVersion { get; set; } = 1;
    public string? EmbeddingVersion { get; set; }
    public string? QdrantPointId { get; set; }
    public DateTimeOffset? IndexedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
