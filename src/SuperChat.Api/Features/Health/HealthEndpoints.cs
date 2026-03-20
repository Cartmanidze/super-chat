using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Integrations.Telegram;
using SuperChat.Contracts.Features.Intelligence.Extraction;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Operations.Health;

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
                invitedUsers = snapshot.ActiveInviteCount,
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
