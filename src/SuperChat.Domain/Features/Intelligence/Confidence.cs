namespace SuperChat.Domain.Features.Intelligence;

public sealed record Confidence
{
    public double Value { get; }

    public Confidence(double value)
    {
        if (value < 0.0 || value > 1.0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Confidence must be between 0.0 and 1.0.");
        Value = value;
    }

    public static implicit operator double(Confidence confidence) => confidence.Value;
    public static explicit operator Confidence(double value) => new(value);

    public override string ToString() => Value.ToString("F2");
}
