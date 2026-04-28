using System.Text.RegularExpressions;
using SuperChat.Domain.Features.Messaging;

namespace SuperChat.Domain.Features.Intelligence;

public static partial class HeuristicSignalDetector
{
    private static readonly string[] WaitingCueKeywords =
    [
        "жду", "ждём", "ждем", "ответ", "ответите", "ответишь", "ответьте", "подскаж", "актуально ли",
        "reply", "respond", "get back", "let me know"
    ];

    private static readonly string[] RequestCueKeywords =
    [
        "пожалуйста", "нужно", "надо", "пришли", "пришлите", "отправь", "отправьте", "скинь", "скиньте",
        "посмотри", "посмотрите", "подготовь", "подготовьте", "заполни", "заполните", "подтверди", "подтвердите",
        "вернись", "вернитесь", "сделай", "сделайте", "please send", "need to", "could you", "can you"
    ];

    private static readonly string[] CommitmentCueKeywords =
    [
        "сделаю", "сделаем", "посмотрю", "вернусь", "отвечу", "напишу", "отправлю", "пришлю", "подготовлю",
        "подтвержу", "созвонюсь", "обещаю", "беру", "закрою", "i will", "i'll", "promise", "send you"
    ];

    private static readonly string[] QuestionLeadKeywords =
    [
        "подскаж", "можешь", "можете", "когда", "какой", "какая", "какие", "получилось", "получится",
        "актуально", "готово", "видели", "сможешь", "сможете", "будет ли", "что", "где", "почему", "how", "when", "can you"
    ];

    public static IReadOnlyCollection<ExtractedItem> Detect(ConversationWindow window, TimeZoneInfo referenceTimeZone)
    {
        if (window.Messages.Count == 0)
        {
            return Array.Empty<ExtractedItem>();
        }

        var items = new List<ExtractedItem>();
        AddMeetingItems(window, referenceTimeZone, items);
        AddWaitingAndTaskItems(window, items);
        AddCommitmentItem(window, items);
        return items;
    }

    private static void AddMeetingItems(
        ConversationWindow window,
        TimeZoneInfo referenceTimeZone,
        List<ExtractedItem> items)
    {
        var transcript = window.Transcript.Trim();
        if (string.IsNullOrWhiteSpace(transcript) || StructuredArtifactDetector.LooksLikeStructuredArtifact(transcript))
        {
            return;
        }

        var meetingSignal = window.Messages.Count == 1
            ? MeetingSignalDetector.TryFromMessage(window.LastMessage, referenceTimeZone)
            : MeetingSignalDetector.TryFromChunk(
                transcript,
                window.TsFrom,
                window.TsTo,
                referenceTimeZone);

        if (meetingSignal is null)
        {
            return;
        }

        items.Add(new ExtractedItem(
            Guid.NewGuid(),
            window.UserId,
            ExtractedItemKind.Meeting,
            "Скоро встреча",
            meetingSignal.Summary,
            window.ExternalChatId,
            window.LastMessage.ExternalMessageId,
            meetingSignal.Person,
            window.TsTo,
            meetingSignal.ScheduledFor,
            meetingSignal.Confidence));
    }

    private static void AddWaitingAndTaskItems(ConversationWindow window, List<ExtractedItem> items)
    {
        var unresolvedMessage = WaitingOnTurnDetector.GetUnansweredExternalMessage(window);
        if (unresolvedMessage is null || ShouldSkip(unresolvedMessage))
        {
            return;
        }

        var summary = unresolvedMessage.Text.Trim();
        var person = ResolveCounterpartyName(unresolvedMessage);

        if (IsWaitingCandidate(summary))
        {
            items.Add(new ExtractedItem(
                Guid.NewGuid(),
                unresolvedMessage.UserId,
                ExtractedItemKind.WaitingOn,
                string.IsNullOrWhiteSpace(person) ? "Нужно ответить" : $"Нужно ответить: {person}",
                summary,
                unresolvedMessage.ExternalChatId,
                unresolvedMessage.ExternalMessageId,
                person,
                unresolvedMessage.SentAt,
                null,
                new Confidence(ComputeWaitingConfidence(summary))));
        }

        if (IsTaskCandidate(summary))
        {
            items.Add(new ExtractedItem(
                Guid.NewGuid(),
                unresolvedMessage.UserId,
                ExtractedItemKind.Task,
                "Нужен следующий шаг",
                summary,
                unresolvedMessage.ExternalChatId,
                unresolvedMessage.ExternalMessageId,
                person,
                unresolvedMessage.SentAt,
                null,
                new Confidence(ComputeTaskConfidence(summary))));
        }
    }

