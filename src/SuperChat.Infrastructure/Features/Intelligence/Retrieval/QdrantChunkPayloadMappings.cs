using Qdrant.Client.Grpc;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

internal static class QdrantChunkPayloadMappings
{
    public static void ApplyTo(this QdrantChunkPayload payload, IDictionary<string, Value> target)
    {
        target["user_id"] = payload.UserId;
        target["source"] = payload.Source;
        target["provider"] = payload.Provider;
        target["transport"] = payload.Transport;
        target["chat_id"] = payload.ChatId;
        target["kind"] = payload.Kind;
        target["ts_from"] = payload.TsFrom;
        target["ts_to"] = payload.TsTo;
        target["chunk_id"] = payload.ChunkId;
        target["embedding_version"] = payload.EmbeddingVersion;
        target["chunk_version"] = payload.ChunkVersion;
        target["message_count"] = payload.MessageCount;
        target["content_hash"] = payload.ContentHash;

        if (!string.IsNullOrWhiteSpace(payload.PeerId))
        {
            target["peer_id"] = payload.PeerId;
        }

        if (!string.IsNullOrWhiteSpace(payload.ThreadId))
        {
            target["thread_id"] = payload.ThreadId;
        }
    }

    public static string? GetOptionalPayloadString(this IDictionary<string, Value> payload, string key)
    {
        return payload.TryGetValue(key, out var value) &&
               value.KindCase == Value.KindOneofCase.StringValue &&
               !string.IsNullOrWhiteSpace(value.StringValue)
            ? value.StringValue
            : null;
    }
}
