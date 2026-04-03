using SuperChat.Contracts.Features.Chat;
using SuperChat.Domain.Features.Chat;

namespace SuperChat.Infrastructure.Features.Chat;

public sealed class ChatTemplateCatalog : IChatTemplateCatalog
{
    private static readonly IReadOnlyList<ChatTemplateDefinition> Templates =
    [
        new(
            ChatPromptTemplate.Meetings,
            "Chat.Template.Meetings.Title",
            "Chat.Template.Meetings.Description",
            "Chat.Template.Meetings.Question",
            "Chat.Answer.Meetings.Intro",
            "Chat.Answer.Meetings.Empty",
            10)
    ];

    private readonly IReadOnlyList<ChatTemplateDefinition> _visibleTemplates = Templates
        .Where(template => template.ShowInUi)
        .OrderBy(template => template.SortOrder)
        .ToList();

    private readonly IReadOnlyDictionary<string, ChatTemplateDefinition> _templatesById = Templates
        .ToDictionary(template => template.Id, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ChatTemplateDefinition> GetVisibleTemplates()
    {
        return _visibleTemplates;
    }

    public bool TryGetTemplate(string templateId, out ChatTemplateDefinition template)
    {
        return _templatesById.TryGetValue(templateId, out template!);
    }
}
