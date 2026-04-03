using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Chat;
using SuperChat.Contracts.Features.Integrations;
using SuperChat.Domain.Features.Chat;
using SuperChat.Domain.Features.Integrations;
using SuperChat.Web.Localization;
using SuperChat.Web.Security;

namespace SuperChat.Web.Pages;

public sealed class IndexModel(
    IAuthFlowService authFlowService,
    IIntegrationConnectionService integrationConnectionService,
    IChatTemplateCatalog chatTemplateCatalog,
    IChatExperienceService chatExperienceService,
    IUiTextService uiTextService,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string TemplateId { get; set; } = ChatPromptTemplate.Meetings;

    [BindProperty]
    public string Question { get; set; } = string.Empty;

    public bool IsSignedIn => User.Identity?.IsAuthenticated ?? false;

    public bool CanAskQuestions { get; private set; }

    public string StatusMessage { get; private set; } = string.Empty;

    public bool CodeSent { get; private set; }

    public int MaxQuestionLength => ChatPromptRequest.MaxQuestionLength;

    public IReadOnlyList<ChatTemplateDefinition> Templates { get; } = chatTemplateCatalog.GetVisibleTemplates();

    public ChatTemplateDefinition DefaultTemplate => Templates.First();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!IsSignedIn)
        {
            return Page();
        }

        var connection = await integrationConnectionService.GetStatusAsync(
            User.GetUserId(),
            IntegrationProvider.Telegram,
            cancellationToken);

        CanAskQuestions = connection.State == IntegrationConnectionState.Connected;
        return Page();
    }

    public async Task<IActionResult> OnPostRequestLinkAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            StatusMessage = localizer["Auth.Email.Required"];
            return Page();
        }

        try
        {
            var result = await authFlowService.SendCodeAsync(Email, cancellationToken);
            StatusMessage = uiTextService.SendCodeStatusText(result.Status);
            CodeSent = result.Accepted;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            StatusMessage = localizer["Auth.Code.GenericError"];
        }

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
            return StatusCode(StatusCodes.Status409Conflict, new { error = localizer["Search.Connection.RequiredError"].Value });
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
