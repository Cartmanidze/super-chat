namespace SuperChat.Contracts.Features.Admin;

public enum WorkerRuntimeState
{
    NotStarted = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Disabled = 4
}

public sealed record WorkerRuntimeStatusViewModel(
    string Key,
    string DisplayName,
    WorkerRuntimeState State,
    DateTimeOffset? LastStartedAt,
    DateTimeOffset? LastFinishedAt,
    string? Details,
    string? ErrorMessage);
