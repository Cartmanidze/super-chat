namespace SuperChat.Domain.Features.Intelligence;

public sealed record MeetingSignal(
    string Title,
    string Summary,
    string? Person,
    DateTimeOffset ObservedAt,
    DateTimeOffset ScheduledFor,
    double Confidence);
