using SuperChat.Domain.Features.Integrations.Telegram;

namespace SuperChat.Infrastructure.Features.Operations.Sync;

internal static class SyncStateResolver
{
    internal static TelegramConnectionState ResolveConnectionStateAfterSuccessfulSync(
        TelegramConnectionState currentState,
        bool managementConnected,
        bool chatConnected = false,
        bool lostConnection = false,
        TelegramConnectionState? detectedLoginStep = null)
    {
        if (managementConnected)
        {
            return TelegramConnectionState.Connected;
        }

        // P1: Auto-reconnect when bridge reports lost connection
        if (lostConnection && currentState == TelegramConnectionState.Connected)
        {
            return TelegramConnectionState.BridgePending;
        }

        var isLoginFlow =
            currentState >= TelegramConnectionState.LoginAwaitingPhone &&
            currentState <= TelegramConnectionState.LoginAwaitingPassword;

        // While login is still waiting for phone/code/password, ordinary chat messages
        // must not flip the connection to Connected before the management room confirms it.
        if (isLoginFlow)
        {
            if (detectedLoginStep is not null && detectedLoginStep.Value > currentState)
            {
                return detectedLoginStep.Value;
            }

            return currentState;
        }

        if (chatConnected)
        {
            return TelegramConnectionState.Connected;
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
