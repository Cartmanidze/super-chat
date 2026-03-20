using System.Text.Json;

namespace SuperChat.Infrastructure.Features.Integrations.Matrix;

internal static class MatrixDirectRoomMappings
{
    public static HashSet<string> GetDirectRoomIds(this MatrixAccountDataPayload? accountData)
    {
        var roomIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var accountDataEvent in accountData?.Events ?? [])
        {
            if (!string.Equals(accountDataEvent.Type, "m.direct", StringComparison.Ordinal) ||
                accountDataEvent.Content is not { ValueKind: JsonValueKind.Object } directMappings)
            {
                continue;
            }

            foreach (var mapping in directMappings.EnumerateObject())
            {
                if (mapping.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var roomId in mapping.Value.EnumerateArray())
                {
                    var value = roomId.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        roomIds.Add(value);
                    }
                }
            }
        }

        return roomIds;
    }
}
