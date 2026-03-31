namespace SuperChat.Domain.Features.Auth;

public sealed record Email
{
    public string Value { get; }

    public Email(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim().ToLowerInvariant();
    }

    public static implicit operator string(Email email) => email.Value;
    public static explicit operator Email(string value) => new(value);

    public override string ToString() => Value;
}
