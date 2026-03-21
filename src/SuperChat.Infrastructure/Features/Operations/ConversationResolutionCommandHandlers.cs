using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Infrastructure.Diagnostics;
using SuperChat.Infrastructure.Features.Intelligence.Resolution;
using System.Diagnostics;

namespace SuperChat.Infrastructure.Features.Operations;

internal sealed class ResolveConversationItemsCommandHandler(
    ConversationResolutionService conversationResolutionService,
    TimeProvider timeProvider,
    ILogger<ResolveConversationItemsCommandHandler> logger) : IHandleMessages<ResolveConversationItemsCommand>
{
    private const string CommandName = "resolve_conversation_items";

    public async Task Handle(ResolveConversationItemsCommand message)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = "succeeded";
        SuperChatMetrics.PipelineCommandsInProgress.WithLabels(CommandName).Inc();

        try
        {
            await conversationResolutionService.ResolveConversationAsync(
                message.UserId,
                message.MatrixRoomId,
                timeProvider.GetUtcNow(),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            result = "failed";
            logger.LogWarning(exception, "Conversation resolution failed for room {RoomId}.", message.MatrixRoomId);
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

internal sealed class ResolveDueMeetingsCommandHandler(
    ConversationResolutionService conversationResolutionService,
    ILogger<ResolveDueMeetingsCommandHandler> logger) : IHandleMessages<ResolveDueMeetingsCommand>
{
    private const string CommandName = "resolve_due_meetings";

    public async Task Handle(ResolveDueMeetingsCommand message)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = "succeeded";
        SuperChatMetrics.PipelineCommandsInProgress.WithLabels(CommandName).Inc();

        try
        {
            await conversationResolutionService.ResolveDueMeetingsAsync(
                message.UserId,
                message.MatrixRoomId,
                message.ResolveAfter,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            result = "failed";
            logger.LogWarning(exception, "Due meeting resolution failed for room {RoomId}.", message.MatrixRoomId);
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
