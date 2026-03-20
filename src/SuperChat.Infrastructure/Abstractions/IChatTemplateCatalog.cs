using SuperChat.Contracts.Features.Chat;

namespace SuperChat.Infrastructure.Abstractions;

public interface IChatTemplateCatalog
{
    IReadOnlyList<ChatTemplateDefinition> GetVisibleTemplates();

    bool TryGetTemplate(string templateId, out ChatTemplateDefinition template);
}
