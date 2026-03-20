using FluentValidation;
using SuperChat.Contracts.Features.Chat;
using SuperChat.Domain.Features.Chat;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Api.Features.Chat;

public sealed class ChatPromptRequestValidator : AbstractValidator<ChatPromptRequest>
{
    public ChatPromptRequestValidator(IChatTemplateCatalog templateCatalog)
    {
        RuleFor(request => request.TemplateId)
            .Must(templateId => templateCatalog.TryGetTemplate(ChatPromptTemplate.Normalize(templateId), out _))
            .WithMessage("Unsupported chat template.")
            .OverridePropertyName("templateId");

        RuleFor(request => request.Question)
            .Cascade(CascadeMode.Stop)
            .Must(static question => !string.IsNullOrWhiteSpace(question))
            .WithMessage("Question is required.")
            .Must(static question => question!.Trim().Length <= ChatPromptRequest.MaxQuestionLength)
            .WithMessage($"Question must be {ChatPromptRequest.MaxQuestionLength} characters or fewer.")
            .OverridePropertyName("question");
    }
}
