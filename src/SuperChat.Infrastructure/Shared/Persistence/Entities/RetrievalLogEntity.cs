namespace SuperChat.Infrastructure.Shared.Persistence;

internal sealed class RetrievalLogEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public string QueryKind { get; set; } = string.Empty;
    public string? FiltersJson { get; set; }
    public int CandidateCount { get; set; }
    public string? SelectedChunkIdsJson { get; set; }
    public int? LatencyMs { get; set; }
    public string? ModelVersionsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
