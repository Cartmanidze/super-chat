namespace SuperChat.Contracts.Features.Intelligence.Meetings;

public sealed class MeetingProjectionOptions
{
    public const string SectionName = "MeetingProjection";

    public bool Enabled { get; set; } = true;

    public int PollSeconds { get; set; } = 15;
}
