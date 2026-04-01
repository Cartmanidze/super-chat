using System.Net;

namespace SuperChat.Web.Tests;

[Collection("web-host")]
public sealed class ErrorHostTests : IClassFixture<WebTestApplicationFactory>, IClassFixture<ProductionWebTestApplicationFactory>
{
    private readonly WebTestApplicationFactory _developmentFactory;
    private readonly ProductionWebTestApplicationFactory _productionFactory;

    public ErrorHostTests(
        WebTestApplicationFactory developmentFactory,
        ProductionWebTestApplicationFactory productionFactory)
    {
        _developmentFactory = developmentFactory;
        _productionFactory = productionFactory;
    }

    [Fact]
    public async Task ErrorPage_ShowsDevelopmentHint_InDevelopment()
    {
        using var client = _developmentFactory.CreateClient();

        var response = await client.GetAsync("/error?lang=en");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Development Mode", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ErrorPage_HidesDevelopmentHint_InProduction()
    {
        using var client = _productionFactory.CreateClient();

        var response = await client.GetAsync("/error?lang=en");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Development Mode", content, StringComparison.Ordinal);
    }
}
