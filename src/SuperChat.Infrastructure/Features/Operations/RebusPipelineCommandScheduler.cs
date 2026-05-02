using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Rebus.Bus;
using Rebus.Config.Outbox;
using Rebus.Transport;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Diagnostics;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Infrastructure.Features.Operations;

internal sealed class RebusPipelineCommandScheduler(
    IBus bus,
    IOptions<ChunkingOptions> chunkingOptions,
    ILogger<RebusPipelineCommandScheduler> logger) : IPipelineCommandScheduler
{
    public bool RequiresTransactionalDispatch => true;

    public async Task DispatchChatMessageStoredAsync(
        SuperChatDbContext dbContext,
        ChatMessageStoredEvent payload,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not IDbContextTransaction currentTransaction)
        {
            throw new InvalidOperationException("Chat message dispatch requires an active database transaction.");
        }

        var dbTransaction = currentTransaction.GetDbTransaction();
        if (dbTransaction is not NpgsqlTransaction npgsqlTransaction)
        {
            throw new InvalidOperationException("Chat message dispatch requires an Npgsql transaction.");
        }

        if (dbContext.Database.GetDbConnection() is not NpgsqlConnection npgsqlConnection)
        {
            throw new InvalidOperationException("Chat message dispatch requires an Npgsql connection.");
        }

        using var rebusTransactionScope = new RebusTransactionScope();
        rebusTransactionScope.UseOutbox(npgsqlConnection, npgsqlTransaction);
        using var scope = MessagePipelineTrace.BeginScope(
            logger,
            payload.UserId,
            payload.ExternalChatId,
            payload.ChatMessageId,
            payload.ExternalMessageId);

        var rebuildFrom = payload.SentAt.AddMinutes(-Math.Max(1, chunkingOptions.Value.MaxGapMinutes));
        logger.LogInformation(
            "Dispatching transactional pipeline commands for chat message. Source={Source}, SentAt={SentAt}, RebuildFrom={RebuildFrom}, SettleDelaySeconds={SettleDelaySeconds}.",
            payload.Source,
            payload.SentAt,
            rebuildFrom,
            ConversationWindowSettlement.SettleDelay.TotalSeconds);

        await bus.DeferLocal(
            ConversationWindowSettlement.SettleDelay,
            new ProcessConversationAfterSettleCommand(
                payload.UserId,
                payload.Source,
                payload.ExternalChatId,
                payload.ChatMessageId,
                payload.ExternalMessageId));
        await bus.SendLocal(new RebuildConversationChunksCommand(
            payload.UserId,
            payload.ExternalChatId,
            rebuildFrom,
            payload.ChatMessageId,
            payload.ExternalMessageId));
        SuperChatMetrics.PipelineDispatchTotal.WithLabels("transactional", "process_conversation_after_settle").Inc();
        SuperChatMetrics.PipelineDispatchTotal.WithLabels("transactional", "rebuild_conversation_chunks").Inc();

        await rebusTransactionScope.CompleteAsync();
        logger.LogInformation("Transactional pipeline commands dispatched successfully.");
    }
}

internal sealed class NonTransactionalRebusPipelineCommandScheduler(
    IBus bus,
    IOptions<ChunkingOptions> chunkingOptions,
    ILogger<NonTransactionalRebusPipelineCommandScheduler> logger) : IPipelineCommandScheduler
{
    public bool RequiresTransactionalDispatch => false;

    public async Task DispatchChatMessageStoredAsync(
        SuperChatDbContext dbContext,
        ChatMessageStoredEvent payload,
        CancellationToken cancellationToken)
    {
        using var scope = MessagePipelineTrace.BeginScope(
            logger,
            payload.UserId,
            payload.ExternalChatId,
            payload.ChatMessageId,
            payload.ExternalMessageId);

        var rebuildFrom = payload.SentAt.AddMinutes(-Math.Max(1, chunkingOptions.Value.MaxGapMinutes));
        logger.LogInformation(
            "Dispatching non-transactional pipeline commands for chat message. Source={Source}, SentAt={SentAt}, RebuildFrom={RebuildFrom}, SettleDelaySeconds={SettleDelaySeconds}.",
            payload.Source,
            payload.SentAt,
            rebuildFrom,
            ConversationWindowSettlement.SettleDelay.TotalSeconds);

        await bus.DeferLocal(
            ConversationWindowSettlement.SettleDelay,
            new ProcessConversationAfterSettleCommand(
                payload.UserId,
                payload.Source,
                payload.ExternalChatId,
                payload.ChatMessageId,
                payload.ExternalMessageId));
        await bus.SendLocal(new RebuildConversationChunksCommand(
            payload.UserId,
            payload.ExternalChatId,
            rebuildFrom,
            payload.ChatMessageId,
            payload.ExternalMessageId));
        SuperChatMetrics.PipelineDispatchTotal.WithLabels("non_transactional", "process_conversation_after_settle").Inc();
        SuperChatMetrics.PipelineDispatchTotal.WithLabels("non_transactional", "rebuild_conversation_chunks").Inc();
        logger.LogInformation("Non-transactional pipeline commands dispatched successfully.");
    }
}

internal sealed class NoOpPipelineCommandScheduler(
    ILogger<NoOpPipelineCommandScheduler> logger) : IPipelineCommandScheduler
{
    public bool RequiresTransactionalDispatch => false;

    public Task DispatchChatMessageStoredAsync(
        SuperChatDbContext dbContext,
        ChatMessageStoredEvent payload,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Skipping pipeline dispatch because scheduling is disabled. UserId={UserId}, ExternalChatId={ExternalChatId}, ExternalMessageId={ExternalMessageId}, Source={Source}.",
            payload.UserId,
            payload.ExternalChatId,
            payload.ExternalMessageId,
            payload.Source);
        SuperChatMetrics.PipelineDispatchSkippedTotal.WithLabels("no_op", "scheduling_disabled").Inc();
        return Task.CompletedTask;
    }
}
