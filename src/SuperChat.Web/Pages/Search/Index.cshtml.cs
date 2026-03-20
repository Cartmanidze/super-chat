using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Contracts.Features.Search;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Search;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages.Search;

[Authorize]
public sealed class IndexModel(ISearchService searchService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Query { get; set; } = string.Empty;

    public IReadOnlyList<SearchResultViewModel> Results { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Results = await searchService.SearchAsync(User.GetUserId(), Query, cancellationToken);
    }
}
