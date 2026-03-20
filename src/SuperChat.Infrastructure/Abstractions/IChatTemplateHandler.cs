using SuperChat.Contracts.Features.Chat;

namespace SuperChat.Infrastructure.Abstractions;

public interface IChatTemplateHandler
{
    string TemplateId { get; }

    Task<ChatAnswerViewModel> HandleAsync(Guid userId, string question, CancellationToken cancellationToken);
}
