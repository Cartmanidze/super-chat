using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Features.Operations;

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
            null,
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
            null,
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
            null,
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
            null,
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
            null,
            true,
            30);

        Assert.False(result);
    }

    [Fact]
    public void ShouldIngestRoom_RejectsBroadcastChannelsEvenWhenMatrixMarksRoomAsDirect()
    {
        var result = MatrixSyncBackgroundService.ShouldIngestRoom(
            "!channel:matrix.example",
            "!bridge:matrix.example",
            true,
            new TelegramRoomInfo("!channel:matrix.example", "channel", 12, "Broadcast", true),
            null,
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
            null,
            true,
            30);

        Assert.False(result);
    }

    [Fact]
    public void ShouldIngestRoom_AllowsGroupByMatrixMemberCount_WhenTelegramInfoIsMissing()
    {
        var result = MatrixSyncBackgroundService.ShouldIngestRoom(
            "!group:matrix.example",
            "!bridge:matrix.example",
            false,
            null,
            12,
            true,
            30);

        Assert.True(result);
    }

    [Fact]
    public void ShouldIngestRoom_RejectsGroupByMatrixMemberCount_WhenAboveConfiguredLimit()
    {
        var result = MatrixSyncBackgroundService.ShouldIngestRoom(
            "!group:matrix.example",
            "!bridge:matrix.example",
            false,
            null,
            45,
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
            null,
            true,
            30);

        Assert.False(result);
    }

    [Fact]
    public void ShouldIngestMessageBody_RejectsForwardedChannelPosts()
    {
        var result = MatrixSyncBackgroundService.ShouldIngestMessageBody(
            "Forwarded from channel Example:\n> News update");

        Assert.False(result);
    }

    [Fact]
    public void ShouldIngestMessageBody_RejectsRussianForwardedChannelPosts()
    {
        var result = MatrixSyncBackgroundService.ShouldIngestMessageBody(
            "Переслано из канала Новости:\n> Апдейт");

        Assert.False(result);
    }

    [Fact]
    public void ShouldIngestMessageBody_AllowsRegularDirectMessages()
    {
        var result = MatrixSyncBackgroundService.ShouldIngestMessageBody(
            "Please send the proposal tomorrow.");

        Assert.True(result);
    }

    [Fact]
    public void ShouldRetryBridgeLogin_ReturnsTrue_ForBridgeGreetingWhilePending()
    {
        var result = MatrixSyncBackgroundService.ShouldRetryBridgeLogin(
            TelegramConnectionState.BridgePending,
            connected: false,
            discoveredLoginUrl: null,
            sawBridgeGreeting: true);

        Assert.True(result);
    }

    [Fact]
    public void ShouldRetryBridgeLogin_ReturnsFalse_WhenLoginUrlIsAlreadyKnown()
    {
        var result = MatrixSyncBackgroundService.ShouldRetryBridgeLogin(
            TelegramConnectionState.BridgePending,
            connected: false,
            discoveredLoginUrl: "https://bridge.example.com/login",
            sawBridgeGreeting: true);

        Assert.False(result);
    }

    [Fact]
    public void ResolveConnectionStateAfterSuccessfulSync_ReturnsBridgePending_WhenCurrentStateIsError()
    {
        var result = MatrixSyncBackgroundService.ResolveConnectionStateAfterSuccessfulSync(
            TelegramConnectionState.Error,
            connected: false);

        Assert.Equal(TelegramConnectionState.BridgePending, result);
    }

    [Fact]
    public void ResolveConnectionStateAfterSuccessfulSync_ReturnsConnected_WhenConnectionIsLive()
    {
        var result = MatrixSyncBackgroundService.ResolveConnectionStateAfterSuccessfulSync(
            TelegramConnectionState.Error,
            connected: true);

        Assert.Equal(TelegramConnectionState.Connected, result);
    }

    [Fact]
    public void LooksLikeBridgeGreeting_ReturnsTrue_ForBridgeHello()
    {
        var result = MatrixSyncBackgroundService.LooksLikeBridgeGreeting(
            "Hello, I'm a Telegram bridge bot. Use `login` to continue.");

        Assert.True(result);
    }
}
