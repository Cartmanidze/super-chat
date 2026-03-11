namespace SuperChat.Contracts.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string FromSender { get; set; } = "Super Chat <hello@superchat.local>";

    public string SmtpHost { get; set; } = "localhost";

    public int SmtpPort { get; set; } = 1025;
}
