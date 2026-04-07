using NSubstitute;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;

namespace SuperChat.Tests;

public sealed class MeetingServiceReadRepositoryBoundaryTests
{
    [Fact]
    public void MeetingService_UsesMeetingRepositoryBoundaryForUpcomingReads()
    {
        var constructor = typeof(MeetingService).GetConstructor(
        [
            typeof(MeetingUpsertService),
            typeof(IMeetingRepository),
            typeof(TimeProvider)
        ]);

        Assert.NotNull(constructor);
        AssertNoDependencyByName<MeetingService>("MeetingUpcomingQueryService");
    }

    [Fact]
    public async Task MeetingService_GetUpcomingAsync_UsesMeetingRepository()
    {
        var repository = Substitute.For<IMeetingRepository>();
        var userId = Guid.NewGuid();
        var fromInclusive = new DateTimeOffset(2026, 03, 13, 15, 00, 00, TimeSpan.Zero);
        var expected = new[] { CreateMeetingRecord(userId, "$meeting-3") };

        repository.GetUpcomingAsync(userId, fromInclusive, 10, CancellationToken.None).Returns(expected);

        var service = CreateMeetingService(repository);

        var result = await service.GetUpcomingAsync(userId, fromInclusive, 10, CancellationToken.None);

        Assert.Equal(expected, result);
        await repository.Received(1).GetUpcomingAsync(userId, fromInclusive, 10, CancellationToken.None);
    }

    private static MeetingService CreateMeetingService(IMeetingRepository repository)
    {
        var constructor = typeof(MeetingService).GetConstructor(
        [
            typeof(MeetingUpsertService),
            typeof(IMeetingRepository),
            typeof(TimeProvider)
        ]);

        Assert.NotNull(constructor);

        return (MeetingService)constructor.Invoke(
        [
            new MeetingUpsertService(Substitute.For<Microsoft.EntityFrameworkCore.IDbContextFactory<SuperChat.Infrastructure.Shared.Persistence.SuperChatDbContext>>()),
            repository,
            TimeProvider.System
        ]);
    }

    private static void AssertNoDependencyByName<TService>(string dependencyTypeName)
    {
        Assert.DoesNotContain(
            typeof(TService).GetConstructors().SelectMany(constructor => constructor.GetParameters()),
            parameter => string.Equals(parameter.ParameterType.Name, dependencyTypeName, StringComparison.Ordinal));
    }

    private static MeetingRecord CreateMeetingRecord(Guid userId, string sourceEventId)
    {
        return new MeetingRecord(
            Guid.NewGuid(),
            userId,
            "Sync with product",
            "Walk through the latest status update.",
            "!team:matrix.localhost",
            sourceEventId,
            null,
            new DateTimeOffset(2026, 03, 13, 15, 00, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 13, 17, 00, 00, TimeSpan.Zero),
            new Confidence(0.91));
    }
}
