using SuperChat.Domain.Shared;

namespace SuperChat.Domain.Features.Feedback;

public sealed record FeedbackEvent(
    Guid Id,
    Guid UserId,
    string Area,
    string Value,
    string? Notes,
    DateTimeOffset CreatedAt)
{
    private readonly bool _validated = Validate(Id, UserId, Area, Value);

    private static bool Validate(Guid id, Guid userId, string area, string value)
    {
        DomainGuard.NotEmpty(id);
        DomainGuard.NotEmpty(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(area);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return true;
    }
}
