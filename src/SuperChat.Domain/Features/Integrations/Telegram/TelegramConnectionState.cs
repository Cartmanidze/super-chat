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
    LoginAwaitingPassword = 9,
    // Telegram отозвал auth_key (sidecar при resume или health-check получил
    // is_user_authorized() == false). Не путать с Disconnected: это явное
    // действие пользователя «отключить», а Revoked — пассивный отвал, на
    // который пользователь сам не влиял. UI показывает «нужен вход» и в том,
    // и в другом случае, но события расходятся для аналитики.
    Revoked = 10
}
