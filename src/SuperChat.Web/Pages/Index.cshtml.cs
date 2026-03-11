using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;

namespace SuperChat.Web.Pages;

public sealed class IndexModel(IOptions<PilotOptions> pilotOptions) : PageModel
{
    [BindProperty]
    public string Email { get; set; } = string.Empty;

    public bool IsSignedIn => User.Identity?.IsAuthenticated ?? false;

    public string BootstrapModeSummary => pilotOptions.Value.DevSeedSampleData
        ? "Development bridge seeding enabled"
        : "Waiting for real bridge sync";

    public void OnGet()
    {
    }
}
