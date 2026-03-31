using SuperChat.Contracts.Features.Integrations.Telegram;
using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Features.Operations.Sync;

namespace SuperChat.Tests;

public sealed class MatrixSyncBackgroundServiceTests
{
    [Fact]
    public void IsManagementRoom_ReturnsTrueForExactMatch()
    {
        var result = SyncStateResolver.IsManagementRoom(
            "!bridge:matrix.example",
            "!bridge:matrix.example");

        Assert.True(result);
    }

    [Fact]
    public void IsManagementRoom_ReturnsFalseForDifferentRoom()
    {
        var result = SyncStateResolver.IsManagementRoom(
            "!chat:matrix.example",
            "!bridge:matrix.example");

        Assert.False(result);
    }

    [Fact]
    public void IsManagementRoom_ReturnsFalseWhenManagementRoomIsMissing()
    {
        var result = SyncStateResolver.IsManagementRoom(
            "!chat:matrix.example",
            null);

        Assert.False(result);
    }

    [Fact]
    public void GetInvitedRoomsToJoin_ExcludesManagementRoomAndDuplicates()
    {
        var result = SyncStateResolver.GetInvitedRoomsToJoin(
            ["!portal-a:matrix.example", "!bridge:matrix.example", "!portal-a:matrix.example", "!portal-b:matrix.example"],
            "!bridge:matrix.example");

        Assert.Equal(["!portal-a:matrix.example", "!portal-b:matrix.example"], result);
    }

    [Fact]
    public void GetInvitedRoomsToJoin_IgnoresEmptyIds()
    {
        var result = SyncStateResolver.GetInvitedRoomsToJoin(
            ["", "   ", "!portal:matrix.example"],
            null);

        Assert.Equal(["!portal:matrix.example"], result);
    }

