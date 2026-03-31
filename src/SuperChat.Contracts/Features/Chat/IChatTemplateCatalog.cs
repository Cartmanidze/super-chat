namespace SuperChat.Contracts.Features.Chat;

public interface IChatTemplateCatalog
{
    IReadOnlyList<ChatTemplateDefinition> GetVisibleTemplates();

    bool TryGetTemplate(string templateId, out ChatTemplateDefinition template);
}
