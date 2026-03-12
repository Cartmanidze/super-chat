namespace SuperChat.Domain.Model;

public enum IntegrationConnectionState
{
    NotStarted = 1,
    Pending = 2,
    Connected = 3,
    RequiresSetup = 4,
    Disconnected = 5,
    Error = 6
}
