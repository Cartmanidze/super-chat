using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SuperChat.Contracts.Features.Intelligence.Meetings;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Features.Intelligence.WorkItems;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Tests;

public sealed class WorkItemServiceReadRepositoryBoundaryTests
{
    [Fact]
    public void WorkItemService_UsesWorkItemRepositoryBoundaryForReads()
    {
        var constructor = typeof(WorkItemService).GetConstructor(
        [
            typeof(WorkItemWriter),
            typeof(IWorkItemRepository),
            typeof(TimeProvider)
        ]);

        Assert.NotNull(constructor);
        AssertNoDependencyByName<WorkItemService>("WorkItemQueryService");
    }

    [Fact]
    public async Task WorkItemService_GetForUserAsync_UsesRepositoryWithoutUnresolvedFilter()
    {
        var repository = Substitute.For<IWorkItemRepository>();
        var userId = Guid.NewGuid();
        var expected = new[] { CreateWorkItemRecord(userId, "$work-item-2") };

        repository.GetByUserAsync(userId, unresolvedOnly: false, CancellationToken.None).Returns(expected);

        var service = CreateWorkItemService(repository);

        var result = await service.GetForUserAsync(userId, CancellationToken.None);

        Assert.Equal(expected, result);
        await repository.Received(1).GetByUserAsync(userId, unresolvedOnly: false, CancellationToken.None);
    }

    [Fact]
    public async Task WorkItemService_GetActiveForUserAsync_UsesRepositoryWithUnresolvedFilter()
    {
        var repository = Substitute.For<IWorkItemRepository>();
        var userId = Guid.NewGuid();
        var expected = new[] { CreateWorkItemRecord(userId, "$work-item-3") };

        repository.GetByUserAsync(userId, unresolvedOnly: true, CancellationToken.None).Returns(expected);

        var service = CreateWorkItemService(repository);

        var result = await service.GetActiveForUserAsync(userId, CancellationToken.None);

        Assert.Equal(expected, result);
        await repository.Received(1).GetByUserAsync(userId, unresolvedOnly: true, CancellationToken.None);
    }

    private static WorkItemService CreateWorkItemService(IWorkItemRepository repository)
    {
        var constructor = typeof(WorkItemService).GetConstructor(
        [
            typeof(WorkItemWriter),
            typeof(IWorkItemRepository),
            typeof(TimeProvider)
        ]);

        Assert.NotNull(constructor);

        return (WorkItemService)constructor.Invoke(
        [
            new WorkItemWriter(
                Substitute.For<IMeetingService>(),
                NullLogger<WorkItemWriter>.Instance),
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

    private static WorkItemRecord CreateWorkItemRecord(Guid userId, string sourceEventId)
    {
        return new WorkItemRecord(
            Guid.NewGuid(),
            userId,
            ExtractedItemKind.Meeting,
            "Send contract",
            "Please send the contract tomorrow.",
            "!sales:matrix.localhost",
            sourceEventId,
            null,
            new DateTimeOffset(2026, 03, 13, 15, 00, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 03, 14, 09, 00, 00, TimeSpan.Zero),
            new Confidence(0.87));
    }
}
