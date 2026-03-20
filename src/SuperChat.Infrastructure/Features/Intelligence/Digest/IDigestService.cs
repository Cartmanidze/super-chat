using SuperChat.Contracts.Features.WorkItems;

namespace SuperChat.Infrastructure.Features.Intelligence.Digest;

public interface IDigestService
{
    Task<IReadOnlyList<WorkItemCardViewModel>> GetTodayAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemCardViewModel>> GetWaitingAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemCardViewModel>> GetMeetingsAsync(Guid userId, CancellationToken cancellationToken);
}
