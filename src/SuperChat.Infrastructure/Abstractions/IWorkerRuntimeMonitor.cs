using SuperChat.Contracts.Features.Admin;

namespace SuperChat.Infrastructure.Abstractions;

public interface IWorkerRuntimeMonitor
{
    void RegisterWorker(string key, string displayName);

    void MarkRunning(string key, string displayName, string? details = null);

    void MarkSucceeded(string key, string displayName, string? details = null);

    void MarkFailed(string key, string displayName, Exception exception, string? details = null);

    void MarkDisabled(string key, string displayName, string? details = null);

    IReadOnlyList<WorkerRuntimeStatusViewModel> GetStatuses();
}
