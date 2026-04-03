using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;

namespace SuperChat.Infrastructure.Features.Operations;

internal sealed class DueMeetingsSweepBackgroundService(
    MeetingAutoResolutionService meetingAutoResolutionService,
    IOptions<ResolutionOptions> resolutionOptions,
    TimeProvider timeProvider,
    ILogger<DueMeetingsSweepBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = resolutionOptions.Value;
        if (!options.Enabled || !options.EnableDueMeetingsSweep)
        {
            logger.LogInformation(
                "Due meeting sweep background service is disabled. ResolutionEnabled={ResolutionEnabled}, SweepEnabled={SweepEnabled}.",
                options.Enabled,
                options.EnableDueMeetingsSweep);
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, options.DueMeetingsSweepMinutes));
        logger.LogInformation(
            "Starting due meeting sweep background service. IntervalMinutes={IntervalMinutes}, MeetingGracePeriodMinutes={MeetingGracePeriodMinutes}.",
            interval.TotalMinutes,
            options.MeetingGracePeriodMinutes);

        await SweepAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SweepAsync(stoppingToken);
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        var options = resolutionOptions.Value;
        var resolveBefore = timeProvider.GetUtcNow().AddMinutes(-Math.Max(0, options.MeetingGracePeriodMinutes));

        logger.LogInformation(
            "Running due meeting sweep. ResolveBefore={ResolveBefore}.",
            resolveBefore);

        await meetingAutoResolutionService.ResolveDueMeetingsAsync(resolveBefore, cancellationToken);
    }
}
