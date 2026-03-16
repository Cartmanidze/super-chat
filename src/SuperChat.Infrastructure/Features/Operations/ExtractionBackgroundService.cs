using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.HostedServices;

public sealed class ExtractionBackgroundService(
    IMessageNormalizationService normalizationService,
    IAiStructuredExtractionService extractionService,
    IExtractedItemService extractedItemService,
    TimeProvider timeProvider,
    ILogger<ExtractionBackgroundService> logger) : BackgroundService
{
    internal static readonly TimeSpan DialogueGap = TimeSpan.FromMinutes(3);
    internal static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(20);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            var pendingMessages = await normalizationService.GetPendingMessagesAsync(stoppingToken);
            if (pendingMessages.Count == 0)
            {
                continue;
            }

            var readyWindows = BuildReadyConversationWindows(pendingMessages, timeProvider.GetUtcNow());
            if (readyWindows.Count == 0)
            {
                continue;
            }

            var processedIds = new List<Guid>(readyWindows.Sum(window => window.Messages.Count));

            foreach (var window in readyWindows)
            {
                var items = await extractionService.ExtractAsync(window, stoppingToken);
                await extractedItemService.AddRangeAsync(items, stoppingToken);
                processedIds.AddRange(window.Messages.Select(message => message.Id));
            }

            await normalizationService.MarkProcessedAsync(processedIds, stoppingToken);
            logger.LogInformation(
                "Processed {WindowCount} dialogue windows from {MessageCount} messages into extracted items.",
                readyWindows.Count,
                processedIds.Count);
        }
    }

    internal static IReadOnlyList<ConversationWindow> BuildReadyConversationWindows(
        IReadOnlyList<NormalizedMessage> pendingMessages,
        DateTimeOffset now)
    {
        if (pendingMessages.Count == 0)
        {
            return [];
        }

        var windows = new List<ConversationWindow>();
        foreach (var roomGroup in pendingMessages
                     .GroupBy(message => new { message.UserId, message.Source, message.MatrixRoomId }))
        {
            var orderedMessages = roomGroup
                .OrderBy(message => message.SentAt)
                .ThenBy(message => message.IngestedAt)
                .ThenBy(message => message.Id)
                .ToList();

            var buffer = new List<NormalizedMessage>();
            foreach (var message in orderedMessages)
            {
                if (buffer.Count > 0)
                {
                    var previous = buffer[^1];
                    if (message.SentAt - previous.SentAt > DialogueGap)
                    {
                        TryAddWindow(buffer, now, windows);
                        buffer.Clear();
                    }
                }

                buffer.Add(message);
            }

            TryAddWindow(buffer, now, windows);
        }

        return windows;
    }

    private static void TryAddWindow(
        IReadOnlyList<NormalizedMessage> messages,
        DateTimeOffset now,
        ICollection<ConversationWindow> windows)
    {
        if (messages.Count == 0)
        {
            return;
        }

        var lastMessage = messages[^1];
        if (now - lastMessage.IngestedAt < SettleDelay)
        {
            return;
        }

        windows.Add(new ConversationWindow(
            lastMessage.UserId,
            lastMessage.Source,
            lastMessage.MatrixRoomId,
            messages.ToList()));
    }
}
