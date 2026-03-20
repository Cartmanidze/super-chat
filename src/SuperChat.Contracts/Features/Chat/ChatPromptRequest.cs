namespace SuperChat.Contracts.Features.Chat;

public sealed record ChatPromptRequest(
    string TemplateId,
    string Question)
{
    public const int MaxQuestionLength = 100;
}
