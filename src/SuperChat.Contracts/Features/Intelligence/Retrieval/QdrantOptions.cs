namespace SuperChat.Contracts.Features.Intelligence.Retrieval;

public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public string BaseUrl { get; set; } = "http://localhost:6333";

    public int GrpcPort { get; set; } = 6334;

    public string ApiKey { get; set; } = string.Empty;

    public bool AutoInitialize { get; set; }

    public string MemoryCollectionName { get; set; } = "memory_bgem3_v1";

    public string DenseVectorName { get; set; } = "text-dense";

    public string SparseVectorName { get; set; } = "text-sparse";

    public int DenseVectorSize { get; set; } = 1024;
}
