using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    IOptions<ChunkingOptions> chunkingOptions) : IPipelineCommandScheduler
{
    public bool RequiresTransactionalDispatch => true;

    public async Task DispatchNormalizedMessageStoredAsync(
        SuperChatDbContext dbContext,
        Guid userId,
        string source,
        string matrixRoomId,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not IDbContextTransaction currentTransaction)
        {
            throw new InvalidOperationException("Normalized message dispatch requires an active database transaction.");
        }

        var dbTransaction = currentTransaction.GetDbTransaction();
        if (dbTransaction is not NpgsqlTransaction npgsqlTransaction)
        {
            throw new InvalidOperationException("Normalized message dispatch requires an Npgsql transaction.");
        }

        if (dbContext.Database.GetDbConnection() is not NpgsqlConnection npgsqlConnection)
        {
            throw new InvalidOperationException("Normalized message dispatch requires an Npgsql connection.");
        }

        using var rebusTransactionScope = new RebusTransactionScope();
        rebusTransactionScope.UseOutbox(npgsqlConnection, npgsqlTransaction);

        await bus.DeferLocal(
            ConversationWindowSettlement.SettleDelay,
            new ProcessConversationAfterSettleCommand(userId, source, matrixRoomId));
        await bus.SendLocal(new RebuildConversationChunksCommand(
            userId,
            matrixRoomId,
            sentAt.AddMinutes(-Math.Max(1, chunkingOptions.Value.MaxGapMinutes))));
        SuperChatMetrics.PipelineDispatchTotal.WithLabels("transactional", "process_conversation_after_settle").Inc();
        SuperChatMetrics.PipelineDispatchTotal.WithLabels("transactional", "rebuild_conversation_chunks").Inc();

        await rebusTransactionScope.CompleteAsync();
    }
}

internal sealed class NonTransactionalRebusPipelineCommandScheduler(
    IBus bus,
    IOptions<ChunkingOptions> chunkingOptions) : IPipelineCommandScheduler
{
    public bool RequiresTransactionalDispatch => false;

    public async Task DispatchNormalizedMessageStoredAsync(
        SuperChatDbContext dbContext,
        Guid userId,
        string source,
        string matrixRoomId,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        await bus.DeferLocal(
            ConversationWindowSettlement.SettleDelay,
            new ProcessConversationAfterSettleCommand(userId, source, matrixRoomId));
        await bus.SendLocal(new RebuildConversationChunksCommand(
            userId,
            matrixRoomId,
            sentAt.AddMinutes(-Math.Max(1, chunkingOptions.Value.MaxGapMinutes))));
        SuperChatMetrics.PipelineDispatchTotal.WithLabels("non_transactional", "process_conversation_after_settle").Inc();
        SuperChatMetrics.PipelineDispatchTotal.WithLabels("non_transactional", "rebuild_conversation_chunks").Inc();
    }
}

internal sealed class NoOpPipelineCommandScheduler : IPipelineCommandScheduler
{
    public bool RequiresTransactionalDispatch => false;

    public Task DispatchNormalizedMessageStoredAsync(
        SuperChatDbContext dbContext,
        Guid userId,
        string source,
        string matrixRoomId,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
