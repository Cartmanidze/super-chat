namespace SuperChat.Domain.Features.Integrations.Telegram;

public enum TelegramConnectionState
{
    NotStarted = 1,
    BridgePending = 2,
    Connected = 3,
    RequiresSetup = 4,
    Disconnected = 5,
    Error = 6,
    // Chat login sub-states. Numeric ordering is load-bearing:
    // forward-only transitions are enforced via value > comparison.
    LoginAwaitingPhone = 7,
    LoginAwaitingCode = 8,
    LoginAwaitingPassword = 9
}
