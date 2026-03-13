using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Health;
using SuperChat.Infrastructure.HostedServices;
using SuperChat.Infrastructure.Persistence;

namespace SuperChat.Infrastructure.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSuperChatBootstrap(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableBackgroundWorkers = true)
    {
        services.AddMemoryCache();

        services
            .AddOptions<ChunkingOptions>()
            .Bind(configuration.GetSection(ChunkingOptions.SectionName));
        services
            .AddOptions<ChunkIndexingOptions>()
            .Bind(configuration.GetSection(ChunkIndexingOptions.SectionName));
        services
            .AddOptions<DeepSeekOptions>()
            .Bind(configuration.GetSection(DeepSeekOptions.SectionName));
        services
            .AddOptions<EmbeddingOptions>()
            .Bind(configuration.GetSection(EmbeddingOptions.SectionName));
        services
            .AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName));
        services
            .AddOptions<MatrixOptions>()
            .Bind(configuration.GetSection(MatrixOptions.SectionName));
        services
            .AddOptions<PilotOptions>()
            .Bind(configuration.GetSection(PilotOptions.SectionName));
        services
            .AddOptions<QdrantOptions>()
            .Bind(configuration.GetSection(QdrantOptions.SectionName));
        services
            .AddOptions<RetrievalOptions>()
            .Bind(configuration.GetSection(RetrievalOptions.SectionName));
        services
            .AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection(PersistenceOptions.SectionName));
        services
            .AddOptions<TelegramBridgeOptions>()
            .Bind(configuration.GetSection(TelegramBridgeOptions.SectionName));

        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PilotOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<MatrixOptions>>().Value);
        services.AddHttpClient<MatrixApiClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MatrixOptions>>().Value;
            client.BaseAddress = new Uri(options.HomeserverUrl.TrimEnd('/'));
        });
        services.AddHttpClient<ITelegramRoomInfoService, TelegramRoomInfoService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<TelegramBridgeOptions>>().Value;
            if (Uri.TryCreate(options.ParticipantCountBaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }
        });
        services.AddHttpClient<IQdrantClient, QdrantClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<QdrantOptions>>().Value;
            if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }

            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                client.DefaultRequestHeaders.Add("api-key", options.ApiKey);
            }
        });
        services.AddHttpClient<IEmbeddingService, EmbeddingServiceClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<EmbeddingOptions>>().Value;
            if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }

            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        });
        services.AddDbContextFactory<SuperChatDbContext>((serviceProvider, optionsBuilder) =>
        {
            var persistence = serviceProvider.GetRequiredService<IOptions<PersistenceOptions>>().Value;
            var connectionString = configuration.GetConnectionString("SuperChatDb");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = persistence.ConnectionString;
            }

            if (string.IsNullOrWhiteSpace(connectionString) && persistence.Provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                connectionString = $"Data Source={persistence.DatabaseName}.db";
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = "Host=localhost;Port=5432;Database=superchat_app;Username=postgres;Password=postgres";
            }

            if (IsSqliteConnectionString(connectionString) || persistence.Provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                optionsBuilder.UseSqlite(connectionString);
                return;
            }

            optionsBuilder.UseNpgsql(connectionString);
        });

        services.AddSingleton<IAuthFlowService, AuthFlowService>();
        services.AddSingleton<IApiSessionService, ApiSessionService>();
        services.AddSingleton<IMatrixProvisioningService, MatrixProvisioningService>();
        services.AddSingleton<ITelegramConnectionService, TelegramConnectionService>();
        services.AddSingleton<IIntegrationConnectionService, IntegrationConnectionService>();
        services.AddSingleton<IRoomDisplayNameService, MatrixRoomDisplayNameService>();
        services.AddSingleton<IMessageNormalizationService, MessageNormalizationService>();
        services.AddSingleton<IChatTemplateCatalog, ChatTemplateCatalog>();
        services.AddSingleton<IChatTemplateHandler, TodayChatTemplateHandler>();
        services.AddSingleton<IChatTemplateHandler, WaitingChatTemplateHandler>();
        services.AddSingleton<IChatTemplateHandler, MeetingsChatTemplateHandler>();
        services.AddSingleton<IChatTemplateHandler, RecentChatTemplateHandler>();
        services.AddSingleton<IChunkBuilderService, ChunkBuilderService>();
        services.AddSingleton<IChunkIndexingService, ChunkIndexingService>();
        services.AddSingleton<IMeetingService, MeetingService>();
        services.AddSingleton<IExtractedItemService, ExtractedItemService>();
        services.AddSingleton<IRetrievalService, RetrievalService>();
        services.AddSingleton<HeuristicStructuredExtractionService>();
        services.AddSingleton<DeepSeekStructuredExtractionService>();
        services.AddSingleton<IAiStructuredExtractionService, BootstrapStructuredExtractionService>();
        services.AddSingleton<IDigestService, DigestService>();
        services.AddSingleton<ISearchService, SearchService>();
        services.AddSingleton<IChatExperienceService, ChatExperienceService>();
        services.AddSingleton<IFeedbackService, FeedbackService>();
        services.AddSingleton<IHealthSnapshotService, HealthSnapshotService>();
        services.AddHostedService<PersistenceInitializationHostedService>();
        services.AddHostedService<QdrantInitializationHostedService>();

        if (enableBackgroundWorkers)
        {
            services.AddHostedService<MatrixSyncBackgroundService>();
            services.AddHostedService<ChunkBuilderBackgroundService>();
            services.AddHostedService<ChunkIndexingBackgroundService>();
            services.AddHostedService<ExtractionBackgroundService>();
        }

        services.AddHealthChecks().AddCheck<BootstrapHealthCheck>("bootstrap");

        return services;
    }

    private static bool IsSqliteConnectionString(string connectionString)
    {
        return connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
               connectionString.EndsWith(".db", StringComparison.OrdinalIgnoreCase);
    }
}
