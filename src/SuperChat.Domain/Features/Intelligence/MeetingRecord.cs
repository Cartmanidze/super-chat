namespace SuperChat.Domain.Model;

public sealed record MeetingRecord(
    Guid Id,
    Guid UserId,
    string Title,
    string Summary,
    string SourceRoom,
    string SourceEventId,
    string? Person,
    DateTimeOffset ObservedAt,
    DateTimeOffset ScheduledFor,
    double Confidence,
    string? MeetingProvider = null,
    Uri? MeetingJoinUrl = null);
