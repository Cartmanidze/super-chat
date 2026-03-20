using SuperChat.Contracts.Features.WorkItems;
using MeetingJoinLinkParser = SuperChat.Infrastructure.Shared.Presentation.MeetingJoinLinkParser;

namespace SuperChat.Tests;

public sealed class MeetingJoinLinkParserTests
{
    [Theory]
    [InlineData("https://meet.google.com/abc-defg-hij", MeetingJoinProvider.GoogleMeet)]
    [InlineData("https://us05web.zoom.us/j/123456789?pwd=secret", MeetingJoinProvider.Zoom)]
    [InlineData("https://teams.microsoft.com/l/meetup-join/19%3ameeting_example%40thread.v2/0?context=%7b%7d", MeetingJoinProvider.MicrosoftTeams)]
    [InlineData("https://teams.live.com/meet/1234567890123?p=abcdef", MeetingJoinProvider.MicrosoftTeams)]
    [InlineData("https://acme.webex.com/meet/demo", MeetingJoinProvider.Webex)]
    [InlineData("https://meet.jit.si/super-chat-sync", MeetingJoinProvider.JitsiMeet)]
    [InlineData("https://whereby.com/superchat-room", MeetingJoinProvider.Whereby)]
    [InlineData("https://telemost.yandex.ru/j/12345678901234", MeetingJoinProvider.YandexTelemost)]
    public void TryParse_RecognizesSupportedMeetingProviders(string url, MeetingJoinProvider expectedProvider)
    {
        var parsed = MeetingJoinLinkParser.TryParse($"Подключение здесь: {url}");

        Assert.NotNull(parsed);
        Assert.Equal(expectedProvider, parsed.Provider);
        Assert.Equal(new Uri(url).ToString(), parsed.Url.ToString());
    }

    [Fact]
    public void TryParse_TrimsTrailingPunctuation()
    {
        var url = "https://whereby.com/superchat-room";

        var parsed = MeetingJoinLinkParser.TryParse($"Залетайте: {url}),");

        Assert.NotNull(parsed);
        Assert.Equal(MeetingJoinProvider.Whereby, parsed.Provider);
        Assert.Equal(url, parsed.Url.ToString());
    }

    [Fact]
    public void TryParse_ReturnsNull_ForUnsupportedHost()
    {
        var parsed = MeetingJoinLinkParser.TryParse("Ссылка на созвон: https://example.com/meeting-room");

        Assert.Null(parsed);
    }
}
