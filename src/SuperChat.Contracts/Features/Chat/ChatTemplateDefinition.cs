namespace SuperChat.Contracts.Features.Chat;

public sealed record ChatTemplateDefinition(
    string Id,
    string TitleKey,
    string DescriptionKey,
    string QuestionKey,
    string AnswerIntroKey,
    string AnswerEmptyKey,
    int SortOrder,
    bool ShowInUi = true);
