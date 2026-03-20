using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QdrantSdk = Qdrant.Client.QdrantClient;
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
            .AddOptions<ChatAnsweringOptions>()
            .Bind(configuration.GetSection(ChatAnsweringOptions.SectionName));
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
            .AddOptions<TextEnrichmentOptions>()
            .Bind(configuration.GetSection(TextEnrichmentOptions.SectionName));
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
            .AddOptions<MeetingProjectionOptions>()
            .Bind(configuration.GetSection(MeetingProjectionOptions.SectionName));
        services
            .AddOptions<MessageIngestionFilterOptions>()
            .Bind(configuration.GetSection(MessageIngestionFilterOptions.SectionName));
        services
            .AddOptions<PilotOptions>()
            .Bind(configuration.GetSection(PilotOptions.SectionName));
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
        services.AddSuperChatQdrant(configuration);
        services.AddHttpClient<IEmbeddingService, EmbeddingServiceClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<EmbeddingOptions>>().Value;
            var configuredBaseUrl = string.Equals(options.Backend, "YandexCloud", StringComparison.OrdinalIgnoreCase)
                ? options.YandexBaseUrl
                : options.BaseUrl;

            if (Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }

            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        });
        services.AddHttpClient<IDeepSeekJsonClient, DeepSeekJsonClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<DeepSeekOptions>>().Value;
            if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }

            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
            }

            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddHttpClient<ITextEnrichmentClient, TextEnrichmentClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<TextEnrichmentOptions>>().Value;
            if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }

            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        });
        services.AddDbContextFactory<SuperChatDbContext>((serviceProvider, optionsBuilder) =>
        {
            var persistence = serviceProvider.GetRequiredService<IOptions<PersistenceOptions>>().Value;
            var connectionString = SuperChatDbContextConfiguration.ResolveConnectionString(configuration, persistence);
            SuperChatDbContextConfiguration.Configure(optionsBuilder, connectionString, persistence.Provider);
        });

        services.AddSingleton<IAuthFlowService, AuthFlowService>();
        services.AddSingleton<IApiSessionService, ApiSessionService>();
        services.AddSingleton<IPilotInviteAdminService, PilotInviteAdminService>();
        services.AddSingleton<IMatrixProvisioningService, MatrixProvisioningService>();
        services.AddSingleton<ITelegramConnectionService, TelegramConnectionService>();
        services.AddSingleton<IIntegrationConnectionService, IntegrationConnectionService>();
        services.AddSingleton<IRoomDisplayNameService, MatrixRoomDisplayNameService>();
        services.AddSingleton<IWorkerRuntimeMonitor, WorkerRuntimeMonitor>();
        services.AddSingleton<IncomingMessageFilter>();
        services.AddSingleton<IMessageNormalizationService, MessageNormalizationService>();
        services.AddSingleton<IChatTemplateCatalog, ChatTemplateCatalog>();
        services.AddSingleton<IChatTemplateHandler, TodayChatTemplateHandler>();
        services.AddSingleton<IChatTemplateHandler, WaitingChatTemplateHandler>();
        services.AddSingleton<IChatTemplateHandler, MeetingsChatTemplateHandler>();
        services.AddSingleton<IChatTemplateHandler, RecentChatTemplateHandler>();
        services.AddSingleton<IChatAnswerGenerationService, ChatAnswerGenerationService>();
        services.AddSingleton<IChunkBuilderService, ChunkBuilderService>();
        services.AddSingleton<IChunkIndexingService, ChunkIndexingService>();
        services.AddSingleton<IMeetingProjectionService, MeetingProjectionService>();
        services.AddSingleton<MeetingUpsertService>();
        services.AddSingleton<MeetingAutoResolutionService>();
        services.AddSingleton<MeetingLookupService>();
        services.AddSingleton<MeetingUpcomingQueryService>();
        services.AddSingleton<MeetingManualResolutionService>();
        services.AddSingleton<IMeetingService, MeetingService>();
        services.AddSingleton<WorkItemIngestionService>();
        services.AddSingleton<WorkItemAutoResolutionService>();
        services.AddSingleton<WorkItemLookupService>();
        services.AddSingleton<WorkItemQueryService>();
        services.AddSingleton<WorkItemManualResolutionService>();
        services.AddSingleton<IWorkItemService, WorkItemService>();
        services.AddSingleton<WorkItemStrategySnapshotProvider>();
        services.AddSingleton<IWorkItemTypeStrategy, RequestWorkItemTypeStrategy>();
        services.AddSingleton<IWorkItemTypeStrategy, EventWorkItemTypeStrategy>();
        services.AddSingleton<IWorkItemTypeStrategy, ActionItemWorkItemTypeStrategy>();
        services.AddSingleton<IWorkItemCatalogService, WorkItemCatalogService>();
        services.AddSingleton<IRequestWorkItemCommandService, RequestWorkItemCommandService>();
        services.AddSingleton<IEventWorkItemCommandService, EventWorkItemCommandService>();
        services.AddSingleton<IActionItemWorkItemCommandService, ActionItemWorkItemCommandService>();
        services.AddSingleton<IRetrievalService, RetrievalService>();
        services.AddSingleton<HeuristicStructuredExtractionService>();
        services.AddSingleton<DeepSeekStructuredExtractionService>();
        services.AddSingleton<IAiStructuredExtractionService, BootstrapStructuredExtractionService>();
        services.AddSingleton<IDigestService, DigestService>();
        services.AddSingleton<ISearchService, SearchService>();
        services.AddSingleton<IChatExperienceService, ChatExperienceService>();
        services.AddSingleton<IFeedbackService, FeedbackService>();
        services.AddSingleton<IHealthSnapshotService, HealthSnapshotService>();

        if (enableBackgroundWorkers)
        {
            services.AddHostedService<MatrixSyncBackgroundService>();
            services.AddHostedService<ChunkBuilderBackgroundService>();
            services.AddHostedService<ChunkIndexingBackgroundService>();
            services.AddHostedService<MeetingProjectionBackgroundService>();
            services.AddHostedService<ExtractionBackgroundService>();
        }

        services.AddHealthChecks().AddCheck<BootstrapHealthCheck>("bootstrap");

        return services;
    }

    public static IServiceCollection AddSuperChatQdrant(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<QdrantOptions>()
            .Bind(configuration.GetSection(QdrantOptions.SectionName));

        services.AddSingleton<IQdrantSdkClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<QdrantOptions>>().Value;
            if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                throw new InvalidOperationException("Qdrant base URL is not configured.");
            }

            var grpcUri = new UriBuilder(baseUri)
            {
                Port = options.GrpcPort > 0 ? options.GrpcPort : 6334
            }.Uri;

            var sdkClient = new QdrantSdk(
                grpcUri,
                string.IsNullOrWhiteSpace(options.ApiKey) ? string.Empty : options.ApiKey,
                TimeSpan.FromSeconds(30),
                serviceProvider.GetRequiredService<ILoggerFactory>());

            return new QdrantSdkClientAdapter(sdkClient);
        });
        services.AddSingleton<IQdrantClient, QdrantClient>();
        services.AddSingleton<QdrantInitializationService>();

        return services;
    }
}
