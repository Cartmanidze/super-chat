using System.Text.Json.Serialization;

namespace SuperChat.Contracts;

public sealed record DeepSeekChatCompletionRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<DeepSeekMessage> Messages,
    [property: JsonPropertyName("response_format")] DeepSeekResponseFormat ResponseFormat);

public sealed record DeepSeekMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

public sealed record DeepSeekResponseFormat(
    [property: JsonPropertyName("type")] string Type);

public sealed record DeepSeekChatCompletionResponse(
    [property: JsonPropertyName("choices")] IReadOnlyList<DeepSeekChoice>? Choices);

public sealed record DeepSeekChoice(
    [property: JsonPropertyName("message")] DeepSeekAssistantMessage? Message);

public sealed record DeepSeekAssistantMessage(
    [property: JsonPropertyName("content")] string? Content);

public sealed record DeepSeekStructuredResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<DeepSeekStructuredItem>? Items);

public sealed record DeepSeekStructuredItem(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("person")] string? Person,
    [property: JsonPropertyName("deadline")] string? Deadline,
    [property: JsonPropertyName("priority")] string? Priority,
    [property: JsonPropertyName("confidence")] double? Confidence,
    [property: JsonPropertyName("summary")] string Summary);
