namespace SuperChat.Domain.Features.Feedback;

public interface IFeedbackEventRepository
{
    Task AddAsync(FeedbackEvent feedback, CancellationToken cancellationToken);
}
