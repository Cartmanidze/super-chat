using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.State;

namespace SuperChat.Infrastructure.Services;

public sealed class FeedbackService(SuperChatStore store) : IFeedbackService
{
    public Task RecordAsync(Guid userId, string area, bool useful, string? note, CancellationToken cancellationToken)
    {
        store.RecordFeedback(userId, area, useful, note);
        return Task.CompletedTask;
    }
}
