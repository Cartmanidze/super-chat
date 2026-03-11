using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.HostedServices;

public sealed class MatrixSyncBackgroundService(
    State.SuperChatStore store,
    IMessageNormalizationService normalizationService,
    IOptions<PilotOptions> pilotOptions,
    TimeProvider timeProvider,
    ILogger<MatrixSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(4));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!pilotOptions.Value.DevSeedSampleData)
            {
                continue;
            }

            foreach (var connection in store.GetConnectionsReadyForDevelopmentSync())
            {
                var seeded = SeedSampleMessages(connection.UserId);
                if (seeded > 0)
                {
                    logger.LogInformation("Seeded {SeededCount} development messages for user {UserId}.", seeded, connection.UserId);
                }
            }
        }
    }

    private int SeedSampleMessages(Guid userId)
    {
        var now = timeProvider.GetUtcNow();
        var count = 0;

        count += normalizationService.TryStore(
            userId,
            "!sales:matrix.localhost",
            "$evt-1",
            "Надя",
            "Пожалуйста, отправь обновлённое предложение завтра утром.",
            now.AddMinutes(-14))
            ? 1
            : 0;

        count += normalizationService.TryStore(
            userId,
            "!ops:matrix.localhost",
            "$evt-2",
            "Виктор",
            "У нас встреча с пилотной когортой в пятницу в 11:00.",
            now.AddMinutes(-9))
            ? 1
            : 0;

        count += normalizationService.TryStore(
            userId,
            "!founders:matrix.localhost",
            "$evt-3",
            "Ты",
            "Я отправлю правки по договору к концу дня.",
            now.AddMinutes(-7))
            ? 1
            : 0;

        count += normalizationService.TryStore(
            userId,
            "!replies:matrix.localhost",
            "$evt-4",
            "Алекс",
            "Все ещё ждём ответ от Марины по hiring-плану.",
            now.AddMinutes(-3))
            ? 1
            : 0;

        if (count > 0)
        {
            store.MarkDemoSeeded(userId, now);
        }

        return count;
    }
}
