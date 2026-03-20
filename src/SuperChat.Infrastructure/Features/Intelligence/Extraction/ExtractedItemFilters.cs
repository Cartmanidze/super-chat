using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.Extraction;

internal static class ExtractedItemFilters
{
    internal const string GenericFollowUpCandidateTitle = "Follow-up candidate";

    internal static bool ShouldKeep(ExtractedItem item)
    {
        return !string.Equals(item.Title, GenericFollowUpCandidateTitle, StringComparison.Ordinal) &&
               !StructuredArtifactDetector.LooksLikeStructuredArtifact(BuildArtifactText(item.Title, item.Summary));
    }

    internal static bool ShouldKeep(ExtractedItemEntity item)
    {
        return !string.Equals(item.Title, GenericFollowUpCandidateTitle, StringComparison.Ordinal) &&
               !StructuredArtifactDetector.LooksLikeStructuredArtifact(BuildArtifactText(item.Title, item.Summary));
    }

    internal static bool ShouldKeep(WorkItemEntity item)
    {
        return !string.Equals(item.Title, GenericFollowUpCandidateTitle, StringComparison.Ordinal) &&
               !StructuredArtifactDetector.LooksLikeStructuredArtifact(BuildArtifactText(item.Title, item.Summary));
    }

    private static string BuildArtifactText(string title, string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return title;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return summary;
        }

        return $"{title}{Environment.NewLine}{summary}";
    }
}
