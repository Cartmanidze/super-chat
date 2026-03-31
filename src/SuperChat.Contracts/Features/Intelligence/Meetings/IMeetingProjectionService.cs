namespace SuperChat.Contracts.Features.Intelligence.Meetings;

public interface IMeetingProjectionService
{
    Task<MeetingProjectionRunResult> ProjectPendingChunkMeetingsAsync(CancellationToken cancellationToken);

    Task<MeetingProjectionRunResult> ProjectConversationMeetingsAsync(
        Guid userId,
        string matrixRoomId,
        CancellationToken cancellationToken);
}

public sealed record MeetingProjectionRunResult(
    int UsersProcessed,
    int RoomsRebuilt,
    int MeetingsProjected)
{
    public static MeetingProjectionRunResult Empty { get; } = new(0, 0, 0);

    public MeetingProjectionRunResult Merge(MeetingProjectionRunResult other)
    {
        return new MeetingProjectionRunResult(
            UsersProcessed + other.UsersProcessed,
            RoomsRebuilt + other.RoomsRebuilt,
            MeetingsProjected + other.MeetingsProjected);
    }
}
