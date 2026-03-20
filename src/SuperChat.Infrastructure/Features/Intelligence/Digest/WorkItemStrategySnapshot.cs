using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Infrastructure.Features.Intelligence.Digest;

internal sealed record WorkItemStrategySnapshot(
    DateTimeOffset Now,
    IReadOnlyList<WorkItemRecord> WorkItems,
    IReadOnlyList<MeetingRecord> Meetings,
    IReadOnlyDictionary<string, string> RoomNames);
