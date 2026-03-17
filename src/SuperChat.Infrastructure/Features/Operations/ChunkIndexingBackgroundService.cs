using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.HostedServices;

public sealed class ChunkIndexingBackgroundService(
    IChunkIndexingService chunkIndexingService,
    IOptions<ChunkIndexingOptions> chunkIndexingOptions,
    IWorkerRuntimeMonitor workerRuntimeMonitor,
    ILogger<ChunkIndexingBackgroundService> logger) : BackgroundService
{
    private const string WorkerKey = "chunk-indexing";
    private const string WorkerDisplayName = "Chunk Indexing";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        workerRuntimeMonitor.RegisterWorker(WorkerKey, WorkerDisplayName);
        var options = chunkIndexingOptions.Value;
        if (!options.Enabled)
        {
            logger.LogInformation("Chunk indexing is disabled.");
            workerRuntimeMonitor.MarkDisabled(WorkerKey, WorkerDisplayName, "Chunk indexing is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.PollSeconds)));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                workerRuntimeMonitor.MarkRunning(WorkerKey, WorkerDisplayName);
                var result = await chunkIndexingService.IndexPendingChunksAsync(stoppingToken);
                workerRuntimeMonitor.MarkSucceeded(
                    WorkerKey,
                    WorkerDisplayName,
                    $"Selected={result.ChunksSelected}, Indexed={result.ChunksIndexed}");
                if (result.ChunksIndexed > 0)
                {
                    logger.LogInformation(
                        "Chunk indexer selected {SelectedChunkCount} chunks and indexed {IndexedChunkCount} chunks into Qdrant.",
                        result.ChunksSelected,
                        result.ChunksIndexed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                workerRuntimeMonitor.MarkFailed(WorkerKey, WorkerDisplayName, exception);
                logger.LogWarning(exception, "Chunk indexing tick failed.");
            }
        }
    }
}
