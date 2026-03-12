using SuperChat.Infrastructure.Abstractions;
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
            null,
            false,
            30);

        Assert.True(result);
    }

    [Fact]
    public void ShouldIngestRoom_AllowsTelegramDirectRooms()
    {
        var result = MatrixSyncBackgroundService.ShouldIngestRoom(
            "!dm:matrix.example",
            "!bridge:matrix.example",
            false,
            new TelegramRoomInfo("!dm:matrix.example", "user", null, "Alex", false),
            false,
            30);

        Assert.True(result);
    }

    [Fact]
    public void ShouldIngestRoom_RejectsGroupsWhenFeatureFlagIsDisabled()
    {
        var result = MatrixSyncBackgroundService.ShouldIngestRoom(
            "!group:matrix.example",
            "!bridge:matrix.example",
            false,
            new TelegramRoomInfo("!group:matrix.example", "channel", 30, "Team", false),
            false,
            30);

        Assert.False(result);
    }

    [Fact]
    public void ShouldIngestRoom_AllowsGroupAtConfiguredLimit_WhenFeatureFlagIsEnabled()
    {
        var result = MatrixSyncBackgroundService.ShouldIngestRoom(
            "!group:matrix.example",
            "!bridge:matrix.example",
            false,
            new TelegramRoomInfo("!group:matrix.example", "channel", 30, "Team", false),
            true,
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
            new TelegramRoomInfo("!group:matrix.example", "channel", 31, "Big group", false),
            true,
            30);

        Assert.False(result);
    }

    [Fact]
    public void ShouldIngestRoom_RejectsBroadcastChannels()
    {
        var result = MatrixSyncBackgroundService.ShouldIngestRoom(
            "!channel:matrix.example",
            "!bridge:matrix.example",
            false,
            new TelegramRoomInfo("!channel:matrix.example", "channel", 12, "Broadcast", true),
            true,
            30);

        Assert.False(result);
    }

    [Fact]
    public void ShouldIngestRoom_RejectsUnknownGroupWhenTelegramInfoIsMissing()
    {
        var result = MatrixSyncBackgroundService.ShouldIngestRoom(
            "!group:matrix.example",
            "!bridge:matrix.example",
            false,
            null,
            true,
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
            new TelegramRoomInfo("!bridge:matrix.example", "user", null, "Bridge", false),
            true,
            30);

        Assert.False(result);
    }
}
