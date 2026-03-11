using SuperChat.Contracts.ViewModels;

namespace SuperChat.Infrastructure.Abstractions;

public interface IDigestService
{
    Task<IReadOnlyList<DashboardCardViewModel>> GetTodayAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DashboardCardViewModel>> GetWaitingAsync(Guid userId, CancellationToken cancellationToken);
}
