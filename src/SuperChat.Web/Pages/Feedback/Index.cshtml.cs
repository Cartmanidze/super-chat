using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Feedback;
using SuperChat.Web.Localization;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages.Feedback;

[Authorize]
public sealed class IndexModel(
    IFeedbackService feedbackService,
    IStringLocalizer<SharedResource> localizer) : PageModel
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
        StatusMessage = localizer["Feedback.Recorded"];
        return Page();
    }
}
