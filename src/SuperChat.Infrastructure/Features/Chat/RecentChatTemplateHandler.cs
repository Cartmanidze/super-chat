using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Configuration;
using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class RecentChatTemplateHandler(
    IMessageNormalizationService messageNormalizationService,
    IRoomDisplayNameService roomDisplayNameService,
    TimeProvider timeProvider,
    PilotOptions pilotOptions,
    ILogger<RecentChatTemplateHandler> logger) : IChatTemplateHandler
{
    public string TemplateId => ChatPromptTemplate.Recent;

    public async Task<ChatAnswerViewModel> HandleAsync(Guid userId, string question, CancellationToken cancellationToken)
    {
        var messages = await messageNormalizationService.GetRecentMessagesAsync(userId, 80, cancellationToken);
        var timeZone = ResolveTodayTimeZone(logger, pilotOptions.TodayTimeZoneId);
        var now = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone);
        var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var nextDayStart = dayStart.AddDays(1);

        var recentToday = messages
            .Where(message =>
            {
                var localSentAt = TimeZoneInfo.ConvertTime(message.SentAt, timeZone);
                return localSentAt >= dayStart && localSentAt < nextDayStart;
            })
            .Take(8)
            .ToList();

        var roomNames = await roomDisplayNameService.ResolveManyAsync(userId, recentToday.Select(message => message.MatrixRoomId), cancellationToken);
        var items = recentToday
            .Select(message =>
            {
                var sourceRoom = roomNames.TryGetValue(message.MatrixRoomId, out var roomName)
                    ? roomName
                    : LooksLikeMatrixRoomId(message.MatrixRoomId)
                        ? string.Empty
                        : message.MatrixRoomId;

                return new ChatResultItemViewModel(
                    MessagePresentationFormatter.ResolveDisplaySenderName(message.SenderName, sourceRoom),
                    message.Text,
                    sourceRoom,
                    message.SentAt);
            })
            .ToList();

        return new ChatAnswerViewModel(TemplateId, question, items);
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
                logger.LogWarning(ex, "Configured chat time zone '{TimeZoneId}' was not found. Falling back to UTC.", configuredTimeZoneId);
            }
            catch (InvalidTimeZoneException ex)
            {
                logger.LogWarning(ex, "Configured chat time zone '{TimeZoneId}' is invalid. Falling back to UTC.", configuredTimeZoneId);
            }
        }

        return TimeZoneInfo.Utc;
    }
}
