using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.State;

namespace SuperChat.Api.Features.Health;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/health", (
            SuperChatStore store,
            IOptions<PilotOptions> pilotOptions,
            IOptions<DeepSeekOptions> deepSeekOptions,
            IOptions<TelegramBridgeOptions> telegramOptions) =>
        {
            return Results.Json(new
            {
                status = "ok",
                demoMode = pilotOptions.Value.DevSeedSampleData,
                invitedUsers = store.AllowedEmailCount,
                knownUsers = store.KnownUserCount,
                pendingMessages = store.PendingMessageCount,
                extractedItems = store.ExtractedItemCount,
                activeSessions = store.ActiveSessionCount,
                aiModel = deepSeekOptions.Value.Model,
                bridgeBot = telegramOptions.Value.BotUserId
            });
        });

        return api;
    }
}
