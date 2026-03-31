namespace SuperChat.Infrastructure.Shared.Persistence;

internal sealed class MeetingEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SourceRoom { get; set; } = string.Empty;
    public string SourceEventId { get; set; } = string.Empty;
    public string? Person { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public DateTimeOffset ScheduledFor { get; set; }
    public double Confidence { get; set; }
    public string? MeetingProvider { get; set; }
    public string? MeetingJoinUrl { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolutionKind { get; set; }
    public string? ResolutionSource { get; set; }
    public double? ResolutionConfidence { get; set; }
    public string? ResolutionModel { get; set; }
    public string? ResolutionEvidenceJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
