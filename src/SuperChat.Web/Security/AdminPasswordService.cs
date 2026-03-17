using System.Text;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;

namespace SuperChat.Web.Security;

public sealed class AdminPasswordService(IOptions<PilotOptions> pilotOptions) : IAdminPasswordService
{
    private const string Base64Prefix = "base64:";

    private readonly string _configuredHash = NormalizeConfiguredHash(pilotOptions.Value.AdminPasswordHash);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuredHash);

    public bool Verify(string password)
    {
        if (!IsConfigured)
        {
            return false;
        }

        return AdminPasswordHasher.Verify(password, _configuredHash);
    }

    private static string NormalizeConfiguredHash(string? configuredHash)
    {
        var trimmed = configuredHash?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (!trimmed.StartsWith(Base64Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var encoded = trimmed[Base64Prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return string.Empty;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }
}
