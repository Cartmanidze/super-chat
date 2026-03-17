namespace SuperChat.Infrastructure.Services;

internal static class MatrixRoomSummaryMappings
{
    public static int? GetMemberCount(this MatrixRoomSummaryPayload? summary)
    {
        if (summary?.JoinedMemberCount is null && summary?.InvitedMemberCount is null)
        {
            return null;
        }

        return Math.Max(0, summary?.JoinedMemberCount.GetValueOrDefault() ?? 0) +
               Math.Max(0, summary?.InvitedMemberCount.GetValueOrDefault() ?? 0);
    }
}
