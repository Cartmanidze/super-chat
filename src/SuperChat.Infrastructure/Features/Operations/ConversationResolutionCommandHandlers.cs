using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Rebus.Handlers;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Infrastructure.Diagnostics;
using SuperChat.Infrastructure.Features.Intelligence.Resolution;
using System.Diagnostics;

namespace SuperChat.Infrastructure.Features.Operations;

internal sealed class ResolveConversationItemsCommandHandler(
    ConversationResolutionService conversationResolutionService,
    TimeProvider timeProvider,
    IHostApplicationLifetime applicationLifetime,
    ILogger<ResolveConversationItemsCommandHandler> logger) : IHandleMessages<ResolveConversationItemsCommand>
{
    private const string CommandName = "resolve_conversation_items";

    public async Task Handle(ResolveConversationItemsCommand message)
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
            logger.LogInformation("Pipeline command started. Command={CommandName}.", CommandName);
            await conversationResolutionService.ResolveConversationAsync(
                message.UserId,
                message.ExternalChatId,
                timeProvider.GetUtcNow(),
                applicationLifetime.ApplicationStopping);
        }
        catch (Exception exception)
        {
            result = "failed";
            logger.LogError(exception, "Conversation resolution failed for room {RoomId}.", message.ExternalChatId);
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

internal sealed class ResolveDueMeetingsCommandHandler(
    ConversationResolutionService conversationResolutionService,
    IHostApplicationLifetime applicationLifetime,
    ILogger<ResolveDueMeetingsCommandHandler> logger) : IHandleMessages<ResolveDueMeetingsCommand>
{
    private const string CommandName = "resolve_due_meetings";

    public async Task Handle(ResolveDueMeetingsCommand message)
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
                "Pipeline command started. Command={CommandName}, ResolveAfter={ResolveAfter}.",
                CommandName,
                message.ResolveAfter);
            await conversationResolutionService.ResolveDueMeetingsAsync(
                message.UserId,
                message.ExternalChatId,
                message.ResolveAfter,
                applicationLifetime.ApplicationStopping);
        }
        catch (Exception exception)
        {
            result = "failed";
            logger.LogError(exception, "Due meeting resolution failed for room {RoomId}.", message.ExternalChatId);
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
