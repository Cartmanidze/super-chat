using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SuperChat.Contracts;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Intelligence.Extraction;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Intelligence.Resolution;
using SuperChat.Infrastructure.Shared.Presentation;

namespace SuperChat.Tests;

public sealed class DeepSeekResolutionServiceTests
{
    [Fact]
    public async Task ResolveAsync_MapsStructuredAiDecision()
    {
        var candidateId = Guid.NewGuid();
        var client = new StubDeepSeekJsonClient(new AiResolutionResponse(
        [
            new AiResolutionDecision(
                candidateId.ToString("D"),
                true,
                WorkItemResolutionState.Completed,
                0.91,
                "2026-03-16T09:07:00Z",
                "Есть явное подтверждение отправки.",
                ["$evt-done"])
        ]));

        var service = new DeepSeekResolutionService(
            client,
            Options.Create(new DeepSeekOptions
            {
                Model = "deepseek-reasoner"
            }),
            Options.Create(new ResolutionOptions
            {
                UseLlm = true,
                MinConfidence = 0.7,
                MaxOutputTokens = 300
            }),
            new PilotOptions
            {
                TodayTimeZoneId = "Europe/Moscow"
            },
            NullLogger<DeepSeekResolutionService>.Instance);

        var result = await service.ResolveAsync(
        [
            new ConversationResolutionCandidate(
                candidateId,
                ResolutionCandidateType.WorkItem,
                ExtractedItemKind.Meeting,
                "Отправить дек",
                "Нужно отправить финальный дек.",
                "!sales:matrix.localhost",
                null,
                new DateTimeOffset(2026, 03, 16, 09, 00, 00, TimeSpan.Zero),
                null,
                [
                    new ResolutionMessageSnippet("$evt-done", "You", "готово, отправил финальный дек", new DateTimeOffset(2026, 03, 16, 09, 07, 00, TimeSpan.Zero))
                ])
        ],
            CancellationToken.None);

        var decision = Assert.Single(result);
        Assert.Equal(candidateId, decision.CandidateId);
        Assert.Equal(WorkItemResolutionState.Completed, decision.ResolutionKind);
        Assert.Equal(WorkItemResolutionState.AutoAiMeetingCompletion, decision.ResolutionSource);
        Assert.Equal(new DateTimeOffset(2026, 03, 16, 09, 07, 00, TimeSpan.Zero), decision.ResolvedAt);
        Assert.Equal("deepseek-reasoner", decision.Model);
        Assert.Equal(["$evt-done"], decision.EvidenceMessageIds);
    }

    [Fact]
    public async Task ResolveAsync_IgnoresLowConfidenceDecisions()
    {
        var client = new StubDeepSeekJsonClient(new AiResolutionResponse(
        [
            new AiResolutionDecision(
                Guid.NewGuid().ToString("D"),
                true,
                WorkItemResolutionState.Completed,
                0.4,
                null,
                "Слабый сигнал.",
                [])
        ]));

        var service = new DeepSeekResolutionService(
            client,
            Options.Create(new DeepSeekOptions()),
            Options.Create(new ResolutionOptions
            {
                UseLlm = true,
                MinConfidence = 0.7
            }),
            new PilotOptions(),
            NullLogger<DeepSeekResolutionService>.Instance);

        var result = await service.ResolveAsync(
        [
            new ConversationResolutionCandidate(
                Guid.NewGuid(),
                ResolutionCandidateType.WorkItem,
                ExtractedItemKind.Meeting,
                "Нужен следующий шаг",
                "Нужно вернуться с апдейтом.",
                "!sales:matrix.localhost",
                null,
                DateTimeOffset.UtcNow,
                null,
                [])
        ],
            CancellationToken.None);

        Assert.Empty(result);
    }

    private sealed class StubDeepSeekJsonClient(AiResolutionResponse response) : IDeepSeekJsonClient
    {
        public bool IsConfigured => true;

        public Task<TResponse?> CompleteJsonAsync<TResponse>(
            IReadOnlyList<DeepSeekMessage> messages,
            int maxTokens,
            CancellationToken cancellationToken)
            where TResponse : class
        {
            return Task.FromResult(response as TResponse);
        }
    }
}
