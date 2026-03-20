using Microsoft.Extensions.Options;
using SuperChat.Contracts.Features.Intelligence.Extraction;
using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Infrastructure.Features.Intelligence.Extraction;

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
