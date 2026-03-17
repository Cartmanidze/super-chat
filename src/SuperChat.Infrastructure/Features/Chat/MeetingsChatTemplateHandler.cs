using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class MeetingsChatTemplateHandler(IDigestService digestService) : IChatTemplateHandler
{
    public string TemplateId => ChatPromptTemplate.Meetings;

    public async Task<ChatAnswerViewModel> HandleAsync(Guid userId, string question, CancellationToken cancellationToken)
    {
        var cards = await digestService.GetMeetingsAsync(userId, cancellationToken);
        return cards
            .Select(card => card.ToChatResultItemViewModel("Upcoming meeting"))
            .ToChatAnswerViewModel(TemplateId, question);
    }
}
