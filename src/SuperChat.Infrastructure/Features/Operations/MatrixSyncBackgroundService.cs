using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.HostedServices;

public sealed class MatrixSyncBackgroundService(
    ITelegramConnectionService telegramConnectionService,
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

            var connections = await telegramConnectionService.GetReadyForDevelopmentSyncAsync(stoppingToken);
            foreach (var connection in connections)
            {
                var seeded = await SeedSampleMessagesAsync(connection.UserId, stoppingToken);
                await telegramConnectionService.MarkSynchronizedAsync(connection.UserId, timeProvider.GetUtcNow(), stoppingToken);

                if (seeded > 0)
                {
                    logger.LogInformation("Seeded {SeededCount} development messages for user {UserId}.", seeded, connection.UserId);
                }
            }
        }
    }

    private async Task<int> SeedSampleMessagesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var count = 0;

        count += await normalizationService.TryStoreAsync(
            userId,
            "!sales:matrix.localhost",
            "$evt-1",
            "Надя",
            "Пожалуйста, отправь обновлённое предложение завтра утром.",
            now.AddMinutes(-14),
            cancellationToken)
            ? 1
            : 0;

        count += await normalizationService.TryStoreAsync(
            userId,
            "!ops:matrix.localhost",
            "$evt-2",
            "Виктор",
            "У нас встреча с пилотной когортой в пятницу в 11:00.",
            now.AddMinutes(-9),
            cancellationToken)
            ? 1
            : 0;

        count += await normalizationService.TryStoreAsync(
            userId,
            "!founders:matrix.localhost",
            "$evt-3",
            "Ты",
            "Я отправлю правки по договору к концу дня.",
            now.AddMinutes(-7),
            cancellationToken)
            ? 1
            : 0;

        count += await normalizationService.TryStoreAsync(
            userId,
            "!replies:matrix.localhost",
            "$evt-4",
            "Алекс",
            "Все еще ждем ответ от Марины по hiring-плану.",
            now.AddMinutes(-3),
            cancellationToken)
            ? 1
            : 0;

        return count;
    }
}
