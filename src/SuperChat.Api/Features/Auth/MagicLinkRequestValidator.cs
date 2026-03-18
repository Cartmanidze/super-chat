using FluentValidation;

namespace SuperChat.Api.Features.Auth;

public sealed class MagicLinkRequestValidator : AbstractValidator<MagicLinkRequest>
{
    public MagicLinkRequestValidator()
    {
        RuleFor(request => request.Email)
            .Cascade(CascadeMode.Stop)
            .Must(static email => !string.IsNullOrWhiteSpace(email))
            .WithMessage("Email is required.")
            .OverridePropertyName("email");
    }
}
