using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rebus.Config;
using Rebus.Transport.InMem;
using Rebus.Timeouts;
using SuperChat.Contracts.Features.Admin;
using SuperChat.Contracts.Features.Auth;
using SuperChat.Contracts.Features.Integrations;
using SuperChat.Contracts.Features.Integrations.Max;
using SuperChat.Contracts.Features.Integrations.Telegram;
using SuperChat.Contracts.Features.Intelligence.Extraction;
using SuperChat.Contracts.Features.Intelligence.Meetings;
using SuperChat.Contracts.Features.Intelligence.Retrieval;
using SuperChat.Contracts.Features.Messaging;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Contracts.Features.Search;
using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Auth;
using SuperChat.Domain.Features.Feedback;
using SuperChat.Domain.Features.Integrations.Telegram;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Domain.Features.Messaging;
using SuperChat.Application.Composition;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Diagnostics;
using SuperChat.Infrastructure.Features.Admin;
using SuperChat.Infrastructure.Features.Auth;
using SuperChat.Infrastructure.Features.Chat;
using SuperChat.Infrastructure.Features.Feedback;
using SuperChat.Infrastructure.Features.Integrations;
using SuperChat.Infrastructure.Features.Integrations.Max.Userbot;
using SuperChat.Infrastructure.Features.Integrations.Telegram;
using SuperChat.Infrastructure.Features.Integrations.Telegram.Userbot;
using SuperChat.Infrastructure.Features.Intelligence.Digest;
using SuperChat.Infrastructure.Features.Intelligence.Extraction;
using SuperChat.Infrastructure.Features.Intelligence.Meetings;
using SuperChat.Infrastructure.Features.Intelligence.Resolution;
using SuperChat.Infrastructure.Features.Intelligence.Retrieval;
using SuperChat.Infrastructure.Features.Intelligence.WorkItems;
using SuperChat.Infrastructure.Features.Messaging;
using SuperChat.Infrastructure.Features.Operations;
using SuperChat.Infrastructure.Features.Search;
using SuperChat.Infrastructure.Shared.Persistence;
using QdrantSdk = Qdrant.Client.QdrantClient;

