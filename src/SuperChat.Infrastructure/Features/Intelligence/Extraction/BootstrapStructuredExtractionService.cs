using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class BootstrapStructuredExtractionService(
    HeuristicStructuredExtractionService heuristicService,
    DeepSeekStructuredExtractionService deepSeekService,
    IOptions<DeepSeekOptions> options) : IAiStructuredExtractionService
{
    public Task<IReadOnlyCollection<ExtractedItem>> ExtractAsync(ConversationWindow window, CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(options.Value.ApiKey)
            ? heuristicService.ExtractAsync(window, cancellationToken)
            : deepSeekService.ExtractAsync(window, cancellationToken);
    }
}
