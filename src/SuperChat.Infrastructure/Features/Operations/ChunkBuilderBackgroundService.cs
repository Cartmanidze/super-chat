using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.HostedServices;

public sealed class ChunkBuilderBackgroundService(
    IChunkBuilderService chunkBuilderService,
    IOptions<ChunkingOptions> chunkingOptions,
    IWorkerRuntimeMonitor workerRuntimeMonitor,
    ILogger<ChunkBuilderBackgroundService> logger) : BackgroundService
{
    private const string WorkerKey = "chunk-builder";
    private const string WorkerDisplayName = "Chunk Builder";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        workerRuntimeMonitor.RegisterWorker(WorkerKey, WorkerDisplayName);
        var options = chunkingOptions.Value;
        if (!options.Enabled)
        {
            logger.LogInformation("Chunk builder is disabled.");
            workerRuntimeMonitor.MarkDisabled(WorkerKey, WorkerDisplayName, "Chunking is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.PollSeconds)));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                workerRuntimeMonitor.MarkRunning(WorkerKey, WorkerDisplayName);
                var result = await chunkBuilderService.BuildPendingChunksAsync(stoppingToken);
                workerRuntimeMonitor.MarkSucceeded(
                    WorkerKey,
                    WorkerDisplayName,
                    $"Users={result.UsersProcessed}, Rooms={result.RoomsRebuilt}, Chunks={result.ChunksWritten}, Messages={result.MessagesConsidered}");
                if (result.RoomsRebuilt > 0 || result.ChunksWritten > 0)
                {
                    logger.LogInformation(
                        "Chunk builder processed {UserCount} users, rebuilt {RoomCount} rooms, and wrote {ChunkCount} chunks from {MessageCount} messages.",
                        result.UsersProcessed,
                        result.RoomsRebuilt,
                        result.ChunksWritten,
                        result.MessagesConsidered);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                workerRuntimeMonitor.MarkFailed(WorkerKey, WorkerDisplayName, exception);
                logger.LogWarning(exception, "Chunk builder tick failed.");
            }
        }
    }
}