    private static void AddCommitmentItem(ConversationWindow window, List<ExtractedItem> items)
    {
        for (var index = window.Messages.Count - 1; index >= 0; index--)
        {
            var message = window.Messages[index];
            if (!WaitingOnTurnDetector.IsOwnMessage(message) || ShouldSkip(message))
            {
                continue;
            }

            var summary = message.Text.Trim();
            if (!IsCommitmentCandidate(summary))
            {
                continue;
            }

            items.Add(new ExtractedItem(
                Guid.NewGuid(),
                message.UserId,
                ExtractedItemKind.Commitment,
                "Ты пообещал",
                summary,
                message.ExternalChatId,
                message.ExternalMessageId,
                null,
                message.SentAt,
                null,
                new Confidence(ComputeCommitmentConfidence(summary))));
            return;
        }
    }

    private static bool IsWaitingCandidate(string text)
    {
        var lowered = Normalize(text);
        return ContainsQuestionMarker(text) ||
               ContainsAny(lowered, WaitingCueKeywords) ||
               ContainsAny(lowered, RequestCueKeywords);
    }

    private static bool IsTaskCandidate(string text)
    {
        var lowered = Normalize(text);
        return ContainsAny(lowered, RequestCueKeywords) ||
               ImperativeLikeVerbRegex().IsMatch(lowered);
    }

    private static bool IsCommitmentCandidate(string text)
    {
        var lowered = Normalize(text);
        return ContainsAny(lowered, CommitmentCueKeywords) ||
               FirstPersonFutureRegex().IsMatch(lowered);
    }

    private static double ComputeWaitingConfidence(string text)
    {
        var lowered = Normalize(text);
        var confidence = 0.56;

        if (ContainsQuestionMarker(text))
        {
            confidence += 0.14;
        }

        if (ContainsAny(lowered, WaitingCueKeywords))
        {
            confidence += 0.12;
        }

        if (ContainsAny(lowered, QuestionLeadKeywords))
        {
            confidence += 0.08;
        }

        if (ContainsAny(lowered, RequestCueKeywords))
        {
            confidence += 0.06;
        }

        return Math.Min(0.94, confidence);
    }

    private static double ComputeTaskConfidence(string text)
    {
        var lowered = Normalize(text);
        var confidence = 0.54;

        if (ContainsAny(lowered, RequestCueKeywords))
        {
            confidence += 0.18;
        }

        if (ImperativeLikeVerbRegex().IsMatch(lowered))
        {
            confidence += 0.10;
        }

        if (ContainsQuestionMarker(text))
        {
            confidence += 0.04;
        }

        return Math.Min(0.90, confidence);
    }

    private static double ComputeCommitmentConfidence(string text)
    {
        var lowered = Normalize(text);
        var confidence = 0.60;

        if (ContainsAny(lowered, CommitmentCueKeywords))
        {
            confidence += 0.16;
        }

        if (FirstPersonFutureRegex().IsMatch(lowered))
        {
            confidence += 0.10;
        }

        return Math.Min(0.92, confidence);
    }

    private static string? ResolveCounterpartyName(ChatMessage message)
    {
        var sender = message.SenderName?.Trim();
        if (string.IsNullOrWhiteSpace(sender))
        {
            return null;
        }

        if (string.Equals(sender, "Unknown", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sender, "You", StringComparison.OrdinalIgnoreCase) ||
            NumericIdentifierRegex().IsMatch(sender))
        {
            return null;
        }

        return sender;
    }

    private static bool ShouldSkip(ChatMessage message)
    {
        return string.IsNullOrWhiteSpace(message.Text) ||
               StructuredArtifactDetector.LooksLikeStructuredArtifact(message.Text);
    }

    private static bool ContainsQuestionMarker(string text)
    {
        return text.Contains('?', StringComparison.Ordinal);
    }

    private static bool ContainsAny(string text, IEnumerable<string> values)
    {
        return values.Any(value => text.Contains(value, StringComparison.Ordinal));
    }

    private static string Normalize(string text)
    {
        return text.Trim().ToLowerInvariant();
    }

    [GeneratedRegex(@"^\+?\d[\d\-\s\(\)]{4,}$", RegexOptions.CultureInvariant)]
    private static partial Regex NumericIdentifierRegex();

    [GeneratedRegex(@"\b(пришли|пришлите|отправь|отправьте|скинь|скиньте|посмотри|посмотрите|подготовь|подготовьте|заполни|заполните|подтверди|подтвердите|сделай|сделайте)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImperativeLikeVerbRegex();

    [GeneratedRegex(@"\b(я\s+(сделаю|посмотрю|вернусь|отвечу|напишу|отправлю|пришлю|подготовлю|подтвержу)|сделаю|посмотрю|вернусь|отвечу|напишу|отправлю|пришлю|подготовлю|подтвержу|i'll|i will)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FirstPersonFutureRegex();
}
