using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Contracts.Features.Intelligence.Extraction;

public interface IAiStructuredExtractionService
{
    Task<IReadOnlyCollection<ExtractedItem>> ExtractAsync(ConversationWindow window, CancellationToken cancellationToken);
}
