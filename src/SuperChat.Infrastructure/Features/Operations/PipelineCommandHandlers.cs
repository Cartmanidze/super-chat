using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rebus.Bus;
using Rebus.Handlers;
using SuperChat.Contracts.Features.Intelligence.Meetings;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Diagnostics;
using SuperChat.Infrastructure.Features.Intelligence.Extraction;
using SuperChat.Infrastructure.Features.Messaging;
using System.Diagnostics;

namespace SuperChat.Infrastructure.Features.Operations;

internal sealed class ProcessConversationAfterSettleCommandHandler(
    IMessageNormalizationService normalizationService,
    IAiStructuredExtractionService extractionService,
    IWorkItemService workItemService,
    TimeProvider timeProvider,
    ILogger<ProcessConversationAfterSettleCommandHandler> logger) : IHandleMessages<ProcessConversationAfterSettleCommand>
{
    private const string CommandName = "process_conversation_after_settle";

    public async Task Handle(ProcessConversationAfterSettleCommand message)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = "succeeded";
        SuperChatMetrics.PipelineCommandsInProgress.WithLabels(CommandName).Inc();

        try
        {
            var pendingMessages = await normalizationService.GetPendingMessagesForConversationAsync(
                message.UserId,
                message.Source,
                message.MatrixRoomId,
                CancellationToken.None);
            if (pendingMessages.Count == 0)
            {
                return;
            }

            var readyWindows = ConversationWindowSettlement.BuildReadyConversationWindows(
                pendingMessages,
                timeProvider.GetUtcNow());
            if (readyWindows.Count == 0)
            {
                return;
            }

            var processedIds = new List<Guid>(readyWindows.Sum(window => window.Messages.Count));
            foreach (var window in readyWindows)
            {
                var items = await extractionService.ExtractAsync(window, CancellationToken.None);
                await workItemService.IngestRangeAsync(items, CancellationToken.None);
                processedIds.AddRange(window.Messages.Select(item => item.Id));
            }

            await normalizationService.MarkProcessedAsync(processedIds, CancellationToken.None);
        }
        catch (Exception exception)
        {
            result = "failed";
            logger.LogWarning(exception, "Deferred extraction failed for room {RoomId}.", message.MatrixRoomId);
            throw;
        }
        finally
        {
            SuperChatMetrics.PipelineCommandsInProgress.WithLabels(CommandName).Dec();
            SuperChatMetrics.PipelineCommandsTotal.WithLabels(CommandName, result).Inc();
            SuperChatMetrics.PipelineCommandDurationSeconds.WithLabels(CommandName, result).Observe(stopwatch.Elapsed.TotalSeconds);
        }
    }
}

