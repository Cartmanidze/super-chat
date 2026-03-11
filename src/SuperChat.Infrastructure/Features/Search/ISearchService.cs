using SuperChat.Contracts.ViewModels;

namespace SuperChat.Infrastructure.Abstractions;

public interface ISearchService
{
    Task<IReadOnlyList<SearchResultViewModel>> SearchAsync(Guid userId, string query, CancellationToken cancellationToken);
}
