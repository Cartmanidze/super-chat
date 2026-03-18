namespace SuperChat.Contracts.Configuration;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public string Provider { get; set; } = "Postgres";

    public string ConnectionString { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = "superchat_app";

}
