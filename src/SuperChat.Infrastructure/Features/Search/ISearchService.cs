using SuperChat.Contracts.Features.Search;

namespace SuperChat.Infrastructure.Features.Search;

public interface ISearchService
{
    Task<IReadOnlyList<SearchResultViewModel>> SearchAsync(Guid userId, string query, CancellationToken cancellationToken);
}
