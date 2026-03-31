namespace SuperChat.Contracts.Features.Auth;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string FromSender { get; set; } = "Super Chat <hello@superchat.local>";

    public string SmtpHost { get; set; } = "localhost";

    public int SmtpPort { get; set; } = 1025;

    public string? SmtpUsername { get; set; }

    public string? SmtpPassword { get; set; }

    public bool EnableSsl { get; set; }
}
