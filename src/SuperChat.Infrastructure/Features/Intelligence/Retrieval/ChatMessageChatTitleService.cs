using Microsoft.EntityFrameworkCore;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Intelligence.Retrieval;

/// <summary>
/// Подбирает читаемое имя чата, опираясь на самое свежее сообщение
/// в таблице <c>chat_messages</c>, у которого заполнено поле <c>chat_title</c>.
/// Стабильный <c>external_chat_id</c> остаётся ключом во всех таблицах,
/// а отображаемое имя вычисляется только на чтение.
/// </summary>
internal sealed class ChatMessageChatTitleService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory) : IChatTitleService
{
    public async Task<IReadOnlyDictionary<string, string>> ResolveManyAsync(
        Guid userId,
        IEnumerable<string> externalChatIds,
        CancellationToken cancellationToken)
    {
        var requestedIds = externalChatIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (requestedIds.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // SQLite не умеет ORDER BY по DateTimeOffset, поэтому сортировку делаем в памяти.
        // Объём данных небольшой: только сообщения с заполненным chat_title для нужных чатов одного пользователя.
        var rows = await dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => message.UserId == userId)
            .Where(message => requestedIds.Contains(message.ExternalChatId))
            .Where(message => message.ChatTitle != null && message.ChatTitle != string.Empty)
            .Select(message => new
            {
                message.ExternalChatId,
                message.ChatTitle,
                message.SentAt
            })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in rows.GroupBy(row => row.ExternalChatId, StringComparer.Ordinal))
        {
            var latestTitle = group
                .OrderByDescending(row => row.SentAt)
                .Select(row => row.ChatTitle)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(latestTitle))
            {
                result[group.Key] = latestTitle!.Trim();
            }
        }

        return result;
    }
}
