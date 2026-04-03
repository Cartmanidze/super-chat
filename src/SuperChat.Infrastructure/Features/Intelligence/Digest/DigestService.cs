using Microsoft.Extensions.Logging;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Intelligence.Meetings;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Intelligence;

namespace SuperChat.Infrastructure.Features.Intelligence.Digest;

public sealed class DigestService(
    IMeetingService meetingService,
    IRoomDisplayNameService roomDisplayNameService,
    TimeProvider timeProvider,
    PilotOptions pilotOptions,
    ILogger<DigestService> logger) : IDigestService
{
    public async Task<IReadOnlyList<WorkItemCardViewModel>> GetMeetingsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), WorkItemTimeZoneResolver.Resolve(logger, pilotOptions.TodayTimeZoneId));
        var meetings = await meetingService.GetUpcomingAsync(userId, now, 20, cancellationToken);
        var cards = DigestComposer.BuildMeetings(meetings, now)
            .Select(item => item.ToWorkItemCardViewModel(now))
            .ToList();

        return await ResolveRoomNamesAsync(userId, cards, cancellationToken);
    }

    private async Task<IReadOnlyList<WorkItemCardViewModel>> ResolveRoomNamesAsync(
        Guid userId,
        IReadOnlyList<WorkItemCardViewModel> cards,
        CancellationToken cancellationToken)
    {
        var roomNames = await roomDisplayNameService.ResolveManyAsync(userId, cards.Select(item => item.SourceRoom), cancellationToken);

        return cards
            .Select(card => card.WithResolvedSourceRoom(roomNames))
            .ToList();
    }
}
