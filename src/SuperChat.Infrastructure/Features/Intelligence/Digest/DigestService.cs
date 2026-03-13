using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Configuration;
using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Services;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class DigestService(
    IExtractedItemService extractedItemService,
    IRoomDisplayNameService roomDisplayNameService,
    TimeProvider timeProvider,
    PilotOptions pilotOptions,
    ILogger<DigestService> logger) : IDigestService
{
    public async Task<IReadOnlyList<DashboardCardViewModel>> GetTodayAsync(Guid userId, CancellationToken cancellationToken)
    {
        var items = await extractedItemService.GetForUserAsync(userId, cancellationToken);
        var now = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), ResolveTodayTimeZone(logger, pilotOptions.TodayTimeZoneId));
        var cards = DigestComposer.BuildToday(items, now)
            .Select(item => new DashboardCardViewModel(item.Title, item.Summary, item.Kind.ToString(), item.DueAt, item.SourceRoom))
            .ToList();

        return await ResolveRoomNamesAsync(userId, cards, cancellationToken);
    }

    public async Task<IReadOnlyList<DashboardCardViewModel>> GetWaitingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var items = await extractedItemService.GetForUserAsync(userId, cancellationToken);
        var cards = DigestComposer.BuildWaiting(items)
            .Select(item => new DashboardCardViewModel(item.Title, item.Summary, item.Kind.ToString(), item.DueAt, item.SourceRoom))
            .ToList();

        return await ResolveRoomNamesAsync(userId, cards, cancellationToken);
    }

    private async Task<IReadOnlyList<DashboardCardViewModel>> ResolveRoomNamesAsync(
        Guid userId,
        IReadOnlyList<DashboardCardViewModel> cards,
        CancellationToken cancellationToken)
    {
        var roomNames = await roomDisplayNameService.ResolveManyAsync(userId, cards.Select(item => item.SourceRoom), cancellationToken);

        return cards
            .Select(card =>
            {
                if (roomNames.TryGetValue(card.SourceRoom, out var roomName))
                {
                    return card with { SourceRoom = roomName };
                }

                return LooksLikeMatrixRoomId(card.SourceRoom)
                    ? card with { SourceRoom = string.Empty }
                    : card;
            })
            .ToList();
    }

    private static bool LooksLikeMatrixRoomId(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.StartsWith("!", StringComparison.Ordinal) &&
               value.Contains(':', StringComparison.Ordinal);
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
