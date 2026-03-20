using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Features.Operations;

internal static class PipelineWorkerRegistry
{
    public const string ExtractionWorkerKey = "structured-extraction";
    public const string ExtractionWorkerDisplayName = "Structured Extraction";
    public const string ChunkBuilderWorkerKey = "chunk-builder";
    public const string ChunkBuilderWorkerDisplayName = "Chunk Builder";
    public const string ChunkIndexingWorkerKey = "chunk-indexing";
    public const string ChunkIndexingWorkerDisplayName = "Chunk Indexing";
    public const string MeetingProjectionWorkerKey = "meeting-projection";
    public const string MeetingProjectionWorkerDisplayName = "Meeting Projection";

    public static void RegisterAll(IWorkerRuntimeMonitor workerRuntimeMonitor)
    {
        workerRuntimeMonitor.RegisterWorker(ExtractionWorkerKey, ExtractionWorkerDisplayName);
        workerRuntimeMonitor.RegisterWorker(ChunkBuilderWorkerKey, ChunkBuilderWorkerDisplayName);
        workerRuntimeMonitor.RegisterWorker(ChunkIndexingWorkerKey, ChunkIndexingWorkerDisplayName);
        workerRuntimeMonitor.RegisterWorker(MeetingProjectionWorkerKey, MeetingProjectionWorkerDisplayName);
    }
}
