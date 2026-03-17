namespace SuperChat.Contracts.Configuration;

public sealed class PilotOptions
{
    public const string SectionName = "SuperChat";

    public string BaseUrl { get; set; } = "https://localhost:8080";

    public int MagicLinkMinutes { get; set; } = 15;

    public int ApiSessionDays { get; set; } = 30;

    public bool DevSeedSampleData { get; set; } = true;

    public bool EnableGroupIngestion { get; set; } = false;

    public int MaxIngestedGroupMembers { get; set; } = 30;

    public string TodayTimeZoneId { get; set; } = "Europe/Moscow";

    public string[] AllowedEmails { get; set; } = ["pilot@example.com", "stanislavak314@gmail.com"];

    public string[] AdminEmails { get; set; } = ["stanislavak314@gmail.com"];
}
