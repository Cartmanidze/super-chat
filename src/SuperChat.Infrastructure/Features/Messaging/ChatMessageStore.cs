using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Features.Messaging;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Diagnostics;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Messaging;

public sealed class ChatMessageStore(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IPipelineCommandScheduler pipelineCommandScheduler,
    ILogger<ChatMessageStore> logger) : IChatMessageStore
{
    public async Task<IReadOnlyList<ChatMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.ChatMessages
            .AsNoTracking()
            .Where(item => !item.Processed)
            .OrderBy(item => item.SentAt)
            .ThenBy(item => item.ReceivedAt)
            .ThenBy(item => item.Id)
            .Select(item => item.ToDomain())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetPendingMessagesForConversationAsync(
        Guid userId,
        string source,
        string externalChatId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.ChatMessages
            .AsNoTracking()
            .Where(item => item.UserId == userId &&
                           item.Source == source &&
                           item.ExternalChatId == externalChatId &&
                           !item.Processed)
            .OrderBy(item => item.SentAt)
            .ThenBy(item => item.ReceivedAt)
            .ThenBy(item => item.Id)
            .Select(item => item.ToDomain())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(Guid userId, int take, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.ChatMessages
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.SentAt)
            .Take(take)
            .Select(item => item.ToDomain())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> SearchRecentMessagesAsync(
        Guid userId,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0 || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.ChatMessages
            .AsNoTracking()
            .ApplySearchFilter(userId, query.Trim())
            .Take(limit)
            .Select(item => item.ToDomain())
            .ToListAsync(cancellationToken);
    }

    public async Task MarkProcessedAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken)
    {
        var ids = messageIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var messages = await dbContext.ChatMessages
            .Where(item => ids.Contains(item.Id))
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.Processed = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryStoreAsync(
        Guid userId,
        string source,
        string externalChatId,
        string externalMessageId,
        string senderName,
        string text,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken,
        string? chatTitle = null,
        bool isOutgoing = false)
    {
        // Дедуп — двухэтапный. Источник истины — unique-индекс
        // (user_id, external_chat_id, external_message_id) на уровне БД: повторный insert
        // ловится в catch DbUpdateException и трактуется как дубликат. Опережающий
        // AnyAsync ниже — оптимизация: на типичном повторе sidecar-а (ретрай при
        // нестабильной сети) экономит лишний INSERT-attempt + unique-violation
        // exception, плюс закрывает кейс in-memory тестового провайдера, который
        // unique-constraint не уважает.
        //
        // TODO(privacy): Text сейчас лежит в БД plain-text. На случай leak-а dump-а
        // имеет смысл шифровать его симметричным ключом (как auth_key в telegram_sessions),
        // но это отдельный проект — нужен ключ-роутер, миграция существующих записей
        // и решение, как давать поиск по содержимому.
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var alreadyExists = await dbContext.ChatMessages
            .AsNoTracking()
            .AnyAsync(
                item => item.UserId == userId &&
                        item.ExternalChatId == externalChatId &&
                        item.ExternalMessageId == externalMessageId,
                cancellationToken);
        if (alreadyExists)
        {
            using var dupScope = MessagePipelineTrace.BeginScope(logger, userId, externalChatId, triggerExternalMessageId: externalMessageId);
            LogDuplicate(source, senderName, sentAt, text);
            SuperChatMetrics.ChatMessagesDuplicateTotal.WithLabels(source).Inc();
            return false;
        }

        var chatMessageId = Guid.NewGuid();
        using var scope = MessagePipelineTrace.BeginScope(logger, userId, externalChatId, chatMessageId, externalMessageId);

        dbContext.ChatMessages.Add(new ChatMessageEntity
        {
            Id = chatMessageId,
            UserId = userId,
            Source = source,
            ExternalChatId = externalChatId,
            ExternalMessageId = externalMessageId,
            ChatTitle = string.IsNullOrWhiteSpace(chatTitle) ? null : chatTitle,
            SenderName = senderName,
            Text = text,
            SentAt = sentAt,
            ReceivedAt = DateTimeOffset.UtcNow,
            Processed = false,
            IsOutgoing = isOutgoing
        });

        var payload = new ChatMessageStoredEvent(userId, source, externalChatId, chatMessageId, externalMessageId, sentAt);

        if (pipelineCommandScheduler.RequiresTransactionalDispatch)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await pipelineCommandScheduler.DispatchChatMessageStoredAsync(dbContext, payload, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                LogStored(source, senderName, sentAt, text);
                SuperChatMetrics.ChatMessagesStoredTotal.WithLabels(source).Inc();
                return true;
            }
            catch (DbUpdateException)
            {
                LogDuplicate(source, senderName, sentAt, text);
                SuperChatMetrics.ChatMessagesDuplicateTotal.WithLabels(source).Inc();
                return false;
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await pipelineCommandScheduler.DispatchChatMessageStoredAsync(dbContext, payload, cancellationToken);
            LogStored(source, senderName, sentAt, text);
            SuperChatMetrics.ChatMessagesStoredTotal.WithLabels(source).Inc();
            return true;
        }
        catch (DbUpdateException)
        {
            LogDuplicate(source, senderName, sentAt, text);
            SuperChatMetrics.ChatMessagesDuplicateTotal.WithLabels(source).Inc();
            return false;
        }
    }

    private void LogStored(string source, string senderName, DateTimeOffset sentAt, string text)
    {
        // Information — без preview, чтобы переписка не уезжала в Loki/Grafana.
        // На уровне Debug отдельная строка с превью — для дев-стенда.
        logger.LogInformation(
            "Stored chat message. Source={Source}, SenderName={SenderName}, SentAt={SentAt}, TextLength={TextLength}.",
            source,
            senderName,
            sentAt,
            text.Length);
        logger.LogDebug("Stored message preview: {Preview}", MessagePipelineTrace.CreatePreview(text));
    }

    private void LogDuplicate(string source, string senderName, DateTimeOffset sentAt, string text)
    {
        logger.LogInformation(
            "Skipped duplicate chat message. Source={Source}, SenderName={SenderName}, SentAt={SentAt}, TextLength={TextLength}.",
            source,
            senderName,
            sentAt,
            text.Length);
        logger.LogDebug("Skipped duplicate preview: {Preview}", MessagePipelineTrace.CreatePreview(text));
    }
}
