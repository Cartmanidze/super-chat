using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

internal sealed class WorkItemStrategySnapshotProvider(
    IWorkItemService workItemService,
    IMeetingService meetingService,
    IRoomDisplayNameService roomDisplayNameService,
    TimeProvider timeProvider,
    PilotOptions pilotOptions,
    ILogger<WorkItemStrategySnapshotProvider> logger)
{
    public async Task<WorkItemStrategySnapshot> CreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = TimeZoneInfo.ConvertTime(
            timeProvider.GetUtcNow(),
            WorkItemTimeZoneResolver.Resolve(logger, pilotOptions.TodayTimeZoneId));

        var workItems = await workItemService.GetActiveForUserAsync(userId, cancellationToken);
        var meetings = await meetingService.GetUpcomingAsync(userId, now.AddHours(-1), 50, cancellationToken);
        var sourceRooms = workItems
            .Select(item => item.SourceRoom)
            .Concat(meetings.Select(item => item.SourceRoom));
        var roomNames = await roomDisplayNameService.ResolveManyAsync(userId, sourceRooms, cancellationToken);

        return new WorkItemStrategySnapshot(now, workItems, meetings, roomNames);
    }
}
