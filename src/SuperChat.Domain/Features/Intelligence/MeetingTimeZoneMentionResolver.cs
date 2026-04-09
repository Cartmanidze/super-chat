using System.Text.RegularExpressions;

namespace SuperChat.Domain.Features.Intelligence;

public static partial class MeetingTimeZoneMentionResolver
{
    private static readonly string[] AmbiguousAliases =
    [
        "cst",
        "cdt",
        "ist",
        "bst"
    ];

    private static readonly ZoneSpec[] KnownZones =
    [
        Fixed("UTC", TimeSpan.Zero, "utc", "gmt"),
        Fixed("MSK", TimeSpan.FromHours(3), "msk", "мск", "moscow", "москва", "москве", "московскому"),
        Fixed("EST", TimeSpan.FromHours(-5), "est"),
        Fixed("EDT", TimeSpan.FromHours(-4), "edt"),
        Fixed("PST", TimeSpan.FromHours(-8), "pst"),
        Fixed("PDT", TimeSpan.FromHours(-7), "pdt"),
        Fixed("MST", TimeSpan.FromHours(-7), "mst"),
        Fixed("MDT", TimeSpan.FromHours(-6), "mdt"),
        Fixed("CET", TimeSpan.FromHours(1), "cet"),
        Fixed("CEST", TimeSpan.FromHours(2), "cest"),
        Fixed("EET", TimeSpan.FromHours(2), "eet"),
        Fixed("EEST", TimeSpan.FromHours(3), "eest"),
        Geographic("Europe/London", "london", "лондон", "лондоне", "лондону"),
        Geographic("America/New_York", "new york", "new-york", "нью йорк", "нью-йорк"),
        Geographic("America/Los_Angeles", "los angeles", "los-angeles", "лос анджелес", "лос-анджелес"),
        Geographic("America/Chicago", "chicago", "чикаго"),
        Geographic("America/Denver", "denver", "денвер"),
        Geographic("Europe/Berlin", "berlin", "берлин", "берлине"),
        Geographic("Europe/Paris", "paris", "париж", "париже")
    ];

    private static readonly TimeZoneAlias[] KnownAliases = KnownZones
        .SelectMany(CreateAliases)
        .OrderByDescending(alias => alias.Alias.Length)
        .ToArray();

