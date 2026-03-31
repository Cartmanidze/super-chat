namespace SuperChat.Contracts.Features.Chat;

public interface IChatExperienceService
{
    Task<ChatAnswerViewModel> AskAsync(Guid userId, ChatPromptRequest request, CancellationToken cancellationToken);
}
