using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.HostedServices;

public sealed class ExtractionBackgroundService(
    IMessageNormalizationService normalizationService,
    IAiStructuredExtractionService extractionService,
    State.SuperChatStore store,
    ILogger<ExtractionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            var pendingMessages = normalizationService.GetPendingMessages();
            if (pendingMessages.Count == 0)
            {
                continue;
            }

            var processedIds = new List<Guid>(pendingMessages.Count);

            foreach (var message in pendingMessages)
            {
                var items = await extractionService.ExtractAsync(message, stoppingToken);
                store.AddExtractedItems(items);
                processedIds.Add(message.Id);
            }

            normalizationService.MarkProcessed(processedIds);
            logger.LogInformation("Processed {MessageCount} messages into extracted items.", processedIds.Count);
        }
    }
}
