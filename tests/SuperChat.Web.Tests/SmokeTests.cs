using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Web.Tests;

public sealed class SmokeTests : IClassFixture<WebTestApplicationFactory>
{
    private readonly HttpClient _client;

    public SmokeTests(WebTestApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HomePage_DefaultsToRussian()
    {
        var response = await _client.GetAsync("/");
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Не теряйте важное в Telegram", content, StringComparison.Ordinal);
        Assert.Contains("class=\"landing-grid\"", content, StringComparison.Ordinal);
        Assert.Contains("lang=\"ru\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HomePage_SupportsEnglishViaQueryString()
    {
        var response = await _client.GetAsync("/?lang=en");
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Stop losing important work in Telegram", content, StringComparison.Ordinal);
        Assert.Contains("lang=\"en\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HomePage_UsesNewProductLandingChrome()
    {
        var response = await _client.GetAsync("/");
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("class=\"public-topbar\"", content, StringComparison.Ordinal);
        Assert.Contains("class=\"login-card premium-card\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"ok\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"aiModel\":\"deepseek-reasoner\"", content, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class WebTestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"superchat-web-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SuperChatDb"] = $"Data Source={_databasePath}",
                ["Persistence:Provider"] = "Sqlite",
                ["SuperChat:AllowedEmails:0"] = "pilot@example.com",
                ["SuperChat:AdminEmails:0"] = "admin@example.com",
                ["SuperChat:DevSeedSampleData"] = "true"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(IDbContextFactory<SuperChatDbContext>));
            services.RemoveAll(typeof(DbContextOptions<SuperChatDbContext>));
            services.AddDbContextFactory<SuperChatDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch
        {
        }
    }
}
