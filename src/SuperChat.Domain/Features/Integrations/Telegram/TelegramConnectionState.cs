namespace SuperChat.Domain.Model;

public enum TelegramConnectionState
{
    NotStarted = 1,
    BridgePending = 2,
    Connected = 3,
    RequiresSetup = 4,
    Disconnected = 5,
    Error = 6
}
