using SuperChat.Contracts.Features.Feedback;
using SuperChat.Domain.Features.Feedback;

namespace SuperChat.Application.Features.Feedback;

public sealed class FeedbackAppService(
    IFeedbackEventRepository feedbackEventRepository,
    TimeProvider timeProvider) : IFeedbackService
{
    public async Task RecordAsync(Guid userId, string area, bool useful, string? note, CancellationToken cancellationToken)
    {
        var feedback = new FeedbackEvent(
            Id: Guid.NewGuid(),
            UserId: userId,
            Area: area,
            Value: useful ? "useful" : "not_useful",
            Notes: note,
            CreatedAt: timeProvider.GetUtcNow());

        await feedbackEventRepository.AddAsync(feedback, cancellationToken);
    }
}
