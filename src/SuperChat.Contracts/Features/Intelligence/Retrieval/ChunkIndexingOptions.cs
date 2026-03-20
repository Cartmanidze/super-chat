namespace SuperChat.Contracts.Features.Intelligence.Retrieval;

public sealed class ChunkIndexingOptions
{
    public const string SectionName = "ChunkIndexing";

    public bool Enabled { get; set; } = true;

    public int PollSeconds { get; set; } = 10;

    public int BatchSize { get; set; } = 25;
}
