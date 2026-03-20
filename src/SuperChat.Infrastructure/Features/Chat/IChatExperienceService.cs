using SuperChat.Contracts.Features.Chat;

namespace SuperChat.Infrastructure.Features.Chat;

public interface IChatExperienceService
{
    Task<ChatAnswerViewModel> AskAsync(Guid userId, ChatPromptRequest request, CancellationToken cancellationToken);
}
