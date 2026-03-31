using System.Globalization;
using System.Text.RegularExpressions;
using SuperChat.Domain.Features.Messaging;

namespace SuperChat.Domain.Features.Intelligence;

public static partial class MeetingSignalDetector
{
    private const string RussianInterviewStem = "\u0441\u043e\u0431\u0435\u0441\u0435\u0434";

    private static readonly string[] ExplicitMeetingKeywords =
    [
        "meeting", "call", "sync", "calendar", "zoom",
        "встреч", "созвон", "колл", "зум", "календар"
    ];

    private static readonly string[] MeetingIntentKeywords =
    [
        "заехать", "подъехать", "увид", "встрет", "созвон", "синк",
        "будет", "будем", "буду", "договорил", "подтвержда", "confirm"
    ];

    private static readonly string[] MeetingSlangKeywords =
    [
        "\u043c\u0438\u0442",
        "\u043c\u0438\u0442\u0438\u043d\u0433"
    ];

    private static readonly string[] ConfirmationKeywords =
    [
        "подтверждаю", "подтверждаем", "подтверждено", "confirmed", "confirm",
        "итого", "финально", "final", "договорились"
    ];

    private static readonly string[] SchedulingFollowUpKeywords =
    [
        "давай", "лучше", "не могу", "не смогу", "не получится",
        "перенес", "перенос", "вместо", "смож", "подойдёт", "подойдет",
        "can't", "cannot", "instead", "resched"
    ];

    private static readonly string[] TodayKeywords = ["today", "сегодня"];
    private static readonly string[] TomorrowKeywords = ["tomorrow", "завтра"];
    private static readonly string[] FridayKeywords = ["friday", "пятниц"];

    public static MeetingSignal? TryFromMessage(
        NormalizedMessage message,
        TimeZoneInfo referenceTimeZone)
    {
        return TryDetectCore(
            StripSenderPrefix(message.Text),
            message.SentAt,
            message.SentAt,
            referenceTimeZone);
    }

    public static MeetingSignal? TryFromChunk(
        string chunkText,
        DateTimeOffset observedAt,
        DateTimeOffset fallbackScheduledFrom,
        TimeZoneInfo referenceTimeZone)
    {
        var lines = chunkText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(StripSenderPrefix)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        MeetingSignal? best = null;
        var meetingContextSeen = false;

        foreach (var line in lines)
        {
            var explicitCandidate = TryDetectCore(line, observedAt, fallbackScheduledFrom, referenceTimeZone);
            if (explicitCandidate is not null)
            {
                best = explicitCandidate;
                meetingContextSeen = true;
                continue;
            }

            if (!meetingContextSeen)
            {
                continue;
            }

            var followUpCandidate = TryDetectContextualFollowUp(
                line,
                observedAt,
                fallbackScheduledFrom,
                referenceTimeZone);

            if (followUpCandidate is not null)
            {
                best = followUpCandidate;
            }
        }

        return best;
    }

    private static MeetingSignal? TryDetectCore(
        string rawText,
        DateTimeOffset observedAt,
        DateTimeOffset fallbackScheduledFrom,
        TimeZoneInfo referenceTimeZone)
    {
        var summary = rawText.Trim();
        if (string.IsNullOrWhiteSpace(summary))
        {
            return null;
        }

        var lowered = summary.ToLowerInvariant();
        var scheduledFor = ResolveScheduledFor(fallbackScheduledFrom, lowered, referenceTimeZone);
        if (scheduledFor is null)
        {
            return null;
        }

        var hasExplicitMeetingKeyword = ContainsAny(lowered, ExplicitMeetingKeywords);
        var hasInterviewKeyword = lowered.Contains("interview", StringComparison.Ordinal) ||
                                  lowered.Contains(RussianInterviewStem, StringComparison.Ordinal);
        hasExplicitMeetingKeyword = hasExplicitMeetingKeyword || hasInterviewKeyword;
        var hasMeetingIntentKeyword = ContainsAny(lowered, MeetingIntentKeywords);
        var hasConfirmationKeyword = ContainsAny(lowered, ConfirmationKeywords);
        var hasMeetingSlangKeyword = ContainsAny(lowered, MeetingSlangKeywords);
        hasExplicitMeetingKeyword = hasExplicitMeetingKeyword || hasMeetingSlangKeyword;
        hasMeetingIntentKeyword = hasMeetingIntentKeyword || hasMeetingSlangKeyword;

        if (!hasExplicitMeetingKeyword && !hasMeetingIntentKeyword)
        {
            return null;
        }

        var confidence = 0.58;
        if (hasExplicitMeetingKeyword)
        {
            confidence += 0.18;
        }

        if (hasMeetingIntentKeyword)
        {
            confidence += 0.08;
        }

        if (hasConfirmationKeyword)
        {
            confidence += 0.10;
        }

        if (TimeRegex().IsMatch(lowered))
        {
            confidence += 0.07;
        }

        if (ContainsAny(lowered, TodayKeywords) || ContainsAny(lowered, TomorrowKeywords) || ContainsAny(lowered, FridayKeywords))
        {
            confidence += 0.04;
        }

        return new MeetingSignal(
            "Upcoming meeting",
            summary,
            TryExtractPerson(summary),
            observedAt,
            scheduledFor.Value,
            new Confidence(Math.Min(0.98, confidence)));
    }

