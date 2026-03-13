using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.HostedServices;

public sealed class ChunkBuilderBackgroundService(
    IChunkBuilderService chunkBuilderService,
    IOptions<ChunkingOptions> chunkingOptions,
    ILogger<ChunkBuilderBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = chunkingOptions.Value;
        if (!options.Enabled)
        {
            logger.LogInformation("Chunk builder is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.PollSeconds)));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var result = await chunkBuilderService.BuildPendingChunksAsync(stoppingToken);
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
                logger.LogWarning(exception, "Chunk builder tick failed.");
            }
        }
    }
}
