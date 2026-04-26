using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Features.Messaging;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Diagnostics;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Messaging;

public sealed class MessageNormalizationService(
    IDbContextFactory<SuperChatDbContext> dbContextFactory,
    IPipelineCommandScheduler pipelineCommandScheduler,
    ILogger<MessageNormalizationService> logger) : IMessageNormalizationService
{
    public async Task<IReadOnlyList<NormalizedMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.NormalizedMessages
            .AsNoTracking()
            .Where(item => !item.Processed)
            .OrderBy(item => item.SentAt)
            .ThenBy(item => item.ReceivedAt)
            .ThenBy(item => item.Id)
            .Select(item => item.ToDomain())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NormalizedMessage>> GetPendingMessagesForConversationAsync(
        Guid userId,
        string source,
        string externalChatId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.NormalizedMessages
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

    public async Task<IReadOnlyList<NormalizedMessage>> GetRecentMessagesAsync(Guid userId, int take, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.NormalizedMessages
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.SentAt)
            .Take(take)
            .Select(item => item.ToDomain())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NormalizedMessage>> SearchRecentMessagesAsync(
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
        return await dbContext.NormalizedMessages
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
        var messages = await dbContext.NormalizedMessages
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
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var exists = await dbContext.NormalizedMessages
            .AsNoTracking()
            .AnyAsync(item => item.UserId == userId && item.ExternalChatId == externalChatId && item.ExternalMessageId == externalMessageId, cancellationToken);

        if (exists)
        {
            using var duplicateScope = MessagePipelineTrace.BeginScope(logger, userId, externalChatId, triggerExternalMessageId: externalMessageId);
            logger.LogInformation(
                "Skipped normalized message because it already exists. Source={Source}, SenderName={SenderName}, SentAt={SentAt}, TextLength={TextLength}, Preview={Preview}.",
                source,
                senderName,
                sentAt,
                text.Length,
                MessagePipelineTrace.CreatePreview(text));
            SuperChatMetrics.NormalizedMessagesDuplicateTotal.WithLabels(source).Inc();
            return false;
        }

        var normalizedMessageId = Guid.NewGuid();
        using var scope = MessagePipelineTrace.BeginScope(logger, userId, externalChatId, normalizedMessageId, externalMessageId);

        dbContext.NormalizedMessages.Add(new NormalizedMessageEntity
        {
            Id = normalizedMessageId,
            UserId = userId,
            Source = source,
            ExternalChatId = externalChatId,
            ExternalMessageId = externalMessageId,
            SenderName = senderName,
            Text = text,
            SentAt = sentAt,
            ReceivedAt = DateTimeOffset.UtcNow,
            Processed = false
        });

        logger.LogInformation(
            "Persisting normalized message. Source={Source}, SenderName={SenderName}, SentAt={SentAt}, TextLength={TextLength}, Preview={Preview}.",
            source,
            senderName,
            sentAt,
            text.Length,
            MessagePipelineTrace.CreatePreview(text));

        if (pipelineCommandScheduler.RequiresTransactionalDispatch)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await pipelineCommandScheduler.DispatchNormalizedMessageStoredAsync(
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
                    "Stored normalized message and dispatched pipeline commands transactionally. SentAt={SentAt}.",
                    sentAt);
                SuperChatMetrics.NormalizedMessagesStoredTotal.WithLabels(source).Inc();
                return true;
            }
            catch (DbUpdateException)
            {
                logger.LogInformation(
                    "Skipped normalized message after transactional save attempt because it was already written concurrently. SentAt={SentAt}.",
                    sentAt);
                SuperChatMetrics.NormalizedMessagesDuplicateTotal.WithLabels(source).Inc();
                return false;
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await pipelineCommandScheduler.DispatchNormalizedMessageStoredAsync(
                dbContext,
                userId,
                source,
                externalChatId,
                normalizedMessageId,
                externalMessageId,
                sentAt,
                cancellationToken);
            logger.LogInformation(
                "Stored normalized message and dispatched pipeline commands. SentAt={SentAt}.",
                sentAt);
            SuperChatMetrics.NormalizedMessagesStoredTotal.WithLabels(source).Inc();
            return true;
        }
        catch (DbUpdateException)
        {
            logger.LogInformation(
                "Skipped normalized message after save attempt because it was already written concurrently. SentAt={SentAt}.",
                sentAt);
            SuperChatMetrics.NormalizedMessagesDuplicateTotal.WithLabels(source).Inc();
            return false;
        }
    }
}
