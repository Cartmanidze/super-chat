using Microsoft.EntityFrameworkCore;
using SuperChat.Infrastructure.Features.Intelligence.Extraction;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.WorkItems;

internal static class WorkItemSearchQueryExtensions
{
    public static IQueryable<WorkItemEntity> ApplySearchFilter(
        this IQueryable<WorkItemEntity> source,
        Guid userId,
        string normalizedQuery)
    {
        var pattern = LikePatternEscaper.ToContainsPattern(normalizedQuery.ToLower());
        const string escape = LikePatternEscaper.EscapeCharacter;
        const string skippedTitle = ExtractedItemFilters.GenericFollowUpCandidateTitle;

        return source
            .Where(item => item.UserId == userId &&
                item.Title != skippedTitle &&
                (EF.Functions.Like(item.Title.ToLower(), pattern, escape) ||
                 EF.Functions.Like(item.Summary.ToLower(), pattern, escape) ||
                 EF.Functions.Like(item.ExternalChatId.ToLower(), pattern, escape)))
            .OrderByDescending(item => item.ObservedAt)
            .ThenBy(item => item.Id);
    }
}
