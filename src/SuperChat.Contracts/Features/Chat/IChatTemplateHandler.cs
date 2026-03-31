namespace SuperChat.Contracts.Features.Chat;

public interface IChatTemplateHandler
{
    string TemplateId { get; }

    Task<ChatAnswerViewModel> HandleAsync(Guid userId, string question, CancellationToken cancellationToken);
}
