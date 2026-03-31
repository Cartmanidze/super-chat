namespace SuperChat.Domain.Features.Intelligence;

public sealed record MeetingSignal(
    string Title,
    string Summary,
    string? Person,
    DateTimeOffset ObservedAt,
    DateTimeOffset ScheduledFor,
    Confidence Confidence)
{
    private readonly bool _validated = Validate(Title, Summary);

    private static bool Validate(string title, string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        return true;
    }
}