    private static MeetingSignal? TryDetectContextualFollowUp(
        string rawText,
        DateTimeOffset observedAt,
        DateTimeOffset fallbackScheduledFrom,
        TimeZoneInfo referenceTimeZone)
    {
        var summary = rawText.Trim();
        if (string.IsNullOrWhiteSpace(summary))
        {
            return null;
        }

        var lowered = summary.ToLowerInvariant();
        var scheduledFor = ResolveScheduledFor(fallbackScheduledFrom, lowered, referenceTimeZone);
        if (scheduledFor is null || !LooksLikeSchedulingFollowUp(lowered, summary))
        {
            return null;
        }

        var confidence = 0.66;
        if (ContainsAny(lowered, SchedulingFollowUpKeywords))
        {
            confidence += 0.10;
        }

        if (TimeRegex().IsMatch(lowered))
        {
            confidence += 0.08;
        }

        if (ContainsAny(lowered, TodayKeywords) || ContainsAny(lowered, TomorrowKeywords) || ContainsAny(lowered, FridayKeywords))
        {
            confidence += 0.06;
        }

        return new MeetingSignal(
            "Upcoming meeting",
            summary,
            TryExtractPerson(summary),
            observedAt,
            scheduledFor.Value,
            new Confidence(Math.Min(0.92, confidence)));
    }

    private static DateTimeOffset? ResolveScheduledFor(
        DateTimeOffset observedAt,
        string lowered,
        TimeZoneInfo referenceTimeZone)
    {
        var localObservedAt = TimeZoneInfo.ConvertTime(observedAt, referenceTimeZone);
        var explicitTime = TryResolveExplicitTime(localObservedAt, lowered, referenceTimeZone);
        if (explicitTime is not null)
        {
            return explicitTime;
        }

        if (ContainsAny(lowered, TomorrowKeywords))
        {
            return ToUtc(localObservedAt.Date.AddDays(1).AddHours(10), referenceTimeZone);
        }

        if (ContainsAny(lowered, FridayKeywords))
        {
            var candidateDate = localObservedAt.Date;
            while (candidateDate.DayOfWeek != DayOfWeek.Friday)
            {
                candidateDate = candidateDate.AddDays(1);
            }

            return ToUtc(candidateDate.AddHours(11), referenceTimeZone);
        }

        if (ContainsAny(lowered, ["end of day", "концу дня"]))
        {
            return ToUtc(localObservedAt.Date.AddHours(18), referenceTimeZone);
        }

        return null;
    }

    private static DateTimeOffset? TryResolveExplicitTime(
        DateTimeOffset localObservedAt,
        string lowered,
        TimeZoneInfo referenceTimeZone)
    {
        var matches = TimeRegex().Matches(lowered);
        if (matches.Count == 0)
        {
            return null;
        }

        var match = SelectRelevantTimeMatch(matches, lowered);
        if (!int.TryParse(match.Groups["hour"].Value, CultureInfo.InvariantCulture, out var hour))
        {
            return null;
        }

        var minute = match.Groups["minute"].Success &&
                     int.TryParse(match.Groups["minute"].Value, CultureInfo.InvariantCulture, out var parsedMinute)
            ? parsedMinute
            : 0;

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            return null;
        }

        var candidateDate = localObservedAt.Date;
        var explicitlyToday = ContainsAny(lowered, TodayKeywords);

        if (ContainsAny(lowered, TomorrowKeywords))
        {
            candidateDate = candidateDate.AddDays(1);
        }
        else if (ContainsAny(lowered, FridayKeywords))
        {
            while (candidateDate.DayOfWeek != DayOfWeek.Friday)
            {
                candidateDate = candidateDate.AddDays(1);
            }
        }

        var candidateLocal = new DateTimeOffset(
            candidateDate.Year,
            candidateDate.Month,
            candidateDate.Day,
            hour,
            minute,
            0,
            referenceTimeZone.GetUtcOffset(candidateDate));

        if (!explicitlyToday &&
            !ContainsAny(lowered, TomorrowKeywords) &&
            !ContainsAny(lowered, FridayKeywords) &&
            candidateLocal < localObservedAt.AddMinutes(-15))
        {
            candidateLocal = candidateLocal.AddDays(1);
        }

        return ToUtc(candidateLocal, referenceTimeZone);
    }

    private static Match SelectRelevantTimeMatch(MatchCollection matches, string lowered)
    {
        if (matches.Count == 1 || !ContainsAny(lowered, SchedulingFollowUpKeywords))
        {
            return matches[0];
        }

        return matches[matches.Count - 1];
    }

    private static bool LooksLikeSchedulingFollowUp(string lowered, string summary)
    {
        if (ContainsAny(lowered, SchedulingFollowUpKeywords))
        {
            return true;
        }

        return summary.Length <= 64 &&
               (TimeRegex().IsMatch(lowered) ||
                ContainsAny(lowered, TodayKeywords) ||
                ContainsAny(lowered, TomorrowKeywords) ||
                ContainsAny(lowered, FridayKeywords));
    }

    private static DateTimeOffset ToUtc(DateTimeOffset localValue, TimeZoneInfo referenceTimeZone)
    {
        var unspecifiedLocal = DateTime.SpecifyKind(localValue.DateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocal, referenceTimeZone);
    }

    private static string StripSenderPrefix(string line)
    {
        var separatorIndex = line.IndexOf(": ", StringComparison.Ordinal);
        return separatorIndex is > 0 and < 48
            ? line[(separatorIndex + 2)..].Trim()
            : line.Trim();
    }

    private static string? TryExtractPerson(string summary)
    {
        var match = PersonRegex().Match(summary);
        return match.Success
            ? match.Value
            : null;
    }

    private static bool ContainsAny(string text, IEnumerable<string> values)
    {
        return values.Any(value => text.Contains(value, StringComparison.Ordinal));
    }

    [GeneratedRegex(@"(?:\b(?:at|в)\s*)(?<hour>\d{1,2})(?::(?<minute>\d{2}))?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TimeRegex();

    [GeneratedRegex(@"\b[\p{Lu}][\p{Ll}]+\b", RegexOptions.CultureInvariant)]
    private static partial Regex PersonRegex();
}
