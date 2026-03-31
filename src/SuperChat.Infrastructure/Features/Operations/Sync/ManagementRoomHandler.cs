using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Features.Integrations.Matrix;

namespace SuperChat.Infrastructure.Features.Operations.Sync;

internal sealed record ManagementRoomResult(
    string? DiscoveredLoginUrl,
    bool Connected,
    bool LostConnection,
    TelegramConnectionState? DetectedLoginStep,
    bool SawBridgeGreeting);

internal static class ManagementRoomHandler
{
    public static ManagementRoomResult Process(
        IReadOnlyList<MatrixTimelineEvent> events,
        string botUserId,
        Func<string, Uri?> urlExtractor)
    {
        string? discoveredLoginUrl = null;
        bool connected = false;
        bool lostConnection = false;
        TelegramConnectionState? detectedLoginStep = null;
        bool sawBridgeGreeting = false;

        foreach (var evt in events)
        {
            if (!string.Equals(evt.Sender, botUserId, StringComparison.Ordinal))
                continue;

            discoveredLoginUrl ??= urlExtractor(evt.Body)?.ToString();
            sawBridgeGreeting = sawBridgeGreeting || LooksLikeBridgeGreeting(evt.Body);

            if (LooksLikeSuccessfulLogin(evt.Body))
            {
                connected = true;
                lostConnection = false;
            }
            else if (LooksLikeLostConnection(evt.Body))
            {
                connected = false;
                lostConnection = true;
            }

            var stepCandidate = DetectLoginStep(evt.Body);
            if (stepCandidate is not null &&
                (detectedLoginStep is null || stepCandidate.Value > detectedLoginStep.Value))
            {
                detectedLoginStep = stepCandidate;
            }
        }

        return new ManagementRoomResult(discoveredLoginUrl, connected, lostConnection, detectedLoginStep, sawBridgeGreeting);
    }

    internal static bool LooksLikeSuccessfulLogin(string message)
    {
        var normalized = message.ToLowerInvariant();
        if (normalized.Contains(BridgeMessagePatterns.NotLoggedInIndicators[0], StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var indicator in BridgeMessagePatterns.SuccessfulLoginIndicators)
        {
            if (normalized.Contains(indicator, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool LooksLikeBridgeGreeting(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.ToLowerInvariant();
        foreach (var indicator in BridgeMessagePatterns.GreetingIndicators)
        {
            if (normalized.Contains(indicator, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return normalized.Contains(BridgeMessagePatterns.GreetingBridgePhrase, StringComparison.Ordinal) &&
               normalized.Contains(BridgeMessagePatterns.GreetingHelloPhrase, StringComparison.Ordinal);
    }

    internal static bool LooksLikeLostConnection(string message)
    {
        var normalized = message.ToLowerInvariant();
        foreach (var indicator in BridgeMessagePatterns.LostConnectionIndicators)
        {
            if (normalized.Contains(indicator, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static TelegramConnectionState? DetectLoginStep(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var normalized = message.ToLowerInvariant();

        foreach (var indicator in BridgeMessagePatterns.PasswordStepIndicators)
        {
            if (normalized.Contains(indicator, StringComparison.Ordinal))
            {
                return TelegramConnectionState.LoginAwaitingPassword;
            }
        }

        if (normalized.Contains("password", StringComparison.Ordinal) &&
            normalized.Contains(BridgeMessagePatterns.PasswordSendPhrase, StringComparison.Ordinal))
        {
            return TelegramConnectionState.LoginAwaitingPassword;
        }

        foreach (var indicator in BridgeMessagePatterns.CodeStepIndicators)
        {
            if (normalized.Contains(indicator, StringComparison.Ordinal))
            {
                return TelegramConnectionState.LoginAwaitingCode;
            }
        }

        foreach (var indicator in BridgeMessagePatterns.PhoneStepIndicators)
        {
            if (normalized.Contains(indicator, StringComparison.Ordinal))
            {
                return TelegramConnectionState.LoginAwaitingPhone;
            }
        }

        if (normalized.Contains("phone", StringComparison.Ordinal))
        {
            foreach (var indicator in BridgeMessagePatterns.PhoneLoginIndicators)
            {
                if (normalized.Contains(indicator, StringComparison.Ordinal))
                {
                    return TelegramConnectionState.LoginAwaitingPhone;
                }
            }
        }

        return null;
    }

    internal static bool ShouldRetryBridgeLogin(
        TelegramConnectionState currentState,
        bool connected,
        string? discoveredLoginUrl,
        bool sawBridgeGreeting)
    {
        return !connected &&
               string.IsNullOrWhiteSpace(discoveredLoginUrl) &&
               sawBridgeGreeting &&
               currentState is TelegramConnectionState.BridgePending
                   or TelegramConnectionState.Error
                   or TelegramConnectionState.LoginAwaitingPhone;
    }
}
