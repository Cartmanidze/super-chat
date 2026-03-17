using System.Text.RegularExpressions;
using SuperChat.Contracts.ViewModels;

namespace SuperChat.Infrastructure.Services;

internal static partial class MeetingJoinLinkParser
{
    public static MeetingJoinLink? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        foreach (Match match in UrlRegex().Matches(text))
        {
            var rawValue = match.Value.TrimEnd('.', ',', ';', ':', ')', ']', '>');
            if (!Uri.TryCreate(rawValue, UriKind.Absolute, out var uri))
            {
                continue;
            }

            var provider = TryResolveProvider(uri);
            if (provider is not null)
            {
                return new MeetingJoinLink(provider.Value, uri);
            }
        }

        return null;
    }

    public static MeetingJoinProvider? TryParseProvider(string? provider)
    {
        return Enum.TryParse<MeetingJoinProvider>(provider, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private static MeetingJoinProvider? TryResolveProvider(Uri uri)
    {
        var host = uri.Host.Trim().ToLowerInvariant();

        if (MatchesHost(host, "meet.google.com"))
        {
            return MeetingJoinProvider.GoogleMeet;
        }

        if (MatchesHost(host, "zoom.us") || MatchesHost(host, "zoom.com"))
        {
            return MeetingJoinProvider.Zoom;
        }

        if (MatchesHost(host, "teams.microsoft.com") || MatchesHost(host, "teams.live.com"))
        {
            return MeetingJoinProvider.MicrosoftTeams;
        }

        if (MatchesHost(host, "webex.com"))
        {
            return MeetingJoinProvider.Webex;
        }

        if (MatchesHost(host, "meet.jit.si"))
        {
            return MeetingJoinProvider.JitsiMeet;
        }

        if (MatchesHost(host, "whereby.com"))
        {
            return MeetingJoinProvider.Whereby;
        }

        if (MatchesHost(host, "telemost.yandex.ru"))
        {
            return MeetingJoinProvider.YandexTelemost;
        }

        return null;
    }

    private static bool MatchesHost(string host, string canonicalHost)
    {
        return host.Equals(canonicalHost, StringComparison.Ordinal) ||
               host.EndsWith($".{canonicalHost}", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();
}

internal sealed record MeetingJoinLink(
    MeetingJoinProvider Provider,
    Uri Url);