    [Fact]
    public void ShouldIngestRoom_AllowsDirectRooms()
    {
        var result = ChatRoomHandler.ShouldIngestRoom(
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
        var result = ChatRoomHandler.ShouldIngestRoom(
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
        var result = ChatRoomHandler.ShouldIngestRoom(
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
        var result = ChatRoomHandler.ShouldIngestRoom(
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
        var result = ChatRoomHandler.ShouldIngestRoom(
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
        var result = ChatRoomHandler.ShouldIngestRoom(
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
        var result = ChatRoomHandler.ShouldIngestRoom(
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
        var result = ChatRoomHandler.ShouldIngestRoom(
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
        var result = ChatRoomHandler.ShouldIngestRoom(
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
        var result = ChatRoomHandler.ShouldIngestRoom(
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
        var result = ChatRoomHandler.ShouldIngestRoom(
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
        var result = ChatRoomHandler.ShouldIngestMessageBody(
            "Forwarded from channel Example:\n> News update");

        Assert.False(result);
    }

    [Fact]
    public void ShouldIngestMessageBody_RejectsRussianForwardedChannelPosts()
    {
        var result = ChatRoomHandler.ShouldIngestMessageBody(
            "Переслано из канала Новости:\n> Апдейт");

        Assert.False(result);
    }

    [Fact]
    public void ShouldIngestMessageBody_AllowsRegularDirectMessages()
    {
        var result = ChatRoomHandler.ShouldIngestMessageBody(
            "Please send the proposal tomorrow.");

        Assert.True(result);
    }

    [Fact]
    public void ShouldRetryBridgeLogin_ReturnsTrue_ForBridgeGreetingWhilePending()
    {
        var result = ManagementRoomHandler.ShouldRetryBridgeLogin(
            TelegramConnectionState.BridgePending,
            connected: false,
            discoveredLoginUrl: null,
            sawBridgeGreeting: true);

        Assert.True(result);
    }

    [Fact]
    public void ShouldRetryBridgeLogin_ReturnsFalse_WhenLoginUrlIsAlreadyKnown()
    {
        var result = ManagementRoomHandler.ShouldRetryBridgeLogin(
            TelegramConnectionState.BridgePending,
            connected: false,
            discoveredLoginUrl: "https://bridge.example.com/login",
            sawBridgeGreeting: true);

        Assert.False(result);
    }

    [Fact]
    public void ResolveConnectionStateAfterSuccessfulSync_ReturnsBridgePending_WhenCurrentStateIsError()
    {
        var result = SyncStateResolver.ResolveConnectionStateAfterSuccessfulSync(
            TelegramConnectionState.Error,
            connected: false);

        Assert.Equal(TelegramConnectionState.BridgePending, result);
    }

    [Fact]
    public void ResolveConnectionStateAfterSuccessfulSync_ReturnsConnected_WhenConnectionIsLive()
    {
        var result = SyncStateResolver.ResolveConnectionStateAfterSuccessfulSync(
            TelegramConnectionState.Error,
            connected: true);

        Assert.Equal(TelegramConnectionState.Connected, result);
    }

    [Fact]
    public void LooksLikeBridgeGreeting_ReturnsTrue_ForBridgeHello()
    {
        var result = ManagementRoomHandler.LooksLikeBridgeGreeting(
            "Hello, I'm a Telegram bridge bot. Use `login` to continue.");

        Assert.True(result);
    }

    // --- DetectLoginStep tests ---

    [Theory]
    [InlineData("Please send your phone number to log in.", TelegramConnectionState.LoginAwaitingPhone)]
    [InlineData("Send your phone number here to log in via this chat.", TelegramConnectionState.LoginAwaitingPhone)]
    public void DetectLoginStep_DetectsPhoneRequest(string message, TelegramConnectionState expected)
    {
        Assert.Equal(expected, ManagementRoomHandler.DetectLoginStep(message));
    }

    [Theory]
    [InlineData("Enter the code you received.", TelegramConnectionState.LoginAwaitingCode)]
    [InlineData("A verification code has been sent to your Telegram app.", TelegramConnectionState.LoginAwaitingCode)]
    [InlineData("Send the code here.", TelegramConnectionState.LoginAwaitingCode)]
    [InlineData("Send the code to the bot.", TelegramConnectionState.LoginAwaitingCode)]
    public void DetectLoginStep_DetectsCodeRequest(string message, TelegramConnectionState expected)
    {
        Assert.Equal(expected, ManagementRoomHandler.DetectLoginStep(message));
    }

    [Theory]
    [InlineData("Two-step verification is enabled. Please send your password here.", TelegramConnectionState.LoginAwaitingPassword)]
    [InlineData("2FA is enabled. Send your password.", TelegramConnectionState.LoginAwaitingPassword)]
    public void DetectLoginStep_DetectsPasswordRequest(string message, TelegramConnectionState expected)
    {
        Assert.Equal(expected, ManagementRoomHandler.DetectLoginStep(message));
    }

    [Theory]
    [InlineData("Successfully logged in as +79991234567.")]
    [InlineData("Hello, I'm a Telegram bridge bot.")]
    [InlineData("")]
    [InlineData(null)]
    public void DetectLoginStep_ReturnsNull_ForNonLoginMessages(string? message)
    {
        Assert.Null(ManagementRoomHandler.DetectLoginStep(message!));
    }

    // --- LooksLikeLostConnection tests ---

    [Theory]
    [InlineData("You're not logged in.", true)]
    [InlineData("Not logged in. Use `login` to start.", true)]
    [InlineData("Successfully logged in.", false)]
    [InlineData("Hello, I'm a bridge bot.", false)]
    public void LooksLikeLostConnection_DetectsCorrectly(string message, bool expected)
    {
        Assert.Equal(expected, ManagementRoomHandler.LooksLikeLostConnection(message));
    }

    // --- ResolveConnectionStateAfterSuccessfulSync with detectedLoginStep ---

    [Fact]
    public void ResolveConnectionStateAfterSuccessfulSync_ConnectedOverridesLoginStep()
    {
        var result = SyncStateResolver.ResolveConnectionStateAfterSuccessfulSync(
            TelegramConnectionState.LoginAwaitingCode,
            connected: true,
            detectedLoginStep: TelegramConnectionState.LoginAwaitingPassword);

        Assert.Equal(TelegramConnectionState.Connected, result);
    }

    [Fact]
    public void ResolveConnectionStateAfterSuccessfulSync_ForwardLoginStepApplied()
    {
        var result = SyncStateResolver.ResolveConnectionStateAfterSuccessfulSync(
            TelegramConnectionState.LoginAwaitingPhone,
            connected: false,
            detectedLoginStep: TelegramConnectionState.LoginAwaitingCode);

        Assert.Equal(TelegramConnectionState.LoginAwaitingCode, result);
    }

    [Fact]
    public void ResolveConnectionStateAfterSuccessfulSync_BackwardLoginStepRejected()
    {
        var result = SyncStateResolver.ResolveConnectionStateAfterSuccessfulSync(
            TelegramConnectionState.LoginAwaitingCode,
            connected: false,
            detectedLoginStep: TelegramConnectionState.LoginAwaitingPhone);

        Assert.Equal(TelegramConnectionState.LoginAwaitingCode, result);
    }

    [Fact]
    public void ResolveConnectionStateAfterSuccessfulSync_BridgePendingNotHijackedByLoginStep()
    {
        var result = SyncStateResolver.ResolveConnectionStateAfterSuccessfulSync(
            TelegramConnectionState.BridgePending,
            connected: false,
            detectedLoginStep: TelegramConnectionState.LoginAwaitingPhone);

        Assert.Equal(TelegramConnectionState.BridgePending, result);
    }

    [Fact]
    public void ResolveConnectionStateAfterSuccessfulSync_NullLoginStepKeepsCurrentState()
    {
        var result = SyncStateResolver.ResolveConnectionStateAfterSuccessfulSync(
            TelegramConnectionState.LoginAwaitingPhone,
            connected: false,
            detectedLoginStep: null);

        Assert.Equal(TelegramConnectionState.LoginAwaitingPhone, result);
    }

    // --- LooksLikeSuccessfulLogin tests ---

    [Theory]
    [InlineData("Successfully logged in as +79991234567.", true)]
    [InlineData("Logged in as @user.", true)]
    [InlineData("Login successful!", true)]
    [InlineData("You're not logged in.", false)]
    [InlineData("Not logged in. Use `login` to start.", false)]
    [InlineData("Hello, I'm a bridge bot.", false)]
    [InlineData("Check if you're logged into Telegram.", false)]
    [InlineData("Click here to log in.", false)]
    public void LooksLikeSuccessfulLogin_DetectsCorrectly(string message, bool expected)
    {
        Assert.Equal(expected, ManagementRoomHandler.LooksLikeSuccessfulLogin(message));
    }

    // --- ShouldRetryBridgeLogin with LoginAwaiting states ---

    [Fact]
    public void ShouldRetryBridgeLogin_ReturnsTrue_ForLoginAwaitingPhoneWithGreeting()
    {
        var result = ManagementRoomHandler.ShouldRetryBridgeLogin(
            TelegramConnectionState.LoginAwaitingPhone,
            connected: false,
            discoveredLoginUrl: null,
            sawBridgeGreeting: true);

        Assert.True(result);
    }

    [Theory]
    [InlineData(TelegramConnectionState.LoginAwaitingCode)]
    [InlineData(TelegramConnectionState.LoginAwaitingPassword)]
    public void ShouldRetryBridgeLogin_ReturnsFalse_ForAdvancedLoginStates(TelegramConnectionState state)
    {
        var result = ManagementRoomHandler.ShouldRetryBridgeLogin(
            state,
            connected: false,
            discoveredLoginUrl: null,
            sawBridgeGreeting: true);

        Assert.False(result);
    }

    // --- P1: Auto-reconnect on lost connection ---

    [Fact]
    public void ResolveConnectionStateAfterSuccessfulSync_ReturnsBridgePending_OnLostConnection()
    {
        var result = SyncStateResolver.ResolveConnectionStateAfterSuccessfulSync(
            TelegramConnectionState.Connected,
            connected: false,
            lostConnection: true);

        Assert.Equal(TelegramConnectionState.BridgePending, result);
    }
}
