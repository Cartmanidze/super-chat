using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Api.Features.Health;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/health", async (
            IHealthSnapshotService healthSnapshotService,
            IOptions<PilotOptions> pilotOptions,
            IOptions<DeepSeekOptions> deepSeekOptions,
            IOptions<TelegramBridgeOptions> telegramOptions,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await healthSnapshotService.GetAsync(cancellationToken);

            return Results.Json(new
            {
                status = "ok",
                demoMode = pilotOptions.Value.DevSeedSampleData,
                invitedUsers = snapshot.AllowedEmailCount,
                knownUsers = snapshot.KnownUserCount,
                pendingMessages = snapshot.PendingMessageCount,
                extractedItems = snapshot.ExtractedItemCount,
                activeSessions = snapshot.ActiveSessionCount,
                aiModel = deepSeekOptions.Value.Model,
                bridgeBot = telegramOptions.Value.BotUserId
            });
        });

        return api;
    }
}
