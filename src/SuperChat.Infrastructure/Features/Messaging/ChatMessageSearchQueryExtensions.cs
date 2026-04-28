using Microsoft.EntityFrameworkCore;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Messaging;

internal static class ChatMessageSearchQueryExtensions
{
    public static IQueryable<ChatMessageEntity> ApplySearchFilter(
        this IQueryable<ChatMessageEntity> source,
        Guid userId,
        string normalizedQuery)
    {
        var pattern = LikePatternEscaper.ToContainsPattern(normalizedQuery.ToLower());
        const string escape = LikePatternEscaper.EscapeCharacter;

        return source
            .Where(item => item.UserId == userId &&
                (EF.Functions.Like(item.Text.ToLower(), pattern, escape) ||
                 EF.Functions.Like(item.SenderName.ToLower(), pattern, escape) ||
                 EF.Functions.Like(item.ExternalChatId.ToLower(), pattern, escape)))
            .OrderByDescending(item => item.SentAt);
    }
}
