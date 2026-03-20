namespace SuperChat.Contracts.Features.Intelligence.Extraction;

public sealed class TextEnrichmentOptions
{
    public const string SectionName = "TextEnrichment";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "http://localhost:7391";

    public int TimeoutSeconds { get; set; } = 20;
}
