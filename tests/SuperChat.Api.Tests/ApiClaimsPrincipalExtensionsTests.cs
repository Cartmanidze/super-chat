using System.Security.Claims;
using SuperChat.Api.Security;
using SuperChat.Contracts.Features.Auth;

namespace SuperChat.Api.Tests;

public sealed class ApiClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetRequiredUserId_ThrowsInvalidSession_WhenClaimIsInvalid()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "not-a-guid")
            ],
                authenticationType: "test"));

        var exception = Assert.Throws<InvalidSessionException>(() => principal.GetRequiredUserId());

        Assert.Equal(InvalidSessionFailureReason.MalformedUserIdClaim, exception.FailureReason);
        Assert.Equal("not-a-guid", exception.UserIdClaimValue);
    }

    [Fact]
    public void GetRequiredUserId_ThrowsInvalidSession_WhenClaimIsMissing()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(authenticationType: "test"));

        var exception = Assert.Throws<InvalidSessionException>(() => principal.GetRequiredUserId());

        Assert.Equal(InvalidSessionFailureReason.MissingUserIdClaim, exception.FailureReason);
        Assert.Null(exception.UserIdClaimValue);
    }

    [Fact]
    public void GetRequiredUserId_ThrowsInvalidSession_WhenClaimIsEmptyGuid()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString())
            ],
                authenticationType: "test"));

        var exception = Assert.Throws<InvalidSessionException>(() => principal.GetRequiredUserId());

        Assert.Equal(InvalidSessionFailureReason.EmptyUserIdClaim, exception.FailureReason);
        Assert.Equal(Guid.Empty.ToString(), exception.UserIdClaimValue);
    }

    [Fact]
    public void GetRequiredUserId_ReturnsUserId_WhenClaimIsValid()
    {
        var expected = Guid.NewGuid();
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, expected.ToString())
            ],
                authenticationType: "test"));

        var userId = principal.GetRequiredUserId();

        Assert.Equal(expected, userId);
    }
}
