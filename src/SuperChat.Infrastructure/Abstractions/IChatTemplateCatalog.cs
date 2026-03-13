using SuperChat.Contracts.ViewModels;

namespace SuperChat.Infrastructure.Abstractions;

public interface IChatTemplateCatalog
{
    IReadOnlyList<ChatTemplateDefinition> GetVisibleTemplates();

    bool TryGetTemplate(string templateId, out ChatTemplateDefinition template);
}
