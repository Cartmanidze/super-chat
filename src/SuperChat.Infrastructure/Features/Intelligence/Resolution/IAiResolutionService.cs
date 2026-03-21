namespace SuperChat.Infrastructure.Features.Intelligence.Resolution;

internal interface IAiResolutionService
{
    Task<IReadOnlyList<AiResolutionDecisionResult>> ResolveAsync(
        IReadOnlyList<ConversationResolutionCandidate> candidates,
        CancellationToken cancellationToken);
}
