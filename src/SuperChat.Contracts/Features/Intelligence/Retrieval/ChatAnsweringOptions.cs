namespace SuperChat.Contracts.Configuration;

public sealed class ChatAnsweringOptions
{
    public const string SectionName = "ChatAnswering";

    public bool Enabled { get; set; } = true;

    public int MaxContextChunks { get; set; } = 5;

    public int MaxEvidenceItems { get; set; } = 3;

    public int MaxContextCharacters { get; set; } = 4_800;

    public int MaxOutputTokens { get; set; } = 500;
}
