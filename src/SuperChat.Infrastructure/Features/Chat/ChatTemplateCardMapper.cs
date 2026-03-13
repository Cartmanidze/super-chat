using SuperChat.Contracts.ViewModels;

namespace SuperChat.Infrastructure.Services;

internal static class ChatTemplateCardMapper
{
    public static ChatResultItemViewModel MapDigestCard(DashboardCardViewModel card, string? genericTitle = null)
    {
        if (!string.IsNullOrWhiteSpace(genericTitle) &&
            string.Equals(card.Title, genericTitle, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(card.Summary))
        {
            return new ChatResultItemViewModel(card.Summary, string.Empty, card.SourceRoom, card.DueAt);
        }

        return new ChatResultItemViewModel(card.Title, card.Summary, card.SourceRoom, card.DueAt);
    }
}
