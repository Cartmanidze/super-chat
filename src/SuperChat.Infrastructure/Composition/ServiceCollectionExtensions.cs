using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Infrastructure.Health;
using SuperChat.Infrastructure.HostedServices;
using SuperChat.Infrastructure.State;

namespace SuperChat.Infrastructure.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSuperChatBootstrap(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<DeepSeekOptions>()
            .Bind(configuration.GetSection(DeepSeekOptions.SectionName));
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
            .AddOptions<TelegramBridgeOptions>()
            .Bind(configuration.GetSection(TelegramBridgeOptions.SectionName));

        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PilotOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MatrixOptions>>().Value);
        services.AddSingleton<SuperChatStore>();
        services.AddSingleton<IAuthFlowService, InMemoryAuthFlowService>();
        services.AddSingleton<IApiSessionService, InMemoryApiSessionService>();
        services.AddSingleton<IMatrixProvisioningService, InMemoryMatrixProvisioningService>();
        services.AddSingleton<ITelegramConnectionService, BootstrapTelegramConnectionService>();
        services.AddSingleton<IMessageNormalizationService, MessageNormalizationService>();
        services.AddSingleton<HeuristicStructuredExtractionService>();
        services.AddSingleton<DeepSeekStructuredExtractionService>();
        services.AddSingleton<IAiStructuredExtractionService, BootstrapStructuredExtractionService>();
        services.AddSingleton<IDigestService, DigestService>();
        services.AddSingleton<ISearchService, SearchService>();
        services.AddSingleton<IFeedbackService, FeedbackService>();
        services.AddHostedService<MatrixSyncBackgroundService>();
        services.AddHostedService<ExtractionBackgroundService>();
        services.AddHealthChecks().AddCheck<BootstrapHealthCheck>("bootstrap");

        return services;
    }
}
