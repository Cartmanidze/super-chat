using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Tests;

public sealed class LikePatternEscaperTests
{
    [Fact]
    public void Escape_LeavesPlainTextUntouched()
    {
        Assert.Equal("contract", LikePatternEscaper.Escape("contract"));
        Assert.Equal("Привет, мир", LikePatternEscaper.Escape("Привет, мир"));
    }

    [Fact]
    public void Escape_EscapesPercentSign()
    {
        Assert.Equal("100\\%", LikePatternEscaper.Escape("100%"));
    }

    [Fact]
    public void Escape_EscapesUnderscore()
    {
        Assert.Equal("user\\_id", LikePatternEscaper.Escape("user_id"));
    }

    [Fact]
    public void Escape_EscapesBackslashBeforeOtherWildcards()
    {
        Assert.Equal("a\\\\b\\%c\\_d", LikePatternEscaper.Escape("a\\b%c_d"));
    }

    [Fact]
    public void Escape_HandlesEmptyString()
    {
        Assert.Equal(string.Empty, LikePatternEscaper.Escape(string.Empty));
    }

    [Fact]
    public void ToContainsPattern_WrapsEscapedValueWithPercents()
    {
        Assert.Equal("%abc%", LikePatternEscaper.ToContainsPattern("abc"));
        Assert.Equal("%50\\%%", LikePatternEscaper.ToContainsPattern("50%"));
        Assert.Equal("%foo\\_bar%", LikePatternEscaper.ToContainsPattern("foo_bar"));
    }

    [Fact]
    public void EscapeCharacter_IsBackslash()
    {
        Assert.Equal("\\", LikePatternEscaper.EscapeCharacter);
    }
}
