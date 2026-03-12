namespace SuperChat.Domain.Model;

public static class IntegrationProviderExtensions
{
    public static string ToRouteSegment(this IntegrationProvider provider)
    {
        return provider switch
        {
            IntegrationProvider.Telegram => "telegram",
            IntegrationProvider.WhatsApp => "whatsapp",
            IntegrationProvider.Signal => "signal",
            IntegrationProvider.Discord => "discord",
            IntegrationProvider.Slack => "slack",
            IntegrationProvider.Email => "email",
            IntegrationProvider.Vk => "vk",
            IntegrationProvider.Max => "max",
            _ => provider.ToString().ToLowerInvariant()
        };
    }

    public static bool TryParseRouteSegment(string? value, out IntegrationProvider provider)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "telegram":
                provider = IntegrationProvider.Telegram;
                return true;
            case "whatsapp":
                provider = IntegrationProvider.WhatsApp;
                return true;
            case "signal":
                provider = IntegrationProvider.Signal;
                return true;
            case "discord":
                provider = IntegrationProvider.Discord;
                return true;
            case "slack":
                provider = IntegrationProvider.Slack;
                return true;
            case "email":
                provider = IntegrationProvider.Email;
                return true;
            case "vk":
                provider = IntegrationProvider.Vk;
                return true;
            case "max":
                provider = IntegrationProvider.Max;
                return true;
            default:
                provider = default;
                return false;
        }
    }

    public static IntegrationTransport GetDefaultTransport(this IntegrationProvider provider)
    {
        return provider switch
        {
            IntegrationProvider.Telegram or
            IntegrationProvider.WhatsApp or
            IntegrationProvider.Signal or
            IntegrationProvider.Discord or
            IntegrationProvider.Slack => IntegrationTransport.MatrixBridge,
            IntegrationProvider.Email => IntegrationTransport.ImapSmtp,
            IntegrationProvider.Vk or
            IntegrationProvider.Max => IntegrationTransport.DirectApi,
            _ => IntegrationTransport.DirectApi
        };
    }
}
