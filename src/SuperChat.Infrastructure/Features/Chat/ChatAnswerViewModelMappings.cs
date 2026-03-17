using SuperChat.Contracts.ViewModels;

namespace SuperChat.Infrastructure.Services;

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
