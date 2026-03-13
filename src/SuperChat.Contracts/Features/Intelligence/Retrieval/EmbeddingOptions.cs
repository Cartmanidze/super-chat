namespace SuperChat.Contracts.Configuration;

public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "http://localhost:7291";

    public int TimeoutSeconds { get; set; } = 60;

    public int DenseVectorSize { get; set; } = 1024;
}
