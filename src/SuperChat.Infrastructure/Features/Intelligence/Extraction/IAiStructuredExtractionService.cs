using SuperChat.Domain.Model;

namespace SuperChat.Infrastructure.Abstractions;

public interface IAiStructuredExtractionService
{
    Task<IReadOnlyCollection<ExtractedItem>> ExtractAsync(ConversationWindow window, CancellationToken cancellationToken);
}
