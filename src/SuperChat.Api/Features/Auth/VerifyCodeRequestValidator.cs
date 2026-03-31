using FluentValidation;

namespace SuperChat.Api.Features.Auth;

public sealed class VerifyCodeRequestValidator : AbstractValidator<VerifyCodeRequest>
{
    public VerifyCodeRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .OverridePropertyName("email");

        RuleFor(request => request.Code)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Verification code is required.")
            .Must(static code => code.Trim().Length == 6 && code.Trim().All(char.IsDigit))
            .WithMessage("Verification code must be exactly 6 digits.")
            .OverridePropertyName("code");
    }
}
