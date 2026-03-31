using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SuperChat.Infrastructure.Shared.Persistence;

using Xunit;

namespace SuperChat.Web.Tests;

[Collection("web-host")]
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
        Assert.Equal("{\"status\":\"ok\"}", content);
    }

    [Fact]
    public async Task MetricsEndpoint_ExposesApplicationMetrics()
    {
        var response = await _client.GetAsync("/metrics");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("superchat_pipeline_commands_total", content, StringComparison.Ordinal);
    }
}

[Collection("web-host")]
public sealed class PipelineEnabledSmokeTests : IClassFixture<PipelineEnabledWebTestApplicationFactory>
{
    private readonly HttpClient _client;

    public PipelineEnabledSmokeTests(PipelineEnabledWebTestApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy_WhenPipelineMessagingEnabledOnSqlite()
    {
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"status\":\"ok\"}", content);
    }
}

[CollectionDefinition("web-host", DisableParallelization = true)]
public sealed class WebHostCollectionDefinition
{
}

public class WebTestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"superchat-web-{Guid.NewGuid():N}.db");

    protected virtual bool PipelineMessagingEnabled => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SuperChatDb"] = $"Data Source={_databasePath}",
                ["Persistence:Provider"] = "Sqlite",
                ["PipelineMessaging:Enabled"] = PipelineMessagingEnabled ? "true" : "false",
                ["SuperChat:AdminEmails:0"] = "admin@example.com",
                ["SuperChat:DevSeedSampleData"] = "true"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(IDbContextFactory<SuperChatDbContext>));
            services.RemoveAll(typeof(DbContextOptions<SuperChatDbContext>));
            services.AddDbContextFactory<SuperChatDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
            services.RemoveAll(typeof(SuperChat.Contracts.Features.Auth.IVerificationCodeSender));
            services.AddSingleton<SuperChat.Contracts.Features.Auth.IVerificationCodeSender, NoOpWebCodeSender>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var previousPipelineEnabled = Environment.GetEnvironmentVariable("PipelineMessaging__Enabled");
        var previousPersistenceProvider = Environment.GetEnvironmentVariable("Persistence__Provider");
        var previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__SuperChatDb");
        Environment.SetEnvironmentVariable("PipelineMessaging__Enabled", PipelineMessagingEnabled ? "true" : "false");
        Environment.SetEnvironmentVariable("Persistence__Provider", "Sqlite");
        Environment.SetEnvironmentVariable("ConnectionStrings__SuperChatDb", $"Data Source={_databasePath}");

        IHost host;
        try
        {
            host = base.CreateHost(builder);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PipelineMessaging__Enabled", previousPipelineEnabled);
            Environment.SetEnvironmentVariable("Persistence__Provider", previousPersistenceProvider);
            Environment.SetEnvironmentVariable("ConnectionStrings__SuperChatDb", previousConnectionString);
        }

        using var scope = host.Services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SuperChatDbContext>>();
        using var dbContext = dbContextFactory.CreateDbContext();
        dbContext.Database.EnsureCreated();

        return host;
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

public sealed class PipelineEnabledWebTestApplicationFactory : WebTestApplicationFactory
{
    protected override bool PipelineMessagingEnabled => true;
}

internal sealed class NoOpWebCodeSender : SuperChat.Contracts.Features.Auth.IVerificationCodeSender
{
    public Task SendAsync(string email, string code, CancellationToken cancellationToken) => Task.CompletedTask;
}