namespace SuperChat.Infrastructure.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSuperChatBootstrap(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enablePipelineScheduling = true,
        bool enablePipelineConsumers = true)
    {
        SuperChatMetrics.Initialize();
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
            .AddOptions<MeetingProjectionOptions>()
            .Bind(configuration.GetSection(MeetingProjectionOptions.SectionName));
        services
            .AddOptions<IncomingMessageFilterOptions>()
            .Bind(configuration.GetSection(IncomingMessageFilterOptions.SectionName));
        services
            .AddOptions<PilotOptions>()
            .Bind(configuration.GetSection(PilotOptions.SectionName))
            .Validate(o => o.VerificationCodeMinutes > 0, "SuperChat:VerificationCodeMinutes must be positive.")
            .Validate(o => o.MaxVerificationAttempts > 0, "SuperChat:MaxVerificationAttempts must be positive.")
            .Validate(o => o.ApiSessionDays > 0, "SuperChat:ApiSessionDays must be positive.")
            .ValidateOnStart();
        services
            .AddOptions<RetrievalOptions>()
            .Bind(configuration.GetSection(RetrievalOptions.SectionName));
        services
            .AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection(PersistenceOptions.SectionName));
        services
            .AddOptions<PipelineMessagingOptions>()
            .Bind(configuration.GetSection(PipelineMessagingOptions.SectionName));
        services
            .AddOptions<ResolutionOptions>()
            .Bind(configuration.GetSection(ResolutionOptions.SectionName));
        services
            .AddOptions<TelegramUserbotOptions>()
            .Bind(configuration.GetSection(TelegramUserbotOptions.SectionName));
        services
            .AddOptions<MaxUserbotOptions>()
            .Bind(configuration.GetSection(MaxUserbotOptions.SectionName));

        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PilotOptions>>().Value);
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
        services.AddHttpClient<TelegramUserbotClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<TelegramUserbotOptions>>().Value;
            if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }

            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        });
        services.AddHttpClient<MaxUserbotClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MaxUserbotOptions>>().Value;
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

        services.AddSuperChatApplication();

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<EmailOptions>>().Value);
        services.AddSingleton<IVerificationCodeSender, SmtpVerificationCodeSender>();
        services.AddSingleton<IAuthFlowService, AuthFlowService>();
        services.AddSingleton<IApiSessionService, ApiSessionService>();
        services.AddSingleton<IAdminPasswordService, AdminPasswordService>();
        services.AddSingleton<IPilotInviteAdminService, PilotInviteAdminService>();
        services.AddSingleton<ITelegramConnectionService, TelegramConnectionService>();
        services.AddSingleton<IIntegrationConnectionService, IntegrationConnectionService>();
        services.AddSingleton<IChatTitleService, ChatMessageChatTitleService>();
        services.AddSingleton<IncomingMessageFilter>();
        services.AddSingleton<IChatMessageStore, ChatMessageStore>();
        services.AddSingleton<IChunkBuilderService, ChunkBuilderService>();
        services.AddSingleton<IChunkIndexingService, ChunkIndexingService>();
        services.AddSingleton<IMeetingProjectionService, MeetingProjectionService>();
        services.AddSingleton<MeetingUpsertService>();
        services.AddSingleton<MeetingAutoResolutionService>();
        services.AddSingleton<IMeetingService, MeetingService>();
        services.AddSingleton<ConversationResolutionService>();
        services.AddSingleton<WorkItemWriter>();
        services.AddSingleton<IWorkItemService, WorkItemService>();
        services.AddSingleton<IRetrievalService, RetrievalService>();
        services.AddSingleton<IUserTimeZoneResolver, AppUserTimeZoneResolver>();
        services.AddSingleton<HeuristicStructuredExtractionService>();
        services.AddSingleton<DeepSeekStructuredExtractionService>();
        services.AddSingleton<IAiStructuredExtractionService, BootstrapStructuredExtractionService>();
        services.AddSingleton<IAiResolutionService, DeepSeekResolutionService>();
        services.AddSingleton<IDigestService, DigestService>();
        services.AddSingleton<ISearchService, SearchService>();

        // Repositories
        services.AddSingleton<IWorkItemRepository, EfWorkItemRepository>();
        services.AddSingleton<IMeetingRepository, EfMeetingRepository>();
        services.AddSingleton<IFeedbackEventRepository, EfFeedbackEventRepository>();

        var pipelineMessagingOptions = configuration.GetSection(PipelineMessagingOptions.SectionName).Get<PipelineMessagingOptions>() ?? new PipelineMessagingOptions();
        var persistenceOptions = configuration.GetSection(PersistenceOptions.SectionName).Get<PersistenceOptions>() ?? new PersistenceOptions();
        var usePostgresTransport = string.Equals(persistenceOptions.Provider, "Postgres", StringComparison.OrdinalIgnoreCase);
        var effectivePipelineConsumers = enablePipelineConsumers || (!usePostgresTransport && enablePipelineScheduling);
        var canUseRebusPipeline = pipelineMessagingOptions.Enabled && (enablePipelineScheduling || effectivePipelineConsumers);
        if (canUseRebusPipeline)
        {
            if (!usePostgresTransport)
            {
                services.AddSingleton<InMemNetwork>();
            }

            services.AddRebus(
                (configurer, serviceProvider) =>
                {
                    if (effectivePipelineConsumers)
                    {
                        if (usePostgresTransport)
                        {
                            var connectionString = SuperChatDbContextConfiguration.ResolveConnectionString(configuration, persistenceOptions);
                            configurer.Transport(transport => transport.UsePostgreSql(
                                connectionString,
                                pipelineMessagingOptions.TransportTableName,
                                pipelineMessagingOptions.InputQueueName,
                                expiredMessagesCleanupInterval: null,
                                schemaName: null));
                        }
                        else
                        {
                            configurer.Transport(transport => transport.UseInMemoryTransport(
                                serviceProvider.GetRequiredService<InMemNetwork>(),
                                pipelineMessagingOptions.InputQueueName));
                        }

                        configurer.Options(options =>
                        {
                            options.SetNumberOfWorkers(Math.Max(1, pipelineMessagingOptions.Workers));
                            options.SetMaxParallelism(Math.Max(1, pipelineMessagingOptions.MaxParallelism));
                        });
                    }
                    else if (usePostgresTransport)
                    {
                        var connectionString = SuperChatDbContextConfiguration.ResolveConnectionString(configuration, persistenceOptions);
                        configurer.Transport(transport => transport.UsePostgreSqlAsOneWayClient(
                            connectionString,
                            pipelineMessagingOptions.TransportTableName));
                        configurer.Timeouts(timeouts => timeouts.UseExternalTimeoutManager(
                            pipelineMessagingOptions.InputQueueName));
                    }
                    else
                    {
                        configurer.Transport(transport => transport.UseInMemoryTransportAsOneWayClient(
                            serviceProvider.GetRequiredService<InMemNetwork>()));
                    }

                    return configurer;
                },
                isDefaultBus: true);
            if (effectivePipelineConsumers)
            {
                services.AutoRegisterHandlersFromAssemblyOf<ProcessConversationAfterSettleCommandHandler>();
                if (usePostgresTransport)
                {
                    services.AddHostedService<DueMeetingsSweepBackgroundService>();
                }
            }

            if (enablePipelineScheduling)
            {
                if (effectivePipelineConsumers)
                {
                    if (usePostgresTransport)
                    {
                        services.AddSingleton<IPipelineCommandScheduler, RebusPipelineCommandScheduler>();
                    }
                    else
                    {
                        services.AddSingleton<IPipelineCommandScheduler, NonTransactionalRebusPipelineCommandScheduler>();
                    }
                }
                else
                {
                    services.AddSingleton<IPipelineCommandScheduler, OneWayClientPipelineCommandScheduler>();
                }
            }
            else
            {
                services.AddSingleton<IPipelineCommandScheduler, NoOpPipelineCommandScheduler>();
            }
        }
        else
        {
            services.AddSingleton<IPipelineCommandScheduler, NoOpPipelineCommandScheduler>();
        }

        services.AddHealthChecks()
            .AddCheck<BootstrapHealthCheck>("bootstrap");

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
