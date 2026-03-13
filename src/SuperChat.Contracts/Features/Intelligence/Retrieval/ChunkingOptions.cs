namespace SuperChat.Contracts.Configuration;

public sealed class ChunkingOptions
{
    public const string SectionName = "Chunking";

    public bool Enabled { get; set; } = true;

    public int PollSeconds { get; set; } = 5;

    public int MaxGapMinutes { get; set; } = 15;

    public int MaxMessagesPerChunk { get; set; } = 8;

    public int MaxChunkCharacters { get; set; } = 1600;
}
