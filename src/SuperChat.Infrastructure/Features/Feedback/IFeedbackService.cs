namespace SuperChat.Infrastructure.Features.Feedback;

public interface IFeedbackService
{
    Task RecordAsync(Guid userId, string area, bool useful, string? note, CancellationToken cancellationToken);
}
