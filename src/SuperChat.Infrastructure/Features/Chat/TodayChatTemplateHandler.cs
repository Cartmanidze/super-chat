using SuperChat.Contracts.Features.Chat;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Domain.Features.Chat;

namespace SuperChat.Infrastructure.Features.Chat;

public sealed class TodayChatTemplateHandler(IDigestService digestService) : IChatTemplateHandler
{
    public string TemplateId => ChatPromptTemplate.Today;

    public async Task<ChatAnswerViewModel> HandleAsync(Guid userId, string question, CancellationToken cancellationToken)
    {
        var cards = await digestService.GetTodayAsync(userId, cancellationToken);
        return cards
            .Select(card => card.ToChatResultItemViewModel())
            .ToChatAnswerViewModel(TemplateId, question);
    }
}
