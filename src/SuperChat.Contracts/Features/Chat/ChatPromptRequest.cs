namespace SuperChat.Contracts.ViewModels;

public sealed record ChatPromptRequest(
    string TemplateId,
    string Question)
{
    public const int MaxQuestionLength = 100;
}
