namespace SuperChat.Domain.Features.Integrations;

public enum IntegrationTransport
{
    MatrixBridge = 1,
    DirectApi = 2,
    ImapSmtp = 3,
    Webhook = 4
}
