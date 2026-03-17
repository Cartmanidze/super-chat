using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;

namespace SuperChat.Web.Security;

public sealed class AdminPasswordService(IOptions<PilotOptions> pilotOptions) : IAdminPasswordService
{
    private readonly string _configuredHash = pilotOptions.Value.AdminPasswordHash?.Trim() ?? string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuredHash);

    public bool Verify(string password)
    {
        if (!IsConfigured)
        {
            return false;
        }

        return AdminPasswordHasher.Verify(password, _configuredHash);
    }
}
