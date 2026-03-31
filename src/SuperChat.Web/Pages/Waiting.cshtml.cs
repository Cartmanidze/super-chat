using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages;

[Authorize]
public sealed class WaitingModel(
    IDigestService digestService,
    IWorkItemService workItemService,
    TimeProvider timeProvider) : PageModel
{
    public IReadOnlyList<WorkItemCardViewModel> Cards { get; private set; } = [];

    public IReadOnlyList<ResolvedHistoryCard> RecentResolvedItems { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        Cards = await digestService.GetWaitingAsync(userId, cancellationToken);
        var allItems = await workItemService.GetForUserAsync(userId, cancellationToken);
        RecentResolvedItems = ResolvedHistoryComposer.BuildRecentAutoResolved(
                allItems,
                timeProvider.GetUtcNow(),
                6)
            .Select(item => item.ToResolvedHistoryCard())
            .ToList();
    }
}
