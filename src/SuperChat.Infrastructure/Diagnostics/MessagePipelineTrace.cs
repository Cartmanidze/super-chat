using Microsoft.Extensions.Logging;
using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Infrastructure.Diagnostics;

internal static class MessagePipelineTrace
{
    private const int DefaultSampleLimit = 5;
    private const int DefaultPreviewLength = 96;

    public static IDisposable? BeginScope(
        ILogger logger,
        Guid userId,
        string externalChatId,
        Guid? triggerMessageId = null,
        string? triggerExternalMessageId = null)
    {
        return logger.BeginScope(new Dictionary<string, object?>
        {
            ["PipelineUserId"] = userId,
            ["PipelineExternalChatId"] = externalChatId,
            ["PipelineTriggerMessageId"] = triggerMessageId,
            ["PipelineTriggerExternalMessageId"] = triggerExternalMessageId
        });
    }

    public static string SummarizeGuids(IEnumerable<Guid> values, int limit = DefaultSampleLimit)
    {
        return SummarizeStrings(values.Select(value => value.ToString("D")), limit);
    }

    public static string SummarizeStrings(IEnumerable<string?> values, int limit = DefaultSampleLimit)
    {
        var materialized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(Math.Max(1, limit) + 1)
            .ToList();

        if (materialized.Count == 0)
        {
            return "none";
        }

        var hasMore = materialized.Count > limit;
        var selected = hasMore
            ? materialized.Take(limit)
            : materialized;

        return hasMore
            ? $"{string.Join(", ", selected)} (+more)"
            : string.Join(", ", selected);
    }

    public static string SummarizeKinds(IEnumerable<ExtractedItem> items)
    {
        var summary = items
            .GroupBy(item => item.Kind)
            .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal)
            .Select(group => $"{group.Key}:{group.Count()}")
            .ToList();

        return summary.Count == 0
            ? "none"
            : string.Join(", ", summary);
    }

    public static string CreatePreview(string? text, int maxLength = DefaultPreviewLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text
            .ReplaceLineEndings(" ")
            .Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..Math.Max(1, maxLength - 3)] + "...";
    }
}
