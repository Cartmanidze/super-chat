using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.HostedServices;

public sealed class MeetingProjectionBackgroundService(
    IMeetingProjectionService meetingProjectionService,
    IOptions<MeetingProjectionOptions> meetingProjectionOptions,
    IWorkerRuntimeMonitor workerRuntimeMonitor,
    ILogger<MeetingProjectionBackgroundService> logger) : BackgroundService
{
    private const string WorkerKey = "meeting-projection";
    private const string WorkerDisplayName = "Meeting Projection";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        workerRuntimeMonitor.RegisterWorker(WorkerKey, WorkerDisplayName);
        var options = meetingProjectionOptions.Value;
        if (!options.Enabled)
        {
            logger.LogInformation("Meeting projection is disabled.");
            workerRuntimeMonitor.MarkDisabled(WorkerKey, WorkerDisplayName, "Meeting projection is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.PollSeconds)));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                workerRuntimeMonitor.MarkRunning(WorkerKey, WorkerDisplayName);
                var result = await meetingProjectionService.ProjectPendingChunkMeetingsAsync(stoppingToken);
                workerRuntimeMonitor.MarkSucceeded(
                    WorkerKey,
                    WorkerDisplayName,
                    $"Users={result.UsersProcessed}, Rooms={result.RoomsRebuilt}, Meetings={result.MeetingsProjected}");
                if (result.RoomsRebuilt > 0 || result.MeetingsProjected > 0)
                {
                    logger.LogInformation(
                        "Meeting projection processed {UserCount} users, rebuilt {RoomCount} rooms, and projected {MeetingCount} meetings from chunks.",
                        result.UsersProcessed,
                        result.RoomsRebuilt,
                        result.MeetingsProjected);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                workerRuntimeMonitor.MarkFailed(WorkerKey, WorkerDisplayName, exception);
                logger.LogWarning(exception, "Meeting projection tick failed.");
            }
        }
    }
}
