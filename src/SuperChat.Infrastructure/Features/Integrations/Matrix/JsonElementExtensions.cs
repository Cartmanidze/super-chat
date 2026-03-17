using System.Text.Json;

namespace SuperChat.Infrastructure.Services;

internal static class JsonElementExtensions
{
    public static string? GetOptionalStringProperty(this JsonElement? content, string propertyName)
    {
        if (content is not { ValueKind: JsonValueKind.Object } objectContent ||
            !objectContent.TryGetProperty(propertyName, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.GetString();
    }
}
