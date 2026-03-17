using SuperChat.Contracts.Features.Admin;
using SuperChat.Infrastructure.Abstractions;

namespace SuperChat.Infrastructure.Services;

public sealed class WorkerRuntimeMonitor(TimeProvider timeProvider) : IWorkerRuntimeMonitor
{
    private readonly object _gate = new();
    private readonly Dictionary<string, WorkerRuntimeStatusViewModel> _statuses = new(StringComparer.Ordinal);

    public void RegisterWorker(string key, string displayName)
    {
        lock (_gate)
        {
            if (_statuses.ContainsKey(key))
            {
                return;
            }

            _statuses[key] = new WorkerRuntimeStatusViewModel(
                key,
                displayName,
                WorkerRuntimeState.NotStarted,
                null,
                null,
                null,
                null);
        }
    }

    public void MarkRunning(string key, string displayName, string? details = null)
    {
        Update(key, displayName, current => current with
        {
            State = WorkerRuntimeState.Running,
            LastStartedAt = timeProvider.GetUtcNow(),
            Details = details,
            ErrorMessage = null
        });
    }

    public void MarkSucceeded(string key, string displayName, string? details = null)
    {
        var now = timeProvider.GetUtcNow();
        Update(key, displayName, current => current with
        {
            State = WorkerRuntimeState.Succeeded,
            LastFinishedAt = now,
            Details = details ?? current.Details,
            ErrorMessage = null
        });
    }

    public void MarkFailed(string key, string displayName, Exception exception, string? details = null)
    {
        var now = timeProvider.GetUtcNow();
        var errorMessage = $"{exception.GetType().Name}: {exception.Message}";
        Update(key, displayName, current => current with
        {
            State = WorkerRuntimeState.Failed,
            LastFinishedAt = now,
            Details = details ?? current.Details,
            ErrorMessage = errorMessage
        });
    }

    public void MarkDisabled(string key, string displayName, string? details = null)
    {
        Update(key, displayName, current => current with
        {
            State = WorkerRuntimeState.Disabled,
            Details = details,
            ErrorMessage = null
        });
    }

    public IReadOnlyList<WorkerRuntimeStatusViewModel> GetStatuses()
    {
        lock (_gate)
        {
            return _statuses.Values
                .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
                .ToList();
        }
    }

    private void Update(
        string key,
        string displayName,
        Func<WorkerRuntimeStatusViewModel, WorkerRuntimeStatusViewModel> mutate)
    {
        lock (_gate)
        {
            if (!_statuses.TryGetValue(key, out var current))
            {
                current = new WorkerRuntimeStatusViewModel(
                    key,
                    displayName,
                    WorkerRuntimeState.NotStarted,
                    null,
                    null,
                    null,
                    null);
            }

            _statuses[key] = mutate(current with { DisplayName = displayName, Key = key });
        }
    }
}
