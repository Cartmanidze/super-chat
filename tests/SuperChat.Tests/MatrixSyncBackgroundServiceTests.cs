using SuperChat.Infrastructure.HostedServices;

namespace SuperChat.Tests;

public sealed class MatrixSyncBackgroundServiceTests
{
    [Fact]
    public void IsManagementRoom_ReturnsTrueForExactMatch()
    {
        var result = MatrixSyncBackgroundService.IsManagementRoom(
            "!bridge:matrix.example",
            "!bridge:matrix.example");

        Assert.True(result);
    }

    [Fact]
    public void IsManagementRoom_ReturnsFalseForDifferentRoom()
    {
        var result = MatrixSyncBackgroundService.IsManagementRoom(
            "!chat:matrix.example",
            "!bridge:matrix.example");

        Assert.False(result);
    }

    [Fact]
    public void IsManagementRoom_ReturnsFalseWhenManagementRoomIsMissing()
    {
        var result = MatrixSyncBackgroundService.IsManagementRoom(
            "!chat:matrix.example",
            null);

        Assert.False(result);
    }

    [Fact]
    public void GetInvitedRoomsToJoin_ExcludesManagementRoomAndDuplicates()
    {
        var result = MatrixSyncBackgroundService.GetInvitedRoomsToJoin(
            ["!portal-a:matrix.example", "!bridge:matrix.example", "!portal-a:matrix.example", "!portal-b:matrix.example"],
            "!bridge:matrix.example");

        Assert.Equal(["!portal-a:matrix.example", "!portal-b:matrix.example"], result);
    }

    [Fact]
    public void GetInvitedRoomsToJoin_IgnoresEmptyIds()
    {
        var result = MatrixSyncBackgroundService.GetInvitedRoomsToJoin(
            ["", "   ", "!portal:matrix.example"],
            null);

        Assert.Equal(["!portal:matrix.example"], result);
    }

    [Fact]
    public void ShouldIngestRoom_AllowsDirectRooms()
    {
        var result = MatrixSyncBackgroundService.ShouldIngestRoom(
            "!dm:matrix.example",
            "!bridge:matrix.example",
            true,
            2,
            30);

        Assert.True(result);
    }

    [Fact]
    public void ShouldIngestRoom_AllowsGroupAtConfiguredLimit()
    {
        var result = MatrixSyncBackgroundService.ShouldIngestRoom(
            "!group:matrix.example",
            "!bridge:matrix.example",
            false,
            30,
            30);

        Assert.True(result);
    }

    [Fact]
    public void ShouldIngestRoom_RejectsGroupAboveConfiguredLimit()
    {
        var result = MatrixSyncBackgroundService.ShouldIngestRoom(
            "!group:matrix.example",
            "!bridge:matrix.example",
            false,
            31,
            30);

        Assert.False(result);
    }

    [Fact]
    public void ShouldIngestRoom_AlwaysRejectsManagementRoom()
    {
        var result = MatrixSyncBackgroundService.ShouldIngestRoom(
            "!bridge:matrix.example",
            "!bridge:matrix.example",
            true,
            2,
            30);

        Assert.False(result);
    }
}
