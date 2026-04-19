using System.Globalization;
using System.Text;
using SuperChat.Contracts;

namespace SuperChat.Infrastructure.Features.Intelligence.Resolution;

internal static class ConversationResolutionPromptBuilder
{
    public static IReadOnlyList<DeepSeekMessage> BuildMessages(
        IReadOnlyList<ConversationResolutionCandidate> candidates,
        TimeZoneInfo referenceTimeZone,
        double minConfidence)
    {
        return
        [
            new DeepSeekMessage("system", BuildSystemPrompt(referenceTimeZone.Id, minConfidence)),
            new DeepSeekMessage("user", BuildUserPrompt(candidates, referenceTimeZone))
        ];
    }

    private static string BuildSystemPrompt(string timeZoneId, double minConfidence)
    {
        return $$"""
            You review unresolved productivity items from one chat room and decide whether later messages prove they are already resolved.
            Return JSON only in the shape {"decisions":[...]}.

            Each decision object must contain:
            - candidate_id: string GUID from the input
            - should_resolve: boolean
            - resolution_kind: one of "completed", "missed", "cancelled", "rescheduled", or null when should_resolve is false
            - confidence: number from 0.0 to 1.0
            - resolved_at_utc: ISO-8601 UTC timestamp when there is explicit evidence, otherwise null
            - reason: short grounded explanation in Russian
            - evidence_message_ids: list of external message ids that support the decision

            Rules:
            - Use only the provided later messages as evidence.
            - Do not resolve an item unless the evidence is clear.
            - "waiting_on" usually resolves when the user clearly replied with a meaningful answer.
            - "commitment" and "task" resolve when later messages clearly indicate completion, sending, delivery, or acknowledgement.
             - "meeting" may resolve as:
               - "completed" when later messages imply the call or meeting happened
               - "rescheduled" when later messages explicitly move it
               - "cancelled" when later messages explicitly cancel it
               - "missed" only when the meeting time already passed and later messages strongly imply it did not happen
             - A meeting with due_at_utc in the FUTURE relative to the latest later_message timestamp can ONLY be resolved as "cancelled", never as "completed", "rescheduled", or "missed".
             - If evidence is weak or ambiguous, return should_resolve=false.
            - Confidence below {{minConfidence.ToString("0.00", CultureInfo.InvariantCulture)}} should usually mean should_resolve=false.
            - Reference timezone for relative dates is {{timeZoneId}}.
            """;
    }

    private static string BuildUserPrompt(
        IReadOnlyList<ConversationResolutionCandidate> candidates,
        TimeZoneInfo referenceTimeZone)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Candidates:");

        foreach (var candidate in candidates)
        {
            builder.AppendLine($"- candidate_id: {candidate.Id}");
            builder.AppendLine($"  type: {candidate.CandidateType}");
            builder.AppendLine($"  kind: {candidate.Kind}");
            builder.AppendLine($"  room_id: {candidate.ExternalChatId}");
            builder.AppendLine($"  observed_at_utc: {candidate.ObservedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}");
            builder.AppendLine($"  observed_at_local: {TimeZoneInfo.ConvertTime(candidate.ObservedAt, referenceTimeZone).ToString("O", CultureInfo.InvariantCulture)}");
            builder.AppendLine($"  due_at_utc: {(candidate.DueAt.HasValue ? candidate.DueAt.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) : "null")}");
            builder.AppendLine($"  person: {(string.IsNullOrWhiteSpace(candidate.Person) ? "null" : candidate.Person)}");
            builder.AppendLine($"  title: {candidate.Title}");
            builder.AppendLine($"  summary: {candidate.Summary}");
            builder.AppendLine("  later_messages:");

            if (candidate.LaterMessages.Count == 0)
            {
                builder.AppendLine("    - none");
            }

            foreach (var message in candidate.LaterMessages)
            {
                builder.AppendLine($"    - id: {message.ExternalMessageId}");
                builder.AppendLine($"      sent_at_utc: {message.SentAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}");
                builder.AppendLine($"      sender: {message.SenderName}");
                builder.AppendLine($"      text: {message.Text}");
            }
        }

        return builder.ToString().Trim();
    }
}
