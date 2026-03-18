using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Services;

internal sealed record WorkItemStrategySnapshot(
    DateTimeOffset Now,
    IReadOnlyList<ExtractedItem> ExtractedItems,
    IReadOnlyList<MeetingRecord> Meetings,
    IReadOnlyDictionary<string, string> RoomNames);
