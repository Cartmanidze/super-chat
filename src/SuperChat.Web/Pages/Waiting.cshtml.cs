using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Contracts.ViewModels;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages;

[Authorize]
public sealed class WaitingModel(IDigestService digestService) : PageModel
{
    public IReadOnlyList<WorkItemCardViewModel> Cards { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Cards = await digestService.GetWaitingAsync(User.GetUserId(), cancellationToken);
    }
}
