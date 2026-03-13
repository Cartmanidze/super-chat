using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.HostedServices;

public sealed class MeetingProjectionBackgroundService(
    IMeetingProjectionService meetingProjectionService,
    IOptions<MeetingProjectionOptions> meetingProjectionOptions,
    ILogger<MeetingProjectionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = meetingProjectionOptions.Value;
        if (!options.Enabled)
        {
            logger.LogInformation("Meeting projection is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.PollSeconds)));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var result = await meetingProjectionService.ProjectPendingChunkMeetingsAsync(stoppingToken);
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
                logger.LogWarning(exception, "Meeting projection tick failed.");
            }
        }
    }
}
