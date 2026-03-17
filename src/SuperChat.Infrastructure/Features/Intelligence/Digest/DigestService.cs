using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Configuration;
using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;
using SuperChat.Domain.Services;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class DigestService(
    IExtractedItemService extractedItemService,
    IMeetingService meetingService,
    IRoomDisplayNameService roomDisplayNameService,
    TimeProvider timeProvider,
    PilotOptions pilotOptions,
    ILogger<DigestService> logger) : IDigestService
{
    public async Task<IReadOnlyList<WorkItemCardViewModel>> GetTodayAsync(Guid userId, CancellationToken cancellationToken)
    {
        var items = await extractedItemService.GetForUserAsync(userId, cancellationToken);
        var now = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), ResolveTodayTimeZone(logger, pilotOptions.TodayTimeZoneId));
        var cards = DigestComposer.BuildToday(items, now)
            .Select(item => item.ToWorkItemCardViewModel(now))
            .ToList();

        return await ResolveRoomNamesAsync(userId, cards, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkItemCardViewModel>> GetWaitingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var items = await extractedItemService.GetForUserAsync(userId, cancellationToken);
        var now = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), ResolveTodayTimeZone(logger, pilotOptions.TodayTimeZoneId));
        var cards = DigestComposer.BuildWaiting(items)
            .Select(item => item.ToWorkItemCardViewModel(now))
            .ToList();

        return await ResolveRoomNamesAsync(userId, cards, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkItemCardViewModel>> GetMeetingsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), ResolveTodayTimeZone(logger, pilotOptions.TodayTimeZoneId));
        var meetings = await meetingService.GetUpcomingAsync(userId, now.AddHours(-1), 20, cancellationToken);
        var cards = DigestComposer.BuildMeetings(meetings, now)
            .Select(item => item.ToWorkItemCardViewModel(now))
            .ToList();

        return await ResolveRoomNamesAsync(userId, cards, cancellationToken);
    }

    private async Task<IReadOnlyList<WorkItemCardViewModel>> ResolveRoomNamesAsync(
        Guid userId,
        IReadOnlyList<WorkItemCardViewModel> cards,
        CancellationToken cancellationToken)
    {
        var roomNames = await roomDisplayNameService.ResolveManyAsync(userId, cards.Select(item => item.SourceRoom), cancellationToken);

        return cards
            .Select(card => card.WithResolvedSourceRoom(roomNames))
            .ToList();
    }

    private static TimeZoneInfo ResolveTodayTimeZone(ILogger logger, string configuredTimeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(configuredTimeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(configuredTimeZoneId);
            }
            catch (TimeZoneNotFoundException ex)
            {
                logger.LogWarning(ex, "Configured Today time zone '{TimeZoneId}' was not found. Falling back to UTC.", configuredTimeZoneId);
            }
            catch (InvalidTimeZoneException ex)
            {
                logger.LogWarning(ex, "Configured Today time zone '{TimeZoneId}' is invalid. Falling back to UTC.", configuredTimeZoneId);
            }
        }

        return TimeZoneInfo.Utc;
    }
}
