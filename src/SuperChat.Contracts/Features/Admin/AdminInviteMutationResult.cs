namespace SuperChat.Contracts.Features.Admin;

public sealed record AdminInviteMutationResult(
    bool Succeeded,
    string Message);
