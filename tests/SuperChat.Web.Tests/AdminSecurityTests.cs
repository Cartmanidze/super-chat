using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Testing;
using SuperChat.Contracts.Configuration;
using SuperChat.Web.Security;

namespace SuperChat.Web.Tests;

public sealed class AdminSecurityTests : IClassFixture<WebTestApplicationFactory>
{
    private readonly WebTestApplicationFactory _factory;

    public AdminSecurityTests(WebTestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void IsAdmin_ReturnsTrueOnlyForConfiguredEmail()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Email, "glebon84@gmail.com")
        ], "test"));

        var result = principal.IsAdmin(new PilotOptions
        {
            AdminEmails = ["glebon84@gmail.com"]
        });

        Assert.True(result);
    }

    [Fact]
    public async Task AdminPage_RedirectsAnonymousUserToMagicLinkRequest()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/auth/request-link", response.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
    }
}