internal sealed class RebuildConversationChunksCommandHandler(
    IChunkBuilderService chunkBuilderService,
    IBus bus,
    IOptions<ChunkingOptions> chunkingOptions,
    ILogger<RebuildConversationChunksCommandHandler> logger) : IHandleMessages<RebuildConversationChunksCommand>
{
    private const string CommandName = "rebuild_conversation_chunks";

    public async Task Handle(RebuildConversationChunksCommand message)
    {
        if (!chunkingOptions.Value.Enabled)
        {
            SuperChatMetrics.PipelineCommandsTotal.WithLabels(CommandName, "disabled").Inc();
            SuperChatMetrics.PipelineCommandDurationSeconds.WithLabels(CommandName, "disabled").Observe(0);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var result = "succeeded";
        SuperChatMetrics.PipelineCommandsInProgress.WithLabels(CommandName).Inc();

        try
        {
            var buildResult = await chunkBuilderService.BuildConversationChunksAsync(
                message.UserId,
                message.MatrixRoomId,
                message.RebuildFrom,
                CancellationToken.None);

            if (buildResult.RoomsRebuilt > 0)
            {
                await bus.SendLocal(new IndexConversationChunksCommand(message.UserId, message.MatrixRoomId));
                await bus.SendLocal(new ProjectConversationMeetingsCommand(message.UserId, message.MatrixRoomId));
            }
        }
        catch (Exception exception)
        {
            result = "failed";
            logger.LogWarning(exception, "Chunk rebuild failed for room {RoomId}.", message.MatrixRoomId);
            throw;
        }
        finally
        {
            SuperChatMetrics.PipelineCommandsInProgress.WithLabels(CommandName).Dec();
            SuperChatMetrics.PipelineCommandsTotal.WithLabels(CommandName, result).Inc();
            SuperChatMetrics.PipelineCommandDurationSeconds.WithLabels(CommandName, result).Observe(stopwatch.Elapsed.TotalSeconds);
        }
    }
}

internal sealed class IndexConversationChunksCommandHandler(
    IChunkIndexingService chunkIndexingService,
    IOptions<ChunkIndexingOptions> chunkIndexingOptions,
    ILogger<IndexConversationChunksCommandHandler> logger) : IHandleMessages<IndexConversationChunksCommand>
{
    private const string CommandName = "index_conversation_chunks";

    public async Task Handle(IndexConversationChunksCommand message)
    {
        if (!chunkIndexingOptions.Value.Enabled)
        {
            SuperChatMetrics.PipelineCommandsTotal.WithLabels(CommandName, "disabled").Inc();
            SuperChatMetrics.PipelineCommandDurationSeconds.WithLabels(CommandName, "disabled").Observe(0);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var result = "succeeded";
        SuperChatMetrics.PipelineCommandsInProgress.WithLabels(CommandName).Inc();

        try
        {
            _ = await chunkIndexingService.IndexConversationChunksAsync(
                message.UserId,
                message.MatrixRoomId,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            result = "failed";
            logger.LogWarning(exception, "Chunk indexing failed for room {RoomId}.", message.MatrixRoomId);
            throw;
        }
        finally
        {
            SuperChatMetrics.PipelineCommandsInProgress.WithLabels(CommandName).Dec();
            SuperChatMetrics.PipelineCommandsTotal.WithLabels(CommandName, result).Inc();
            SuperChatMetrics.PipelineCommandDurationSeconds.WithLabels(CommandName, result).Observe(stopwatch.Elapsed.TotalSeconds);
        }
    }
}

internal sealed class ProjectConversationMeetingsCommandHandler(
    IMeetingProjectionService meetingProjectionService,
    IOptions<MeetingProjectionOptions> meetingProjectionOptions,
    ILogger<ProjectConversationMeetingsCommandHandler> logger) : IHandleMessages<ProjectConversationMeetingsCommand>
{
    private const string CommandName = "project_conversation_meetings";

    public async Task Handle(ProjectConversationMeetingsCommand message)
    {
        if (!meetingProjectionOptions.Value.Enabled)
        {
            SuperChatMetrics.PipelineCommandsTotal.WithLabels(CommandName, "disabled").Inc();
            SuperChatMetrics.PipelineCommandDurationSeconds.WithLabels(CommandName, "disabled").Observe(0);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var result = "succeeded";
        SuperChatMetrics.PipelineCommandsInProgress.WithLabels(CommandName).Inc();

        try
        {
            _ = await meetingProjectionService.ProjectConversationMeetingsAsync(
                message.UserId,
                message.MatrixRoomId,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            result = "failed";
            logger.LogWarning(exception, "Meeting projection failed for room {RoomId}.", message.MatrixRoomId);
            throw;
        }
        finally
        {
            SuperChatMetrics.PipelineCommandsInProgress.WithLabels(CommandName).Dec();
            SuperChatMetrics.PipelineCommandsTotal.WithLabels(CommandName, result).Inc();
            SuperChatMetrics.PipelineCommandDurationSeconds.WithLabels(CommandName, result).Observe(stopwatch.Elapsed.TotalSeconds);
        }
    }
}
