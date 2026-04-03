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

internal sealed class OneWayClientPipelineCommandScheduler(
    IBus bus,
    IOptions<ChunkingOptions> chunkingOptions,
    IOptions<PipelineMessagingOptions> pipelineMessagingOptions,
    IOptions<PersistenceOptions> persistenceOptions,
    ILogger<OneWayClientPipelineCommandScheduler> logger) : IPipelineCommandScheduler
{
    public bool RequiresTransactionalDispatch =>
        string.Equals(persistenceOptions.Value.Provider, "Postgres", StringComparison.OrdinalIgnoreCase);

    public async Task DispatchNormalizedMessageStoredAsync(
        SuperChatDbContext dbContext,
        Guid userId,
        string source,
        string matrixRoomId,
        Guid normalizedMessageId,
        string matrixEventId,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        using var scope = MessagePipelineTrace.BeginScope(logger, userId, matrixRoomId, normalizedMessageId, matrixEventId);

        var queueName = pipelineMessagingOptions.Value.InputQueueName;
        var rebuildFrom = sentAt.AddMinutes(-Math.Max(1, chunkingOptions.Value.MaxGapMinutes));

        logger.LogInformation(
            "Dispatching one-way pipeline commands for normalized message. Queue={Queue}, Source={Source}, SentAt={SentAt}, RebuildFrom={RebuildFrom}, SettleDelaySeconds={SettleDelaySeconds}.",
            queueName,
            source,
            sentAt,
            rebuildFrom,
            ConversationWindowSettlement.SettleDelay.TotalSeconds);

        if (RequiresTransactionalDispatch)
        {
            if (dbContext.Database.CurrentTransaction is not IDbContextTransaction currentTransaction)
            {
                throw new InvalidOperationException("One-way pipeline dispatch requires an active database transaction.");
            }

            var dbTransaction = currentTransaction.GetDbTransaction();
            if (dbTransaction is not NpgsqlTransaction npgsqlTransaction)
            {
                throw new InvalidOperationException("One-way pipeline dispatch requires an Npgsql transaction.");
            }

            if (dbContext.Database.GetDbConnection() is not NpgsqlConnection npgsqlConnection)
            {
                throw new InvalidOperationException("One-way pipeline dispatch requires an Npgsql connection.");
            }

            using var rebusTransactionScope = new RebusTransactionScope();
            rebusTransactionScope.UseOutbox(npgsqlConnection, npgsqlTransaction);
            await DispatchAsync(queueName, userId, source, matrixRoomId, normalizedMessageId, matrixEventId, sentAt, rebuildFrom, cancellationToken);
            await rebusTransactionScope.CompleteAsync();
            return;
        }

        await DispatchAsync(queueName, userId, source, matrixRoomId, normalizedMessageId, matrixEventId, sentAt, rebuildFrom, cancellationToken);
    }

    private async Task DispatchAsync(
        string queueName,
        Guid userId,
        string source,
        string matrixRoomId,
        Guid normalizedMessageId,
        string matrixEventId,
        DateTimeOffset sentAt,
        DateTimeOffset rebuildFrom,
        CancellationToken cancellationToken)
    {
        await bus.Advanced.Routing.Defer(
            queueName,
            ConversationWindowSettlement.SettleDelay,
            new ProcessConversationAfterSettleCommand(userId, source, matrixRoomId, normalizedMessageId, matrixEventId));
        await bus.Advanced.Routing.Send(
            queueName,
            new RebuildConversationChunksCommand(
                userId,
                matrixRoomId,
                rebuildFrom,
                normalizedMessageId,
                matrixEventId));

        SuperChatMetrics.PipelineDispatchTotal.WithLabels("one_way", "process_conversation_after_settle").Inc();
        SuperChatMetrics.PipelineDispatchTotal.WithLabels("one_way", "rebuild_conversation_chunks").Inc();
        logger.LogInformation("One-way pipeline commands dispatched successfully. Queue={Queue}, SentAt={SentAt}.", queueName, sentAt);
    }
}
