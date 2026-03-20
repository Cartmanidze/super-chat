using SuperChat.Contracts.Features.Chat;

namespace SuperChat.Infrastructure.Features.Chat;

internal static class ChatAnswerViewModelMappings
{
    public static ChatAnswerViewModel ToChatAnswerViewModel(
        this IEnumerable<ChatResultItemViewModel> items,
        string mode,
        string question,
        string? assistantText = null)
    {
        return new ChatAnswerViewModel(mode, question, items.ToList(), assistantText);
    }
}
