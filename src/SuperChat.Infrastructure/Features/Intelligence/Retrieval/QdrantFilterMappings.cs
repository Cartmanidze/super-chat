using Qdrant.Client.Grpc;
using SuperChat.Infrastructure.Abstractions;
using static Qdrant.Client.Grpc.Conditions;

namespace SuperChat.Infrastructure.Services;

internal static class QdrantFilterMappings
{
    public static Filter ToQdrantFilter(this QdrantHybridQuery request)
    {
        var filter = new Filter();
        filter.Must.Add(MatchKeyword("user_id", request.UserId));

        if (!string.IsNullOrWhiteSpace(request.ChatId))
        {
            filter.Must.Add(MatchKeyword("chat_id", request.ChatId));
        }

        if (!string.IsNullOrWhiteSpace(request.PeerId))
        {
            filter.Must.Add(MatchKeyword("peer_id", request.PeerId));
        }

        if (!string.IsNullOrWhiteSpace(request.Kind))
        {
            filter.Must.Add(MatchKeyword("kind", request.Kind));
        }

        return filter;
    }
}
