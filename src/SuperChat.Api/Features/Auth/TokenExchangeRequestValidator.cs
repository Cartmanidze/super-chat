using FluentValidation;

namespace SuperChat.Api.Features.Auth;

public sealed class TokenExchangeRequestValidator : AbstractValidator<TokenExchangeRequest>
{
    public TokenExchangeRequestValidator()
    {
        RuleFor(request => request.Token)
            .Cascade(CascadeMode.Stop)
            .Must(static token => !string.IsNullOrWhiteSpace(token))
            .WithMessage("Token is required.")
            .OverridePropertyName("token");
    }
}
