namespace SuperChat.Domain.Features.Intelligence;

// Domain status describes the live state of a meeting.
// Completed is tracked separately via ResolvedAt and does not belong here.
public enum MeetingStatus
{
    PendingConfirmation = 0,
    Confirmed = 1,
    Cancelled = 2,
    Rescheduled = 3
}
