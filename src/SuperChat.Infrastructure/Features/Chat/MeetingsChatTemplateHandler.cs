using SuperChat.Contracts.Features.Chat;
using SuperChat.Domain.Features.Chat;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Intelligence.Digest;

namespace SuperChat.Infrastructure.Features.Chat;

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
