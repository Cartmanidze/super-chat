namespace SuperChat.Contracts.Configuration;

public sealed class RetrievalOptions
{
    public const string SectionName = "Retrieval";

    public bool Enabled { get; set; } = true;

    public int PrefetchLimit { get; set; } = 24;

    public int ResultLimit { get; set; } = 8;
}
