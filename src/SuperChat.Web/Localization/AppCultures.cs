using System.Globalization;

namespace SuperChat.Web.Localization;

public sealed record SupportedAppCulture(
    string Name,
    string NativeName,
    string ShortName);

public static class AppCultures
{
    public const string DefaultCultureName = "ru";

    public static readonly IReadOnlyList<SupportedAppCulture> Supported =
    [
        new("ru", CultureInfo.GetCultureInfo("ru").NativeName, "RU"),
        new("en", CultureInfo.GetCultureInfo("en").NativeName, "EN")
    ];

    public static readonly IReadOnlyList<CultureInfo> SupportedCultureInfos = Supported
        .Select(item => new CultureInfo(item.Name))
        .ToArray();

    public static bool IsSupported(string? cultureName)
    {
        return Supported.Any(item => string.Equals(item.Name, cultureName, StringComparison.OrdinalIgnoreCase));
    }
}
