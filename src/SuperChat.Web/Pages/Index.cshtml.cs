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

    public bool IsDevelopmentMode => pilotOptions.Value.DevSeedSampleData;

    public void OnGet()
    {
    }
}
