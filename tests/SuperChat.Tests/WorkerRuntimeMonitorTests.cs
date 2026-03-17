using SuperChat.Contracts.Features.Admin;
using SuperChat.Infrastructure.Services;

namespace SuperChat.Tests;

public sealed class WorkerRuntimeMonitorTests
{
    [Fact]
    public void MarkSucceeded_UpdatesWorkerStatusAndTimestamps()
    {
        var timeProvider = new StubTimeProvider(DateTimeOffset.Parse("2026-03-17T10:00:00Z"));
        var monitor = new WorkerRuntimeMonitor(timeProvider);

        monitor.RegisterWorker("matrix-sync", "Matrix Sync");
        monitor.MarkRunning("matrix-sync", "Matrix Sync");
        timeProvider.SetUtcNow(DateTimeOffset.Parse("2026-03-17T10:00:05Z"));
        monitor.MarkSucceeded("matrix-sync", "Matrix Sync", "Messages=12");

        var status = Assert.Single(monitor.GetStatuses());
        Assert.Equal(WorkerRuntimeState.Succeeded, status.State);
        Assert.Equal(DateTimeOffset.Parse("2026-03-17T10:00:00Z"), status.LastStartedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-03-17T10:00:05Z"), status.LastFinishedAt);
        Assert.Equal("Messages=12", status.Details);
        Assert.Null(status.ErrorMessage);
    }

    [Fact]
    public void MarkFailed_PreservesDetailsAndStoresError()
    {
        var timeProvider = new StubTimeProvider(DateTimeOffset.Parse("2026-03-17T10:00:00Z"));
        var monitor = new WorkerRuntimeMonitor(timeProvider);

        monitor.MarkRunning("chunk-indexing", "Chunk Indexing", "Selected=20");
        timeProvider.SetUtcNow(DateTimeOffset.Parse("2026-03-17T10:00:07Z"));
        monitor.MarkFailed("chunk-indexing", "Chunk Indexing", new InvalidOperationException("Qdrant unavailable"));

        var status = Assert.Single(monitor.GetStatuses());
        Assert.Equal(WorkerRuntimeState.Failed, status.State);
        Assert.Equal("Selected=20", status.Details);
        Assert.Equal("InvalidOperationException: Qdrant unavailable", status.ErrorMessage);
        Assert.Equal(DateTimeOffset.Parse("2026-03-17T10:00:07Z"), status.LastFinishedAt);
    }

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void SetUtcNow(DateTimeOffset value)
        {
            _utcNow = value;
        }
    }
}
