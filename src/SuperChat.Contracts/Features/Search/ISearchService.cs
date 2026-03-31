namespace SuperChat.Contracts.Features.Search;

public interface ISearchService
{
    Task<IReadOnlyList<SearchResultViewModel>> SearchAsync(Guid userId, string query, CancellationToken cancellationToken);
}
