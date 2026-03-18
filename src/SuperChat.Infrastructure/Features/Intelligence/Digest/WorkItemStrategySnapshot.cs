using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Services;

internal sealed record WorkItemStrategySnapshot(
    DateTimeOffset Now,
    IReadOnlyList<WorkItemRecord> WorkItems,
    IReadOnlyList<MeetingRecord> Meetings,
    IReadOnlyDictionary<string, string> RoomNames);
