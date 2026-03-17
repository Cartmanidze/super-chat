using System.Globalization;
using Qdrant.Client.Grpc;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

internal static class QdrantPointMappings
{
    public static PointStruct ToPointStruct(this QdrantMemoryPoint point, QdrantOptions configuredOptions)
    {
        var mappedPoint = new PointStruct
        {
            Id = point.PointId.ToQdrantPointId(),
            Vectors = new Dictionary<string, Vector>(StringComparer.Ordinal)
            {
                [configuredOptions.DenseVectorName] = point.DenseVector.ToArray(),
                [configuredOptions.SparseVectorName] = (
                    point.SparseVector.Values.ToArray(),
                    point.SparseVector.Indices.Select(index => checked((uint)index)).ToArray())
            }
        };

        point.Payload.ApplyTo(mappedPoint.Payload);

        return mappedPoint;
    }

    public static QdrantQueryPoint ToQdrantQueryPoint(this ScoredPoint point)
    {
        return new QdrantQueryPoint(
            point.Id.ToPointIdString(),
            point.Payload.GetOptionalPayloadString("chunk_id"),
            point.Score);
    }

    public static PointId ToQdrantPointId(this string pointId)
    {
        if (Guid.TryParse(pointId, out var guid))
        {
            return guid;
        }

        if (ulong.TryParse(pointId, NumberStyles.None, CultureInfo.InvariantCulture, out var numericId))
        {
            return numericId;
        }

        throw new InvalidOperationException($"Unsupported Qdrant point id format: {pointId}");
    }

    public static string ToPointIdString(this PointId pointId)
    {
        return pointId.PointIdOptionsCase switch
        {
            PointId.PointIdOptionsOneofCase.Uuid => pointId.Uuid,
            PointId.PointIdOptionsOneofCase.Num => pointId.Num.ToString(CultureInfo.InvariantCulture),
            _ => string.Empty
        };
    }
}
