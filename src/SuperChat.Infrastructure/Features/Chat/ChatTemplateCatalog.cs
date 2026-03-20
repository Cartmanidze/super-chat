using SuperChat.Contracts.Features.Chat;
using SuperChat.Domain.Features.Chat;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Features.Chat;

public sealed class ChatTemplateCatalog : IChatTemplateCatalog
{
    private static readonly IReadOnlyList<ChatTemplateDefinition> Templates =
    [
        new(
            ChatPromptTemplate.Today,
            "Chat.Template.Today.Title",
            "Chat.Template.Today.Description",
            "Chat.Template.Today.Question",
            "Chat.Answer.Today.Intro",
            "Chat.Answer.Today.Empty",
            10),
        new(
            ChatPromptTemplate.Waiting,
            "Chat.Template.Waiting.Title",
            "Chat.Template.Waiting.Description",
            "Chat.Template.Waiting.Question",
            "Chat.Answer.Waiting.Intro",
            "Chat.Answer.Waiting.Empty",
            20),
        new(
            ChatPromptTemplate.Meetings,
            "Chat.Template.Meetings.Title",
            "Chat.Template.Meetings.Description",
            "Chat.Template.Meetings.Question",
            "Chat.Answer.Meetings.Intro",
            "Chat.Answer.Meetings.Empty",
            30),
        new(
            ChatPromptTemplate.Recent,
            "Chat.Template.Recent.Title",
            "Chat.Template.Recent.Description",
            "Chat.Template.Recent.Question",
            "Chat.Answer.Recent.Intro",
            "Chat.Answer.Recent.Empty",
            40),
        new(
            ChatPromptTemplate.Custom,
            "Chat.Template.Custom.Title",
            "Chat.Template.Custom.Description",
            "Chat.Template.Custom.Question",
            "Chat.Answer.Custom.Intro",
            "Chat.Answer.Custom.Empty",
            90,
            ShowInUi: false)
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
