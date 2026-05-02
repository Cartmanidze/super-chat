namespace SuperChat.Infrastructure.Abstractions;

/// <summary>
/// Внутренний event-объект, который ChatMessageStore передаёт в IPipelineCommandScheduler
/// после успешной записи свежего сообщения. Объединяет 6 полей, которые иначе шли бы
/// отдельными аргументами и затрудняли чтение сигнатур.
/// </summary>
public sealed record ChatMessageStoredEvent(
    Guid UserId,
    string Source,
    string ExternalChatId,
    Guid ChatMessageId,
    string ExternalMessageId,
    DateTimeOffset SentAt);
