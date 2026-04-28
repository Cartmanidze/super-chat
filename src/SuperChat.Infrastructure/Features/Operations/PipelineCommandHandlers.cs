using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Rebus.Bus;
using Rebus.Handlers;
using SuperChat.Contracts.Features.Intelligence.Extraction;
using SuperChat.Contracts.Features.Intelligence.Meetings;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Contracts.Features.Messaging;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Diagnostics;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;
using System.Diagnostics;

namespace SuperChat.Infrastructure.Features.Operations;

internal sealed class ProcessConversationAfterSettleCommandHandler(
    IChatMessageStore normalizationService,
    IAiStructuredExtractionService extractionService,
    IWorkItemService workItemService,
    IBus bus,
    IOptions<ResolutionOptions> resolutionOptions,
    TimeProvider timeProvider,
    IHostApplicationLifetime applicationLifetime,
    ILogger<ProcessConversationAfterSettleCommandHandler> logger) : IHandleMessages<ProcessConversationAfterSettleCommand>
{
    private const string CommandName = "process_conversation_after_settle";

    public async Task Handle(ProcessConversationAfterSettleCommand message)
    {
        using var scope = MessagePipelineTrace.BeginScope(
            logger,
            message.UserId,
            message.ExternalChatId,
            message.TriggerMessageId,
            message.TriggerExternalMessageId);
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
                message.ExternalChatId,
                applicationLifetime.ApplicationStopping);
            if (pendingMessages.Count == 0)
            {
                logger.LogInformation(
                    "No pending chat messages found for processing. Command={CommandName}.",
                    CommandName);
                return;
            }

            logger.LogInformation(
                "Loaded pending chat messages. PendingCount={PendingCount}, PendingMessageIds={PendingMessageIds}, PendingEventIds={PendingEventIds}.",
                pendingMessages.Count,
                MessagePipelineTrace.SummarizeGuids(pendingMessages.Select(item => item.Id)),
                MessagePipelineTrace.SummarizeStrings(pendingMessages.Select(item => item.ExternalMessageId)));

            var readyWindows = ConversationWindowSettlement.BuildReadyConversationWindows(
                pendingMessages,
                timeProvider.GetUtcNow());
            if (readyWindows.Count == 0)
            {
                var retryDelay = ConversationWindowSettlement.GetNextRetryDelay(
                    pendingMessages,
                    timeProvider.GetUtcNow());
                if (retryDelay is not null)
                {
                    var bufferedRetryDelay = retryDelay.Value + TimeSpan.FromSeconds(1);
                    await bus.DeferLocal(
                        bufferedRetryDelay,
                        new ProcessConversationAfterSettleCommand(
                            message.UserId,
                            message.Source,
                            message.ExternalChatId,
                            message.TriggerMessageId,
                            message.TriggerExternalMessageId));

                    logger.LogInformation(
                        "No conversation windows are ready yet. PendingCount={PendingCount}, RetryDelayMs={RetryDelayMs}.",
                        pendingMessages.Count,
                        bufferedRetryDelay.TotalMilliseconds);
                    return;
                }

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
                    MessagePipelineTrace.SummarizeStrings(window.Messages.Select(item => item.ExternalMessageId)),
                    window.TsFrom,
                    window.TsTo);

                var items = await extractionService.ExtractAsync(window, applicationLifetime.ApplicationStopping);
                logger.LogInformation(
                    "Structured extraction completed for window. ExtractedItemCount={ExtractedItemCount}, ItemKinds={ItemKinds}.",
                    items.Count,
                    MessagePipelineTrace.SummarizeKinds(items));

                await workItemService.AcceptRangeAsync(items, applicationLifetime.ApplicationStopping);
                var resolutionDispatch = await DispatchResolutionCommandsAsync(message, items, applicationLifetime.ApplicationStopping);
                logger.LogInformation(
                    "Scheduled downstream resolution commands. ImmediateConversationResolves={ImmediateConversationResolves}, DeferredConversationResolves={DeferredConversationResolves}, DueMeetingResolves={DueMeetingResolves}.",
                    resolutionDispatch.ImmediateConversationResolves,
                    resolutionDispatch.DeferredConversationResolves,
                    resolutionDispatch.DueMeetingResolves);

                processedIds.AddRange(window.Messages.Select(item => item.Id));
            }

            await normalizationService.MarkProcessedAsync(processedIds, applicationLifetime.ApplicationStopping);
            logger.LogInformation(
                "Marked chat messages as processed. ProcessedCount={ProcessedCount}, ProcessedMessageIds={ProcessedMessageIds}.",
                processedIds.Count,
                MessagePipelineTrace.SummarizeGuids(processedIds));
        }
        catch (Exception exception)
        {
            result = "failed";
            logger.LogError(exception, "Deferred extraction failed for room {RoomId}.", message.ExternalChatId);
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
            message.ExternalChatId,
            message.TriggerMessageId,
            message.TriggerExternalMessageId));
        SuperChatMetrics.PipelineDispatchTotal.WithLabels("handler", "resolve_conversation_items").Inc();
        var deferredConversationResolves = 0;

        if (resolutionOptions.Value.ScheduleDeferredConversationPass && items.Count > 0)
        {
            var delay = TimeSpan.FromMinutes(Math.Max(1, resolutionOptions.Value.DeferredConversationDelayMinutes));
            await bus.DeferLocal(
                delay,
                new ResolveConversationItemsCommand(
                    message.UserId,
                    message.ExternalChatId,
                    message.TriggerMessageId,
                    message.TriggerExternalMessageId));
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
                    message.ExternalChatId,
                    resolveAfter,
                    message.TriggerMessageId,
                    message.TriggerExternalMessageId));
            }
            else
            {
                await bus.DeferLocal(
                    delay,
                    new ResolveDueMeetingsCommand(
                        message.UserId,
                        message.ExternalChatId,
                        resolveAfter,
                        message.TriggerMessageId,
                        message.TriggerExternalMessageId));
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
    IHostApplicationLifetime applicationLifetime,
    ILogger<RebuildConversationChunksCommandHandler> logger) : IHandleMessages<RebuildConversationChunksCommand>
{
    private const string CommandName = "rebuild_conversation_chunks";

    public async Task Handle(RebuildConversationChunksCommand message)
    {
        using var scope = MessagePipelineTrace.BeginScope(
            logger,
            message.UserId,
            message.ExternalChatId,
            message.TriggerMessageId,
            message.TriggerExternalMessageId);
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
                message.ExternalChatId,
                message.RebuildFrom,
                applicationLifetime.ApplicationStopping);
            logger.LogInformation(
                "Chunk rebuild completed. UsersProcessed={UsersProcessed}, RoomsRebuilt={RoomsRebuilt}, ChunksWritten={ChunksWritten}, MessagesConsidered={MessagesConsidered}.",
                buildResult.UsersProcessed,
                buildResult.RoomsRebuilt,
                buildResult.ChunksWritten,
                buildResult.MessagesConsidered);

            if (buildResult.RoomsRebuilt > 0)
            {
                await bus.SendLocal(new ProjectConversationMeetingsCommand(
                    message.UserId,
                    message.ExternalChatId,
                    message.TriggerMessageId,
                    message.TriggerExternalMessageId));
                await bus.SendLocal(new IndexConversationChunksCommand(
                    message.UserId,
                    message.ExternalChatId,
                    message.TriggerMessageId,
                    message.TriggerExternalMessageId));
                logger.LogInformation("Scheduled chunk indexing and meeting projection commands.");
            }
        }
        catch (Exception exception)
        {
            result = "failed";
            logger.LogError(exception, "Chunk rebuild failed for room {RoomId}.", message.ExternalChatId);
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
    IHostApplicationLifetime applicationLifetime,
    ILogger<IndexConversationChunksCommandHandler> logger) : IHandleMessages<IndexConversationChunksCommand>
{
    private const string CommandName = "index_conversation_chunks";

    public async Task Handle(IndexConversationChunksCommand message)
    {
        using var scope = MessagePipelineTrace.BeginScope(
            logger,
            message.UserId,
            message.ExternalChatId,
            message.TriggerMessageId,
            message.TriggerExternalMessageId);
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
                message.ExternalChatId,
                applicationLifetime.ApplicationStopping);
            logger.LogInformation(
                "Chunk indexing completed. ChunksSelected={ChunksSelected}, ChunksIndexed={ChunksIndexed}.",
                resultSummary.ChunksSelected,
                resultSummary.ChunksIndexed);
        }
        catch (Exception exception)
        {
            result = "failed";
            logger.LogError(exception, "Chunk indexing failed for room {RoomId}.", message.ExternalChatId);
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
    MeetingAutoResolutionService meetingAutoResolutionService,
    IOptions<MeetingProjectionOptions> meetingProjectionOptions,
    TimeProvider timeProvider,
    IHostApplicationLifetime applicationLifetime,
    ILogger<ProjectConversationMeetingsCommandHandler> logger) : IHandleMessages<ProjectConversationMeetingsCommand>
{
    private const string CommandName = "project_conversation_meetings";

    public async Task Handle(ProjectConversationMeetingsCommand message)
    {
        using var scope = MessagePipelineTrace.BeginScope(
            logger,
            message.UserId,
            message.ExternalChatId,
            message.TriggerMessageId,
            message.TriggerExternalMessageId);
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
                message.ExternalChatId,
                applicationLifetime.ApplicationStopping);
            logger.LogInformation(
                "Meeting projection completed. UsersProcessed={UsersProcessed}, RoomsRebuilt={RoomsRebuilt}, MeetingsProjected={MeetingsProjected}.",
                resultSummary.UsersProcessed,
                resultSummary.RoomsRebuilt,
                resultSummary.MeetingsProjected);

            if (resultSummary.RoomsRebuilt > 0)
            {
                await meetingAutoResolutionService.ResolveConversationAsync(
                    message.UserId,
                    message.ExternalChatId,
                    timeProvider.GetUtcNow(),
                    applicationLifetime.ApplicationStopping);
                logger.LogInformation(
                    "Completed post-projection meeting auto-resolution. RoomId={RoomId}.",
                    message.ExternalChatId);
            }
        }
        catch (Exception exception)
        {
            result = "failed";
            logger.LogError(exception, "Meeting projection failed for room {RoomId}.", message.ExternalChatId);
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
