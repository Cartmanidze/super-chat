using SuperChat.Domain.Features.Integrations.Telegram;

namespace SuperChat.Infrastructure.Features.Operations.Sync;

internal static class SyncStateResolver
{
    internal static TelegramConnectionState ResolveConnectionStateAfterSuccessfulSync(
        TelegramConnectionState currentState,
        bool connected,
        bool lostConnection = false,
        TelegramConnectionState? detectedLoginStep = null)
    {
        if (connected)
        {
            return TelegramConnectionState.Connected;
        }

        // P1: Auto-reconnect when bridge reports lost connection
        if (lostConnection && currentState == TelegramConnectionState.Connected)
        {
            return TelegramConnectionState.BridgePending;
        }

        // Only apply login step transitions for connections already in chat-login flow
        if (detectedLoginStep is not null &&
            currentState >= TelegramConnectionState.LoginAwaitingPhone &&
            detectedLoginStep.Value > currentState)
        {
            return detectedLoginStep.Value;
        }

        return currentState switch
        {
            TelegramConnectionState.NotStarted => TelegramConnectionState.BridgePending,
            TelegramConnectionState.Error => TelegramConnectionState.BridgePending,
            _ => currentState
        };
    }

    internal static bool IsManagementRoom(string roomId, string? managementRoomId)
    {
        return string.Equals(roomId, managementRoomId, StringComparison.Ordinal);
    }

    internal static IReadOnlyList<string> GetInvitedRoomsToJoin(
        IReadOnlyList<string> invitedRoomIds,
        string? managementRoomId)
    {
        return invitedRoomIds
            .Where(roomId => !string.IsNullOrWhiteSpace(roomId))
            .Where(roomId => !IsManagementRoom(roomId, managementRoomId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
