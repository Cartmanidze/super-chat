using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Configuration;
using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Services;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class DigestService(
    IExtractedItemService extractedItemService,
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

        return cards;
    }

    public async Task<IReadOnlyList<DashboardCardViewModel>> GetWaitingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var items = await extractedItemService.GetForUserAsync(userId, cancellationToken);
        var cards = DigestComposer.BuildWaiting(items)
            .Select(item => new DashboardCardViewModel(item.Title, item.Summary, item.Kind.ToString(), item.DueAt, item.SourceRoom))
            .ToList();

        return cards;
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
