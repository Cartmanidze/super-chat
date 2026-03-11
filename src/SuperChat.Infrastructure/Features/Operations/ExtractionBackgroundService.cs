using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.HostedServices;

public sealed class ExtractionBackgroundService(
    IMessageNormalizationService normalizationService,
    IAiStructuredExtractionService extractionService,
    IExtractedItemService extractedItemService,
    ILogger<ExtractionBackgroundService> logger) : BackgroundService
{
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

            var processedIds = new List<Guid>(pendingMessages.Count);

            foreach (var message in pendingMessages)
            {
                var items = await extractionService.ExtractAsync(message, stoppingToken);
                await extractedItemService.AddRangeAsync(items, stoppingToken);
                processedIds.Add(message.Id);
            }

            await normalizationService.MarkProcessedAsync(processedIds, stoppingToken);
            logger.LogInformation("Processed {MessageCount} messages into extracted items.", processedIds.Count);
        }
    }
}
