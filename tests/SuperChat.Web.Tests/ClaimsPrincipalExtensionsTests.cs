using System.Security.Claims;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Web.Security;

namespace SuperChat.Web.Tests;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetUserId_ThrowsInvalidSession_WhenClaimIsInvalid()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "not-a-guid")
            ],
                authenticationType: "test"));

        var exception = Assert.Throws<InvalidSessionException>(() => principal.GetUserId());

        Assert.Equal(InvalidSessionFailureReason.MalformedUserIdClaim, exception.FailureReason);
        Assert.Equal("not-a-guid", exception.UserIdClaimValue);
    }

    [Fact]
    public void GetUserId_ThrowsInvalidSession_WhenClaimIsMissing()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(authenticationType: "test"));

        var exception = Assert.Throws<InvalidSessionException>(() => principal.GetUserId());

        Assert.Equal(InvalidSessionFailureReason.MissingUserIdClaim, exception.FailureReason);
        Assert.Null(exception.UserIdClaimValue);
    }

    [Fact]
    public void GetUserId_ThrowsInvalidSession_WhenClaimIsEmptyGuid()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString())
            ],
                authenticationType: "test"));

        var exception = Assert.Throws<InvalidSessionException>(() => principal.GetUserId());

        Assert.Equal(InvalidSessionFailureReason.EmptyUserIdClaim, exception.FailureReason);
        Assert.Equal(Guid.Empty.ToString(), exception.UserIdClaimValue);
    }

    [Fact]
    public void GetUserId_ReturnsUserId_WhenClaimIsValid()
    {
        var expected = Guid.NewGuid();
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, expected.ToString())
            ],
                authenticationType: "test"));

        var userId = principal.GetUserId();

        Assert.Equal(expected, userId);
    }
}
