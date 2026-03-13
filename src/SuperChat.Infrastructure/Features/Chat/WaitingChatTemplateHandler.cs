using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class WaitingChatTemplateHandler(IDigestService digestService) : IChatTemplateHandler
{
    public string TemplateId => ChatPromptTemplate.Waiting;

    public async Task<ChatAnswerViewModel> HandleAsync(Guid userId, string question, CancellationToken cancellationToken)
    {
        var cards = await digestService.GetWaitingAsync(userId, cancellationToken);
        return new ChatAnswerViewModel(
            TemplateId,
            question,
            cards.Select(card => ChatTemplateCardMapper.MapDigestCard(card, "Awaiting response")).ToList());
    }
}