    public static MeetingTimeZoneResolution Resolve(string text, TimeZoneInfo localTimeZone)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return MeetingTimeZoneResolution.ImplicitLocal(localTimeZone);
        }

        var normalized = Normalize(text);
        if (!TimeMentionRegex().IsMatch(normalized))
        {
            return MeetingTimeZoneResolution.ImplicitLocal(localTimeZone);
        }

        var knownAlias = KnownAliases.FirstOrDefault(alias => ContainsBounded(normalized, alias.Alias));
        if (knownAlias is not null)
        {
            return MeetingTimeZoneResolution.Explicit(
                knownAlias.TimeZone,
                knownAlias.Alias,
                BuildIgnoredPersonTokens(knownAlias.Alias));
        }

        var ambiguousAlias = AmbiguousAliases.FirstOrDefault(alias => ContainsBounded(normalized, alias));
        if (ambiguousAlias is not null)
        {
            return MeetingTimeZoneResolution.Ambiguous(ambiguousAlias);
        }

        var unknownMention = TryFindUnknownMention(normalized);
        return unknownMention is not null
            ? MeetingTimeZoneResolution.Unknown(unknownMention)
            : MeetingTimeZoneResolution.ImplicitLocal(localTimeZone);
    }

    public static bool ShouldIgnoreAsPerson(string candidate, MeetingTimeZoneResolution resolution)
    {
        if (string.IsNullOrWhiteSpace(candidate) || resolution.IgnoredPersonTokens.Count == 0)
        {
            return false;
        }

        return resolution.IgnoredPersonTokens.Contains(Normalize(candidate));
    }

    private static IEnumerable<TimeZoneAlias> CreateAliases(ZoneSpec spec)
    {
        var timeZone = spec.FixedOffset is TimeSpan offset
            ? CreateFixedTimeZone(spec.CanonicalId, offset)
            : TryResolveTimeZone(spec.CanonicalId);
        if (timeZone is null)
        {
            yield break;
        }

        foreach (var alias in spec.Aliases)
        {
            yield return new TimeZoneAlias(Normalize(alias), timeZone);
        }
    }

    private static TimeZoneInfo? TryResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    private static HashSet<string> BuildIgnoredPersonTokens(string alias)
    {
        var tokens = alias
            .Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .ToHashSet(StringComparer.Ordinal);

        tokens.Add(alias);
        return tokens;
    }

    private static string? TryFindUnknownMention(string normalized)
    {
        var englishMatch = EnglishTimeZoneHintRegex().Match(normalized);
        if (englishMatch.Success)
        {
            return englishMatch.Groups["zone"].Value.Trim();
        }

        var russianMatch = RussianTimeZoneHintRegex().Match(normalized);
        if (russianMatch.Success)
        {
            return russianMatch.Groups["zone"].Value.Trim();
        }

        return null;
    }

    private static bool ContainsBounded(string text, string value)
    {
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            var before = index == 0 ? '\0' : text[index - 1];
            var afterIndex = index + value.Length;
            var after = afterIndex >= text.Length ? '\0' : text[afterIndex];
            if (IsBoundary(before) && IsBoundary(after))
            {
                return true;
            }

            index++;
        }

        return false;
    }

    private static bool IsBoundary(char value)
    {
        return value == '\0' || !char.IsLetter(value);
    }

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace('ё', 'е');
    }

    private static TimeZoneInfo CreateFixedTimeZone(string id, TimeSpan offset)
    {
        return TimeZoneInfo.CreateCustomTimeZone(id, offset, id, id);
    }

    private static ZoneSpec Fixed(string id, TimeSpan offset, params string[] aliases)
    {
        return new ZoneSpec(id, offset, aliases);
    }

    private static ZoneSpec Geographic(string id, params string[] aliases)
    {
        return new ZoneSpec(id, null, aliases);
    }

    [GeneratedRegex(@"(?:\b(?:at|в|на)\s*)(?<hour>\d{1,2})(?::(?<minute>\d{2}))?\s*(?<ampm>a\.?m\.?|p\.?m\.?)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TimeMentionRegex();

    [GeneratedRegex(@"\b(?<zone>[a-z][a-z\-\s]{1,32})\s+time\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EnglishTimeZoneHintRegex();

    [GeneratedRegex(@"\bпо\s+(?<zone>[\p{L}\-]+(?:\s+[\p{L}\-]+){0,2})(?:\s+времени)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RussianTimeZoneHintRegex();

    private sealed record ZoneSpec(
        string CanonicalId,
        TimeSpan? FixedOffset,
        IReadOnlyList<string> Aliases);

    private sealed record TimeZoneAlias(
        string Alias,
        TimeZoneInfo TimeZone);
}

public sealed record MeetingTimeZoneResolution(
    MeetingTimeZoneResolutionStatus Status,
    TimeZoneInfo TimeZone,
    string? Mention,
    IReadOnlySet<string> IgnoredPersonTokens)
{
    public bool RequiresClarification =>
        Status is MeetingTimeZoneResolutionStatus.Unknown or MeetingTimeZoneResolutionStatus.Ambiguous;

    public static MeetingTimeZoneResolution ImplicitLocal(TimeZoneInfo localTimeZone)
    {
        return new MeetingTimeZoneResolution(
            MeetingTimeZoneResolutionStatus.ImplicitLocal,
            localTimeZone,
            null,
            new HashSet<string>(StringComparer.Ordinal));
    }

    public static MeetingTimeZoneResolution Explicit(TimeZoneInfo timeZone, string mention, IReadOnlySet<string> ignoredPersonTokens)
    {
        return new MeetingTimeZoneResolution(
            MeetingTimeZoneResolutionStatus.Explicit,
            timeZone,
            mention,
            ignoredPersonTokens);
    }

    public static MeetingTimeZoneResolution Unknown(string mention)
    {
        return new MeetingTimeZoneResolution(
            MeetingTimeZoneResolutionStatus.Unknown,
            TimeZoneInfo.Utc,
            mention,
            new HashSet<string>(StringComparer.Ordinal));
    }

    public static MeetingTimeZoneResolution Ambiguous(string mention)
    {
        return new MeetingTimeZoneResolution(
            MeetingTimeZoneResolutionStatus.Ambiguous,
            TimeZoneInfo.Utc,
            mention,
            new HashSet<string>(StringComparer.Ordinal));
    }
}

public enum MeetingTimeZoneResolutionStatus
{
    ImplicitLocal = 0,
    Explicit = 1,
    Unknown = 2,
    Ambiguous = 3
}
