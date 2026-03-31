using System.Runtime.CompilerServices;

namespace SuperChat.Domain.Shared;

public static class DomainGuard
{
    public static Guid NotEmpty(Guid value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        => value == Guid.Empty
            ? throw new ArgumentException("Value must not be empty.", paramName)
            : value;
}
