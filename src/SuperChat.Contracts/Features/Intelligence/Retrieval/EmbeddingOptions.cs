namespace SuperChat.Contracts.Configuration;

public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    public bool Enabled { get; set; } = true;

    public string Backend { get; set; } = "LocalService";

    public string BaseUrl { get; set; } = "http://localhost:7291";

    public int TimeoutSeconds { get; set; } = 60;

    public int DenseVectorSize { get; set; } = 1024;

    public string YandexBaseUrl { get; set; } = "https://ai.api.cloud.yandex.net";

    public string YandexApiKey { get; set; } = string.Empty;

    public string YandexFolderId { get; set; } = string.Empty;

    public string YandexDocModelUri { get; set; } = string.Empty;

    public string YandexQueryModelUri { get; set; } = string.Empty;

    public string YandexDocModelName { get; set; } = "text-search-doc";

    public string YandexQueryModelName { get; set; } = "text-search-query";
}
