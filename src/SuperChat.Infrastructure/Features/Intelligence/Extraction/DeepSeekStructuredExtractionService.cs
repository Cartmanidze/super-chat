using System.Text.RegularExpressions;
using SuperChat.Domain.Model;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class DeepSeekStructuredExtractionService : IAiStructuredExtractionService
{
    public Task<IReadOnlyCollection<ExtractedItem>> ExtractAsync(NormalizedMessage message, CancellationToken cancellationToken)
    {
        // This bootstrap intentionally avoids live network calls in tests and local setup.
        // The wire-up seam stays in place so replacing this with a real HTTP client is localized.
        return HeuristicStructuredExtractionService.ExtractCoreAsync(message, cancellationToken);
    }
}
