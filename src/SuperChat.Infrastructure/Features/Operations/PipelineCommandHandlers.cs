using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rebus.Bus;
using Rebus.Handlers;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Contracts.Features.Intelligence.Meetings;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Intelligence.Extraction;
using SuperChat.Infrastructure.Features.Messaging;

namespace SuperChat.Infrastructure.Features.Operations;

internal sealed class ProcessConversationAfterSettleCommandHandler(
    IMessageNormalizationService normalizationService,
    IAiStructuredExtractionService extractionService,
    IWorkItemService workItemService,
    TimeProvider timeProvider,
    IWorkerRuntimeMonitor workerRuntimeMonitor,
    ILogger<ProcessConversationAfterSettleCommandHandler> logger) : IHandleMessages<ProcessConversationAfterSettleCommand>
{
    public async Task Handle(ProcessConversationAfterSettleCommand message)
    {
        PipelineWorkerRegistry.RegisterAll(workerRuntimeMonitor);

        try
        {
            workerRuntimeMonitor.MarkRunning(
                PipelineWorkerRegistry.ExtractionWorkerKey,
                PipelineWorkerRegistry.ExtractionWorkerDisplayName,
                $"Room={message.MatrixRoomId}");

            var pendingMessages = await normalizationService.GetPendingMessagesForConversationAsync(
                message.UserId,
                message.Source,
                message.MatrixRoomId,
                CancellationToken.None);
            if (pendingMessages.Count == 0)
            {
                workerRuntimeMonitor.MarkSucceeded(
                    PipelineWorkerRegistry.ExtractionWorkerKey,
                    PipelineWorkerRegistry.ExtractionWorkerDisplayName,
                    $"Room={message.MatrixRoomId}, No pending messages.");
                return;
            }

            var readyWindows = ConversationWindowSettlement.BuildReadyConversationWindows(
                pendingMessages,
                timeProvider.GetUtcNow());
            if (readyWindows.Count == 0)
            {
                workerRuntimeMonitor.MarkSucceeded(
                    PipelineWorkerRegistry.ExtractionWorkerKey,
                    PipelineWorkerRegistry.ExtractionWorkerDisplayName,
                    $"Room={message.MatrixRoomId}, No settled dialogue windows.");
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
            workerRuntimeMonitor.MarkSucceeded(
                PipelineWorkerRegistry.ExtractionWorkerKey,
                PipelineWorkerRegistry.ExtractionWorkerDisplayName,
                $"Room={message.MatrixRoomId}, Windows={readyWindows.Count}, Messages={processedIds.Count}");
        }
        catch (Exception exception)
        {
            workerRuntimeMonitor.MarkFailed(
                PipelineWorkerRegistry.ExtractionWorkerKey,
                PipelineWorkerRegistry.ExtractionWorkerDisplayName,
                exception,
                $"Room={message.MatrixRoomId}");
            logger.LogWarning(exception, "Deferred extraction failed for room {RoomId}.", message.MatrixRoomId);
            throw;
        }
    }
}

internal sealed class RebuildConversationChunksCommandHandler(
    IChunkBuilderService chunkBuilderService,
    IBus bus,
    IOptions<ChunkingOptions> chunkingOptions,
    IWorkerRuntimeMonitor workerRuntimeMonitor,
    ILogger<RebuildConversationChunksCommandHandler> logger) : IHandleMessages<RebuildConversationChunksCommand>
{
    public async Task Handle(RebuildConversationChunksCommand message)
    {
        PipelineWorkerRegistry.RegisterAll(workerRuntimeMonitor);

        if (!chunkingOptions.Value.Enabled)
        {
            workerRuntimeMonitor.MarkDisabled(
                PipelineWorkerRegistry.ChunkBuilderWorkerKey,
                PipelineWorkerRegistry.ChunkBuilderWorkerDisplayName,
                "Chunking is disabled.");
            return;
        }

        try
        {
            workerRuntimeMonitor.MarkRunning(
                PipelineWorkerRegistry.ChunkBuilderWorkerKey,
                PipelineWorkerRegistry.ChunkBuilderWorkerDisplayName,
                $"Room={message.MatrixRoomId}");

            var result = await chunkBuilderService.BuildConversationChunksAsync(
                message.UserId,
                message.MatrixRoomId,
                message.RebuildFrom,
                CancellationToken.None);

            workerRuntimeMonitor.MarkSucceeded(
                PipelineWorkerRegistry.ChunkBuilderWorkerKey,
                PipelineWorkerRegistry.ChunkBuilderWorkerDisplayName,
                $"Room={message.MatrixRoomId}, Rooms={result.RoomsRebuilt}, Chunks={result.ChunksWritten}, Messages={result.MessagesConsidered}");

            if (result.RoomsRebuilt > 0)
            {
                await bus.SendLocal(new IndexConversationChunksCommand(message.UserId, message.MatrixRoomId));
                await bus.SendLocal(new ProjectConversationMeetingsCommand(message.UserId, message.MatrixRoomId));
            }
        }
        catch (Exception exception)
        {
            workerRuntimeMonitor.MarkFailed(
                PipelineWorkerRegistry.ChunkBuilderWorkerKey,
                PipelineWorkerRegistry.ChunkBuilderWorkerDisplayName,
                exception,
                $"Room={message.MatrixRoomId}");
            logger.LogWarning(exception, "Chunk rebuild failed for room {RoomId}.", message.MatrixRoomId);
            throw;
        }
    }
}

internal sealed class IndexConversationChunksCommandHandler(
    IChunkIndexingService chunkIndexingService,
    IOptions<ChunkIndexingOptions> chunkIndexingOptions,
    IWorkerRuntimeMonitor workerRuntimeMonitor,
    ILogger<IndexConversationChunksCommandHandler> logger) : IHandleMessages<IndexConversationChunksCommand>
{
    public async Task Handle(IndexConversationChunksCommand message)
    {
        PipelineWorkerRegistry.RegisterAll(workerRuntimeMonitor);

        if (!chunkIndexingOptions.Value.Enabled)
        {
            workerRuntimeMonitor.MarkDisabled(
                PipelineWorkerRegistry.ChunkIndexingWorkerKey,
                PipelineWorkerRegistry.ChunkIndexingWorkerDisplayName,
                "Chunk indexing is disabled.");
            return;
        }

        try
        {
            workerRuntimeMonitor.MarkRunning(
                PipelineWorkerRegistry.ChunkIndexingWorkerKey,
                PipelineWorkerRegistry.ChunkIndexingWorkerDisplayName,
                $"Room={message.MatrixRoomId}");

            var result = await chunkIndexingService.IndexConversationChunksAsync(
                message.UserId,
                message.MatrixRoomId,
                CancellationToken.None);

            workerRuntimeMonitor.MarkSucceeded(
                PipelineWorkerRegistry.ChunkIndexingWorkerKey,
                PipelineWorkerRegistry.ChunkIndexingWorkerDisplayName,
                $"Room={message.MatrixRoomId}, Selected={result.ChunksSelected}, Indexed={result.ChunksIndexed}");
        }
        catch (Exception exception)
        {
            workerRuntimeMonitor.MarkFailed(
                PipelineWorkerRegistry.ChunkIndexingWorkerKey,
                PipelineWorkerRegistry.ChunkIndexingWorkerDisplayName,
                exception,
                $"Room={message.MatrixRoomId}");
            logger.LogWarning(exception, "Chunk indexing failed for room {RoomId}.", message.MatrixRoomId);
            throw;
        }
    }
}

internal sealed class ProjectConversationMeetingsCommandHandler(
    IMeetingProjectionService meetingProjectionService,
    IOptions<MeetingProjectionOptions> meetingProjectionOptions,
    IWorkerRuntimeMonitor workerRuntimeMonitor,
    ILogger<ProjectConversationMeetingsCommandHandler> logger) : IHandleMessages<ProjectConversationMeetingsCommand>
{
    public async Task Handle(ProjectConversationMeetingsCommand message)
    {
        PipelineWorkerRegistry.RegisterAll(workerRuntimeMonitor);

        if (!meetingProjectionOptions.Value.Enabled)
        {
            workerRuntimeMonitor.MarkDisabled(
                PipelineWorkerRegistry.MeetingProjectionWorkerKey,
                PipelineWorkerRegistry.MeetingProjectionWorkerDisplayName,
                "Meeting projection is disabled.");
            return;
        }

        try
        {
            workerRuntimeMonitor.MarkRunning(
                PipelineWorkerRegistry.MeetingProjectionWorkerKey,
                PipelineWorkerRegistry.MeetingProjectionWorkerDisplayName,
                $"Room={message.MatrixRoomId}");

            var result = await meetingProjectionService.ProjectConversationMeetingsAsync(
                message.UserId,
                message.MatrixRoomId,
                CancellationToken.None);

            workerRuntimeMonitor.MarkSucceeded(
                PipelineWorkerRegistry.MeetingProjectionWorkerKey,
                PipelineWorkerRegistry.MeetingProjectionWorkerDisplayName,
                $"Room={message.MatrixRoomId}, Rooms={result.RoomsRebuilt}, Meetings={result.MeetingsProjected}");
        }
        catch (Exception exception)
        {
            workerRuntimeMonitor.MarkFailed(
                PipelineWorkerRegistry.MeetingProjectionWorkerKey,
                PipelineWorkerRegistry.MeetingProjectionWorkerDisplayName,
                exception,
                $"Room={message.MatrixRoomId}");
            logger.LogWarning(exception, "Meeting projection failed for room {RoomId}.", message.MatrixRoomId);
            throw;
        }
    }
}
