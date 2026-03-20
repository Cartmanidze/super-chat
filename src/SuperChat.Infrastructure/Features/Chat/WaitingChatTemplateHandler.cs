using SuperChat.Contracts.Features.Chat;
using SuperChat.Domain.Features.Chat;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Intelligence.Digest;

namespace SuperChat.Infrastructure.Features.Chat;

public sealed class WaitingChatTemplateHandler(IDigestService digestService) : IChatTemplateHandler
{
    public string TemplateId => ChatPromptTemplate.Waiting;

    public async Task<ChatAnswerViewModel> HandleAsync(Guid userId, string question, CancellationToken cancellationToken)
    {
        var cards = await digestService.GetWaitingAsync(userId, cancellationToken);
        return cards
            .Select(card => card.ToChatResultItemViewModel("Awaiting response"))
            .ToChatAnswerViewModel(TemplateId, question);
    }
}
