using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SuperChat.Web.Tests;

public sealed class SmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SmokeTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HomePage_Loads()
    {
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Turn scattered chats into a daily brief", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"ok\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"aiModel\":\"deepseek-chat\"", content, StringComparison.OrdinalIgnoreCase);
    }
}
