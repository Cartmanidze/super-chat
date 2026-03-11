using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages.Feedback;

[Authorize]
public sealed class IndexModel(IFeedbackService feedbackService) : PageModel
{
    [BindProperty]
    public string Area { get; set; } = "today";

    [BindProperty]
    public bool Useful { get; set; } = true;

    [BindProperty]
    public string? Note { get; set; }

    public string StatusMessage { get; private set; } = string.Empty;

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await feedbackService.RecordAsync(User.GetUserId(), Area, Useful, Note, cancellationToken);
        StatusMessage = "Feedback recorded. Thanks for tightening the pilot loop.";
        return Page();
    }
}
