using SuperChat.Contracts.Features.WorkItems;

namespace SuperChat.Contracts.Features.Operations;

public interface IDigestService
{
    Task<IReadOnlyList<WorkItemCardViewModel>> GetMeetingsAsync(Guid userId, CancellationToken cancellationToken);
}
