using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

internal static class ExtractedItemFilters
{
    internal const string GenericFollowUpCandidateTitle = "Follow-up candidate";

    internal static bool ShouldKeep(ExtractedItem item)
    {
        return !string.Equals(item.Title, GenericFollowUpCandidateTitle, StringComparison.Ordinal);
    }

    internal static bool ShouldKeep(ExtractedItemEntity item)
    {
        return !string.Equals(item.Title, GenericFollowUpCandidateTitle, StringComparison.Ordinal);
    }
}
