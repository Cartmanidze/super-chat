using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Contracts.ViewModels;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Web.Localization;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages;

public sealed class IndexModel(
    IAuthFlowService authFlowService,
    IIntegrationConnectionService integrationConnectionService,
    IChatTemplateCatalog chatTemplateCatalog,
    IChatExperienceService chatExperienceService,
    IUiTextService uiTextService) : PageModel
{
    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string TemplateId { get; set; } = ChatPromptTemplate.Today;

    [BindProperty]
    public string Question { get; set; } = string.Empty;

    public bool IsSignedIn => User.Identity?.IsAuthenticated ?? false;

    public bool CanAskQuestions { get; private set; }

    public string StatusMessage { get; private set; } = string.Empty;

    public Uri? DevelopmentLink { get; private set; }

    public int MaxQuestionLength => ChatPromptRequest.MaxQuestionLength;

    public IReadOnlyList<ChatTemplateDefinition> Templates { get; } = chatTemplateCatalog.GetVisibleTemplates();

    public ChatTemplateDefinition DefaultTemplate => Templates.First();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (!IsSignedIn)
        {
            return;
        }

        var connection = await integrationConnectionService.GetStatusAsync(
            User.GetUserId(),
            IntegrationProvider.Telegram,
            cancellationToken);

        CanAskQuestions = connection.State == IntegrationConnectionState.Connected;
    }

    public async Task<IActionResult> OnPostRequestLinkAsync(CancellationToken cancellationToken)
    {
        var result = await authFlowService.RequestMagicLinkAsync(Email, cancellationToken);
        StatusMessage = uiTextService.MagicLinkRequestStatusText(result.Status);
        DevelopmentLink = result.DevelopmentLink;
        return Page();
    }

    public async Task<IActionResult> OnPostAskAsync(CancellationToken cancellationToken)
    {
        if (!IsSignedIn)
        {
            return Unauthorized();
        }

        var connection = await integrationConnectionService.GetStatusAsync(
            User.GetUserId(),
            IntegrationProvider.Telegram,
            cancellationToken);

        if (connection.State != IntegrationConnectionState.Connected)
        {
            return StatusCode(StatusCodes.Status409Conflict, new { error = "Telegram connection is required before chat is available." });
        }

        try
        {
            var answer = await chatExperienceService.AskAsync(
                User.GetUserId(),
                new ChatPromptRequest(TemplateId, Question),
                cancellationToken);

            return new JsonResult(answer);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
