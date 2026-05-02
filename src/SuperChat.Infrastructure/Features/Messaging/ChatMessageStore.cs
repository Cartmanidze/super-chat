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
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var exists = await dbContext.ChatMessages
            .AsNoTracking()
            .AnyAsync(item => item.UserId == userId && item.ExternalChatId == externalChatId && item.ExternalMessageId == externalMessageId, cancellationToken);

        if (exists)
        {
            using var duplicateScope = MessagePipelineTrace.BeginScope(logger, userId, externalChatId, triggerExternalMessageId: externalMessageId);
            // Preview содержимого сообщения не пишем в Information-лог — оно может уехать
            // в Loki/Grafana, к которым имеют доступ ops, и это утечка приватной переписки.
            // На уровне Debug превью добавлено отдельной строкой ниже.
            logger.LogInformation(
                "Skipped chat message because it already exists. Source={Source}, SenderName={SenderName}, SentAt={SentAt}, TextLength={TextLength}.",
                source,
                senderName,
                sentAt,
                text.Length);
            logger.LogDebug(
                "Skipped duplicate message preview: {Preview}",
                MessagePipelineTrace.CreatePreview(text));
            SuperChatMetrics.ChatMessagesDuplicateTotal.WithLabels(source).Inc();
            return false;
        }

        var normalizedMessageId = Guid.NewGuid();
        using var scope = MessagePipelineTrace.BeginScope(logger, userId, externalChatId, normalizedMessageId, externalMessageId);

        dbContext.ChatMessages.Add(new ChatMessageEntity
        {
            Id = normalizedMessageId,
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

        logger.LogInformation(
            "Persisting chat message. Source={Source}, SenderName={SenderName}, SentAt={SentAt}, TextLength={TextLength}.",
            source,
            senderName,
            sentAt,
            text.Length);
        logger.LogDebug(
            "Persisted message preview: {Preview}",
            MessagePipelineTrace.CreatePreview(text));

        if (pipelineCommandScheduler.RequiresTransactionalDispatch)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await pipelineCommandScheduler.DispatchChatMessageStoredAsync(
                    dbContext,
                    userId,
                    source,
                    externalChatId,
                    normalizedMessageId,
                    externalMessageId,
                    sentAt,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogInformation(
                    "Stored chat message and dispatched pipeline commands transactionally. SentAt={SentAt}.",
                    sentAt);
                SuperChatMetrics.ChatMessagesStoredTotal.WithLabels(source).Inc();
                return true;
            }
            catch (DbUpdateException)
            {
                logger.LogInformation(
                    "Skipped chat message after transactional save attempt because it was already written concurrently. SentAt={SentAt}.",
                    sentAt);
                SuperChatMetrics.ChatMessagesDuplicateTotal.WithLabels(source).Inc();
                return false;
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await pipelineCommandScheduler.DispatchChatMessageStoredAsync(
                dbContext,
                userId,
                source,
                externalChatId,
                normalizedMessageId,
                externalMessageId,
                sentAt,
                cancellationToken);
            logger.LogInformation(
                "Stored chat message and dispatched pipeline commands. SentAt={SentAt}.",
                sentAt);
            SuperChatMetrics.ChatMessagesStoredTotal.WithLabels(source).Inc();
            return true;
        }
        catch (DbUpdateException)
        {
            logger.LogInformation(
                "Skipped chat message after save attempt because it was already written concurrently. SentAt={SentAt}.",
                sentAt);
            SuperChatMetrics.ChatMessagesDuplicateTotal.WithLabels(source).Inc();
            return false;
        }
    }
}
