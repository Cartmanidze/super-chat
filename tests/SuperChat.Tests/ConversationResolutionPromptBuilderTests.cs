using SuperChat.Contracts;
using SuperChat.Infrastructure.Features.Intelligence.Resolution;

namespace SuperChat.Tests;

public sealed class ConversationResolutionPromptBuilderTests
{
    [Fact]
    public void BuildMessages_IncludesFutureMeetingCancellationOnlyRule()
    {
        var messages = ConversationResolutionPromptBuilder.BuildMessages(
            [],
            TimeZoneInfo.Utc,
            minConfidence: 0.7d);

        var systemMessage = Assert.Single(messages, item => item.Role == "system");

        Assert.Contains("can ONLY be resolved as \"cancelled\"", systemMessage.Content, StringComparison.Ordinal);
    }
}
