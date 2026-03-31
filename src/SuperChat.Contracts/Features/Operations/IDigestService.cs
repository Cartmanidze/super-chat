using SuperChat.Contracts.Features.WorkItems;

namespace SuperChat.Contracts.Features.Operations;

public interface IDigestService
{
    Task<IReadOnlyList<WorkItemCardViewModel>> GetTodayAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemCardViewModel>> GetWaitingAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkItemCardViewModel>> GetMeetingsAsync(Guid userId, CancellationToken cancellationToken);
}
