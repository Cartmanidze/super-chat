using FluentValidation;

namespace SuperChat.Api.Features.Auth;

public sealed class SendCodeRequestValidator : AbstractValidator<SendCodeRequest>
{
    public SendCodeRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .OverridePropertyName("email");
    }
}
