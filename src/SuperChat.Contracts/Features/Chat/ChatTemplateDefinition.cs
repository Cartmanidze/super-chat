namespace SuperChat.Contracts.ViewModels;

public sealed record ChatTemplateDefinition(
    string Id,
    string TitleKey,
    string DescriptionKey,
    string QuestionKey,
    string AnswerIntroKey,
    string AnswerEmptyKey,
    int SortOrder,
    bool ShowInUi = true);
