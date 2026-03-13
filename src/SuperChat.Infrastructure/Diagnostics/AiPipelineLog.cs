using Microsoft.Extensions.Logging;

namespace SuperChat.Infrastructure.Diagnostics;

internal static partial class AiPipelineLog
{
    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Information,
        Message = "Chat pipeline started. TemplateId={TemplateId}, QuestionLength={QuestionLength}.")]
    internal static partial void ChatPipelineStarted(
        ILogger logger,
        string templateId,
        int questionLength);

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "Chat pipeline completed. TemplateId={TemplateId}, ElapsedMs={ElapsedMs}, ItemCount={ItemCount}, AssistantTextLength={AssistantTextLength}.")]
    internal static partial void ChatPipelineCompleted(
        ILogger logger,
        string templateId,
        long elapsedMs,
        int itemCount,
        int assistantTextLength);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Warning,
        Message = "Chat pipeline failed. TemplateId={TemplateId}, ElapsedMs={ElapsedMs}.")]
    internal static partial void ChatPipelineFailed(
        ILogger logger,
        string templateId,
        long elapsedMs,
        Exception exception);

    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Information,
        Message = "Template answer enhancement started. TemplateId={TemplateId}, ContextItemCount={ContextItemCount}.")]
    internal static partial void TemplateAnswerEnhancementStarted(
        ILogger logger,
        string templateId,
        int contextItemCount);

    [LoggerMessage(
        EventId = 2104,
        Level = LogLevel.Information,
        Message = "Template answer enhancement completed. TemplateId={TemplateId}, ContextItemCount={ContextItemCount}, GeneratedItemCount={GeneratedItemCount}, AssistantTextLength={AssistantTextLength}, ElapsedMs={ElapsedMs}.")]
    internal static partial void TemplateAnswerEnhancementCompleted(
        ILogger logger,
        string templateId,
        int contextItemCount,
        int generatedItemCount,
        int assistantTextLength,
        long elapsedMs);

    [LoggerMessage(
        EventId = 2105,
        Level = LogLevel.Information,
        Message = "Custom question routed to template. RoutedTemplateId={RoutedTemplateId}.")]
    internal static partial void CustomQuestionRoutedToTemplate(
        ILogger logger,
        string routedTemplateId);

    [LoggerMessage(
        EventId = 2106,
        Level = LogLevel.Information,
        Message = "Custom retrieval completed. ContextCount={ContextCount}.")]
    internal static partial void CustomRetrievalCompleted(
        ILogger logger,
        int contextCount);

    [LoggerMessage(
        EventId = 2107,
        Level = LogLevel.Information,
        Message = "Custom search fallback completed. ResultCount={ResultCount}.")]
    internal static partial void CustomSearchFallbackCompleted(
        ILogger logger,
        int resultCount);

    [LoggerMessage(
        EventId = 2200,
        Level = LogLevel.Information,
        Message = "Chat answer generation started. QuestionLength={QuestionLength}, ContextItemCount={ContextItemCount}, ContextCharacters={ContextCharacters}, MaxOutputTokens={MaxOutputTokens}.")]
    internal static partial void ChatAnswerGenerationStarted(
        ILogger logger,
        int questionLength,
        int contextItemCount,
        int contextCharacters,
        int maxOutputTokens);

    [LoggerMessage(
        EventId = 2201,
        Level = LogLevel.Information,
        Message = "Chat answer generation completed. ContextItemCount={ContextItemCount}, GeneratedItemCount={GeneratedItemCount}, AssistantTextLength={AssistantTextLength}, ElapsedMs={ElapsedMs}.")]
    internal static partial void ChatAnswerGenerationCompleted(
        ILogger logger,
        int contextItemCount,
        int generatedItemCount,
        int assistantTextLength,
        long elapsedMs);

    [LoggerMessage(
        EventId = 2202,
        Level = LogLevel.Warning,
        Message = "Chat answer generation failed. ContextItemCount={ContextItemCount}, ElapsedMs={ElapsedMs}.")]
    internal static partial void ChatAnswerGenerationFailed(
        ILogger logger,
        int contextItemCount,
        long elapsedMs,
        Exception exception);

    [LoggerMessage(
        EventId = 2300,
        Level = LogLevel.Information,
        Message = "DeepSeek request started. Model={Model}, MessageCount={MessageCount}, PromptCharacters={PromptCharacters}, MaxTokens={MaxTokens}.")]
    internal static partial void DeepSeekRequestStarted(
        ILogger logger,
        string model,
        int messageCount,
        int promptCharacters,
        int maxTokens);

    [LoggerMessage(
        EventId = 2301,
        Level = LogLevel.Information,
        Message = "DeepSeek request completed. Model={Model}, MessageCount={MessageCount}, ChoiceCount={ChoiceCount}, ResponseCharacters={ResponseCharacters}, ElapsedMs={ElapsedMs}.")]
    internal static partial void DeepSeekRequestCompleted(
        ILogger logger,
        string model,
        int messageCount,
        int choiceCount,
        int responseCharacters,
        long elapsedMs);

    [LoggerMessage(
        EventId = 2302,
        Level = LogLevel.Warning,
        Message = "DeepSeek request failed. Model={Model}, MessageCount={MessageCount}, PromptCharacters={PromptCharacters}, MaxTokens={MaxTokens}, ElapsedMs={ElapsedMs}, StatusCode={StatusCode}.")]
    internal static partial void DeepSeekRequestFailed(
        ILogger logger,
        string model,
        int messageCount,
        int promptCharacters,
        int maxTokens,
        long elapsedMs,
        string statusCode,
        Exception exception);

    [LoggerMessage(
        EventId = 2400,
        Level = LogLevel.Information,
        Message = "Embedding request started. Backend={Backend}, Purpose={Purpose}, TextLength={TextLength}.")]
    internal static partial void EmbeddingRequestStarted(
        ILogger logger,
        string backend,
        string purpose,
        int textLength);

    [LoggerMessage(
        EventId = 2401,
        Level = LogLevel.Information,
        Message = "Embedding request completed. Backend={Backend}, Purpose={Purpose}, TextLength={TextLength}, DenseVectorSize={DenseVectorSize}, SparseValueCount={SparseValueCount}, Model={Model}, ElapsedMs={ElapsedMs}.")]
    internal static partial void EmbeddingRequestCompleted(
        ILogger logger,
        string backend,
        string purpose,
        int textLength,
        int denseVectorSize,
        int sparseValueCount,
        string model,
        long elapsedMs);

    [LoggerMessage(
        EventId = 2402,
        Level = LogLevel.Warning,
        Message = "Embedding request failed. Backend={Backend}, Purpose={Purpose}, TextLength={TextLength}, ElapsedMs={ElapsedMs}, StatusCode={StatusCode}.")]
    internal static partial void EmbeddingRequestFailed(
        ILogger logger,
        string backend,
        string purpose,
        int textLength,
        long elapsedMs,
        string statusCode,
        Exception exception);

    [LoggerMessage(
        EventId = 2500,
        Level = LogLevel.Information,
        Message = "Retrieval started. QueryKind={QueryKind}, QueryLength={QueryLength}, ResultLimit={ResultLimit}, PrefetchLimit={PrefetchLimit}.")]
    internal static partial void RetrievalStarted(
        ILogger logger,
        string queryKind,
        int queryLength,
        int resultLimit,
        int prefetchLimit);

    [LoggerMessage(
        EventId = 2501,
        Level = LogLevel.Information,
        Message = "Retrieval completed. QueryKind={QueryKind}, QueryLength={QueryLength}, CandidateCount={CandidateCount}, SelectedChunkCount={SelectedChunkCount}, ReturnedChunkCount={ReturnedChunkCount}, ElapsedMs={ElapsedMs}.")]
    internal static partial void RetrievalCompleted(
        ILogger logger,
        string queryKind,
        int queryLength,
        int candidateCount,
        int selectedChunkCount,
        int returnedChunkCount,
        long elapsedMs);

    [LoggerMessage(
        EventId = 2502,
        Level = LogLevel.Warning,
        Message = "Retrieval failed. QueryKind={QueryKind}, QueryLength={QueryLength}, ElapsedMs={ElapsedMs}.")]
    internal static partial void RetrievalFailed(
        ILogger logger,
        string queryKind,
        int queryLength,
        long elapsedMs,
        Exception exception);

    [LoggerMessage(
        EventId = 2600,
        Level = LogLevel.Information,
        Message = "Qdrant upsert started. CollectionName={CollectionName}, PointCount={PointCount}.")]
    internal static partial void QdrantUpsertStarted(
        ILogger logger,
        string collectionName,
        int pointCount);

    [LoggerMessage(
        EventId = 2601,
        Level = LogLevel.Information,
        Message = "Qdrant upsert completed. CollectionName={CollectionName}, PointCount={PointCount}, ElapsedMs={ElapsedMs}.")]
    internal static partial void QdrantUpsertCompleted(
        ILogger logger,
        string collectionName,
        int pointCount,
        long elapsedMs);

    [LoggerMessage(
        EventId = 2602,
        Level = LogLevel.Warning,
        Message = "Qdrant upsert failed. CollectionName={CollectionName}, PointCount={PointCount}, ElapsedMs={ElapsedMs}.")]
    internal static partial void QdrantUpsertFailed(
        ILogger logger,
        string collectionName,
        int pointCount,
        long elapsedMs,
        Exception exception);

    [LoggerMessage(
        EventId = 2603,
        Level = LogLevel.Information,
        Message = "Qdrant query started. CollectionName={CollectionName}, ResultLimit={ResultLimit}, PrefetchLimit={PrefetchLimit}.")]
    internal static partial void QdrantQueryStarted(
        ILogger logger,
        string collectionName,
        int resultLimit,
        int prefetchLimit);

    [LoggerMessage(
        EventId = 2604,
        Level = LogLevel.Information,
        Message = "Qdrant query completed. CollectionName={CollectionName}, ResultCount={ResultCount}, ElapsedMs={ElapsedMs}.")]
    internal static partial void QdrantQueryCompleted(
        ILogger logger,
        string collectionName,
        int resultCount,
        long elapsedMs);

    [LoggerMessage(
        EventId = 2605,
        Level = LogLevel.Warning,
        Message = "Qdrant query failed. CollectionName={CollectionName}, ResultLimit={ResultLimit}, PrefetchLimit={PrefetchLimit}, ElapsedMs={ElapsedMs}.")]
    internal static partial void QdrantQueryFailed(
        ILogger logger,
        string collectionName,
        int resultLimit,
        int prefetchLimit,
        long elapsedMs,
        Exception exception);

    [LoggerMessage(
        EventId = 2700,
        Level = LogLevel.Information,
        Message = "Chunk indexing run started. SelectedChunkCount={SelectedChunkCount}, BatchSize={BatchSize}.")]
    internal static partial void ChunkIndexingRunStarted(
        ILogger logger,
        int selectedChunkCount,
        int batchSize);

    [LoggerMessage(
        EventId = 2701,
        Level = LogLevel.Information,
        Message = "Chunk indexing run completed. SelectedChunkCount={SelectedChunkCount}, IndexedChunkCount={IndexedChunkCount}, ElapsedMs={ElapsedMs}.")]
    internal static partial void ChunkIndexingRunCompleted(
        ILogger logger,
        int selectedChunkCount,
        int indexedChunkCount,
        long elapsedMs);

    [LoggerMessage(
        EventId = 2702,
        Level = LogLevel.Warning,
        Message = "Chunk indexing run failed. SelectedChunkCount={SelectedChunkCount}, ElapsedMs={ElapsedMs}.")]
    internal static partial void ChunkIndexingRunFailed(
        ILogger logger,
        int selectedChunkCount,
        long elapsedMs,
        Exception exception);
}
