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
}
