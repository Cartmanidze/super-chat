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
    IBus bus,
    IOptions<ResolutionOptions> resolutionOptions,
    TimeProvider timeProvider,
    ILogger<ProcessConversationAfterSettleCommandHandler> logger) : IHandleMessages<ProcessConversationAfterSettleCommand>
{
    private const string CommandName = "process_conversation_after_settle";

    public async Task Handle(ProcessConversationAfterSettleCommand message)
    {
        using var scope = MessagePipelineTrace.BeginScope(
            logger,
            message.UserId,
            message.MatrixRoomId,
            message.TriggerMessageId,
            message.TriggerMatrixEventId);
        var stopwatch = Stopwatch.StartNew();
        var result = "succeeded";
        SuperChatMetrics.PipelineCommandsInProgress.WithLabels(CommandName).Inc();

        try
        {
            logger.LogInformation(
                "Pipeline command started. Command={CommandName}, Source={Source}.",
                CommandName,
                message.Source);

            var pendingMessages = await normalizationService.GetPendingMessagesForConversationAsync(
                message.UserId,
                message.Source,
                message.MatrixRoomId,
                CancellationToken.None);
            if (pendingMessages.Count == 0)
            {
                logger.LogInformation(
                    "No pending normalized messages found for processing. Command={CommandName}.",
                    CommandName);
                return;
            }

            logger.LogInformation(
                "Loaded pending normalized messages. PendingCount={PendingCount}, PendingMessageIds={PendingMessageIds}, PendingEventIds={PendingEventIds}.",
                pendingMessages.Count,
                MessagePipelineTrace.SummarizeGuids(pendingMessages.Select(item => item.Id)),
                MessagePipelineTrace.SummarizeStrings(pendingMessages.Select(item => item.MatrixEventId)));

            var readyWindows = ConversationWindowSettlement.BuildReadyConversationWindows(
                pendingMessages,
                timeProvider.GetUtcNow());
            if (readyWindows.Count == 0)
            {
                logger.LogInformation(
                    "No conversation windows are ready yet. PendingCount={PendingCount}.",
                    pendingMessages.Count);
                return;
            }

            logger.LogInformation(
                "Built ready conversation windows. ReadyWindowCount={ReadyWindowCount}.",
                readyWindows.Count);

            var processedIds = new List<Guid>(readyWindows.Sum(window => window.Messages.Count));
            foreach (var window in readyWindows)
            {
                logger.LogInformation(
                    "Processing ready conversation window. MessageCount={MessageCount}, EventIds={EventIds}, TsFrom={TsFrom}, TsTo={TsTo}.",
                    window.Messages.Count,
                    MessagePipelineTrace.SummarizeStrings(window.Messages.Select(item => item.MatrixEventId)),
                    window.TsFrom,
                    window.TsTo);

                var items = await extractionService.ExtractAsync(window, CancellationToken.None);
                logger.LogInformation(
                    "Structured extraction completed for window. ExtractedItemCount={ExtractedItemCount}, ItemKinds={ItemKinds}.",
                    items.Count,
                    MessagePipelineTrace.SummarizeKinds(items));

                await workItemService.IngestRangeAsync(items, CancellationToken.None);
                var resolutionDispatch = await DispatchResolutionCommandsAsync(message, items, CancellationToken.None);
                logger.LogInformation(
                    "Scheduled downstream resolution commands. ImmediateConversationResolves={ImmediateConversationResolves}, DeferredConversationResolves={DeferredConversationResolves}, DueMeetingResolves={DueMeetingResolves}.",
                    resolutionDispatch.ImmediateConversationResolves,
                    resolutionDispatch.DeferredConversationResolves,
                    resolutionDispatch.DueMeetingResolves);

                processedIds.AddRange(window.Messages.Select(item => item.Id));
            }

            await normalizationService.MarkProcessedAsync(processedIds, CancellationToken.None);
            logger.LogInformation(
                "Marked normalized messages as processed. ProcessedCount={ProcessedCount}, ProcessedMessageIds={ProcessedMessageIds}.",
                processedIds.Count,
                MessagePipelineTrace.SummarizeGuids(processedIds));
        }
        catch (Exception exception)
        {
            result = "failed";
            logger.LogWarning(exception, "Deferred extraction failed for room {RoomId}.", message.MatrixRoomId);
            throw;
        }
        finally
        {
            logger.LogInformation(
                "Pipeline command completed. Command={CommandName}, Result={Result}, ElapsedMs={ElapsedMs}.",
                CommandName,
                result,
                stopwatch.ElapsedMilliseconds);
            SuperChatMetrics.PipelineCommandsInProgress.WithLabels(CommandName).Dec();
            SuperChatMetrics.PipelineCommandsTotal.WithLabels(CommandName, result).Inc();
            SuperChatMetrics.PipelineCommandDurationSeconds.WithLabels(CommandName, result).Observe(stopwatch.Elapsed.TotalSeconds);
        }
    }

    private async Task<ResolutionDispatchSummary> DispatchResolutionCommandsAsync(
        ProcessConversationAfterSettleCommand message,
        IReadOnlyCollection<ExtractedItem> items,
        CancellationToken cancellationToken)
    {
        if (!resolutionOptions.Value.Enabled)
        {
            return ResolutionDispatchSummary.Empty;
        }

        await bus.SendLocal(new ResolveConversationItemsCommand(
            message.UserId,
            message.MatrixRoomId,
            message.TriggerMessageId,
            message.TriggerMatrixEventId));
        SuperChatMetrics.PipelineDispatchTotal.WithLabels("handler", "resolve_conversation_items").Inc();
        var deferredConversationResolves = 0;

        if (resolutionOptions.Value.ScheduleDeferredConversationPass)
        {
            var delay = TimeSpan.FromMinutes(Math.Max(1, resolutionOptions.Value.DeferredConversationDelayMinutes));
            await bus.DeferLocal(
                delay,
                new ResolveConversationItemsCommand(
                    message.UserId,
                    message.MatrixRoomId,
                    message.TriggerMessageId,
                    message.TriggerMatrixEventId));
            SuperChatMetrics.PipelineDispatchTotal.WithLabels("handler", "resolve_conversation_items_deferred").Inc();
            deferredConversationResolves = 1;
        }

        var dueMeetingTimes = items
            .Where(item => item.Kind == ExtractedItemKind.Meeting && item.DueAt is not null)
            .Select(item => item.DueAt!.Value)
            .Distinct()
            .ToList();

        foreach (var scheduledFor in dueMeetingTimes)
        {
            var resolveAfter = scheduledFor.AddMinutes(Math.Max(0, resolutionOptions.Value.MeetingGracePeriodMinutes));
            var delay = resolveAfter - timeProvider.GetUtcNow();
            if (delay <= TimeSpan.Zero)
            {
                await bus.SendLocal(new ResolveDueMeetingsCommand(
                    message.UserId,
                    message.MatrixRoomId,
                    resolveAfter,
                    message.TriggerMessageId,
                    message.TriggerMatrixEventId));
            }
            else
            {
                await bus.DeferLocal(
                    delay,
                    new ResolveDueMeetingsCommand(
                        message.UserId,
                        message.MatrixRoomId,
                        resolveAfter,
                        message.TriggerMessageId,
                        message.TriggerMatrixEventId));
            }

            SuperChatMetrics.PipelineDispatchTotal.WithLabels("handler", "resolve_due_meetings").Inc();
        }

        return new ResolutionDispatchSummary(
            1,
            deferredConversationResolves,
            dueMeetingTimes.Count);
    }

    private readonly record struct ResolutionDispatchSummary(
        int ImmediateConversationResolves,
        int DeferredConversationResolves,
        int DueMeetingResolves)
    {
        public static ResolutionDispatchSummary Empty => new(0, 0, 0);
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
        using var scope = MessagePipelineTrace.BeginScope(
            logger,
            message.UserId,
            message.MatrixRoomId,
            message.TriggerMessageId,
            message.TriggerMatrixEventId);
        if (!chunkingOptions.Value.Enabled)
        {
            logger.LogInformation("Skipping chunk rebuild because chunking is disabled.");
            SuperChatMetrics.PipelineCommandsTotal.WithLabels(CommandName, "disabled").Inc();
            SuperChatMetrics.PipelineCommandDurationSeconds.WithLabels(CommandName, "disabled").Observe(0);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var result = "succeeded";
        SuperChatMetrics.PipelineCommandsInProgress.WithLabels(CommandName).Inc();

        try
        {
            logger.LogInformation(
                "Pipeline command started. Command={CommandName}, RebuildFrom={RebuildFrom}.",
                CommandName,
                message.RebuildFrom);

            var buildResult = await chunkBuilderService.BuildConversationChunksAsync(
                message.UserId,
                message.MatrixRoomId,
                message.RebuildFrom,
                CancellationToken.None);
            logger.LogInformation(
                "Chunk rebuild completed. UsersProcessed={UsersProcessed}, RoomsRebuilt={RoomsRebuilt}, ChunksWritten={ChunksWritten}, MessagesConsidered={MessagesConsidered}.",
                buildResult.UsersProcessed,
                buildResult.RoomsRebuilt,
                buildResult.ChunksWritten,
                buildResult.MessagesConsidered);

            if (buildResult.RoomsRebuilt > 0)
            {
                await bus.SendLocal(new IndexConversationChunksCommand(
                    message.UserId,
                    message.MatrixRoomId,
                    message.TriggerMessageId,
                    message.TriggerMatrixEventId));
                await bus.SendLocal(new ProjectConversationMeetingsCommand(
                    message.UserId,
                    message.MatrixRoomId,
                    message.TriggerMessageId,
                    message.TriggerMatrixEventId));
                logger.LogInformation("Scheduled chunk indexing and meeting projection commands.");
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
            logger.LogInformation(
                "Pipeline command completed. Command={CommandName}, Result={Result}, ElapsedMs={ElapsedMs}.",
                CommandName,
                result,
                stopwatch.ElapsedMilliseconds);
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
        using var scope = MessagePipelineTrace.BeginScope(
            logger,
            message.UserId,
            message.MatrixRoomId,
            message.TriggerMessageId,
            message.TriggerMatrixEventId);
        if (!chunkIndexingOptions.Value.Enabled)
        {
            logger.LogInformation("Skipping chunk indexing because indexing is disabled.");
            SuperChatMetrics.PipelineCommandsTotal.WithLabels(CommandName, "disabled").Inc();
            SuperChatMetrics.PipelineCommandDurationSeconds.WithLabels(CommandName, "disabled").Observe(0);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var result = "succeeded";
        SuperChatMetrics.PipelineCommandsInProgress.WithLabels(CommandName).Inc();

        try
        {
            logger.LogInformation("Pipeline command started. Command={CommandName}.", CommandName);
            var resultSummary = await chunkIndexingService.IndexConversationChunksAsync(
                message.UserId,
                message.MatrixRoomId,
                CancellationToken.None);
            logger.LogInformation(
                "Chunk indexing completed. ChunksSelected={ChunksSelected}, ChunksIndexed={ChunksIndexed}.",
                resultSummary.ChunksSelected,
                resultSummary.ChunksIndexed);
        }
        catch (Exception exception)
        {
            result = "failed";
            logger.LogWarning(exception, "Chunk indexing failed for room {RoomId}.", message.MatrixRoomId);
            throw;
        }
        finally
        {
            logger.LogInformation(
                "Pipeline command completed. Command={CommandName}, Result={Result}, ElapsedMs={ElapsedMs}.",
                CommandName,
                result,
                stopwatch.ElapsedMilliseconds);
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
        using var scope = MessagePipelineTrace.BeginScope(
            logger,
            message.UserId,
            message.MatrixRoomId,
            message.TriggerMessageId,
            message.TriggerMatrixEventId);
        if (!meetingProjectionOptions.Value.Enabled)
        {
            logger.LogInformation("Skipping meeting projection because projection is disabled.");
            SuperChatMetrics.PipelineCommandsTotal.WithLabels(CommandName, "disabled").Inc();
            SuperChatMetrics.PipelineCommandDurationSeconds.WithLabels(CommandName, "disabled").Observe(0);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var result = "succeeded";
        SuperChatMetrics.PipelineCommandsInProgress.WithLabels(CommandName).Inc();

        try
        {
            logger.LogInformation("Pipeline command started. Command={CommandName}.", CommandName);
            var resultSummary = await meetingProjectionService.ProjectConversationMeetingsAsync(
                message.UserId,
                message.MatrixRoomId,
                CancellationToken.None);
            logger.LogInformation(
                "Meeting projection completed. UsersProcessed={UsersProcessed}, RoomsRebuilt={RoomsRebuilt}, MeetingsProjected={MeetingsProjected}.",
                resultSummary.UsersProcessed,
                resultSummary.RoomsRebuilt,
                resultSummary.MeetingsProjected);
        }
        catch (Exception exception)
        {
            result = "failed";
            logger.LogWarning(exception, "Meeting projection failed for room {RoomId}.", message.MatrixRoomId);
            throw;
        }
        finally
        {
            logger.LogInformation(
                "Pipeline command completed. Command={CommandName}, Result={Result}, ElapsedMs={ElapsedMs}.",
                CommandName,
                result,
                stopwatch.ElapsedMilliseconds);
            SuperChatMetrics.PipelineCommandsInProgress.WithLabels(CommandName).Dec();
            SuperChatMetrics.PipelineCommandsTotal.WithLabels(CommandName, result).Inc();
            SuperChatMetrics.PipelineCommandDurationSeconds.WithLabels(CommandName, result).Observe(stopwatch.Elapsed.TotalSeconds);
        }
    }
}
