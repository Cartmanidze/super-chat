using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Services;

internal static class ChatTemplateCardMapper
{
    public static ChatResultItemViewModel MapDigestCard(DashboardCardViewModel card, string? genericTitle = null)
    {
        var timestamp = string.Equals(card.Kind, ExtractedItemKind.Meeting.ToString(), StringComparison.Ordinal)
            ? card.DueAt ?? card.ObservedAt
            : card.ObservedAt;

        if (!string.IsNullOrWhiteSpace(genericTitle) &&
            string.Equals(card.Title, genericTitle, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(card.Summary))
        {
            return new ChatResultItemViewModel(card.Summary, string.Empty, card.SourceRoom, timestamp);
        }

        return new ChatResultItemViewModel(card.Title, card.Summary, card.SourceRoom, timestamp);
    }
}
