using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Infrastructure.Composition;

namespace SuperChat.Tests;

public sealed class DeadRepositoryCleanupTests
{
    [Fact]
    public void LegacyRepositoryTypes_AreRemovedFromAssemblies()
    {
        var domainAssembly = typeof(ExtractedItem).Assembly;
        var infrastructureAssembly = typeof(ServiceCollectionExtensions).Assembly;

        AssertLegacyTypesAbsent(domainAssembly,
        [
            "SuperChat.Domain.Features.Auth.IAppUserRepository",
            "SuperChat.Domain.Features.Integrations.Matrix.IMatrixIdentityRepository",
            "SuperChat.Domain.Features.Integrations.Telegram.ITelegramConnectionRepository",
            "SuperChat.Domain.Features.Messaging.INormalizedMessageRepository"
        ]);
        AssertLegacyTypesAbsent(infrastructureAssembly,
        [
            "SuperChat.Infrastructure.Features.Auth.EfAppUserRepository",
            "SuperChat.Infrastructure.Features.Integrations.Matrix.EfMatrixIdentityRepository",
            "SuperChat.Infrastructure.Features.Integrations.Telegram.EfTelegramConnectionRepository",
            "SuperChat.Infrastructure.Features.Messaging.EfNormalizedMessageRepository"
        ]);
    }

    [Fact]
    public void Bootstrap_DoesNotRegisterLegacyRepositories()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();

        services.AddSuperChatBootstrap(
            configuration,
            enablePipelineScheduling: false,
            enablePipelineConsumers: false);

        Assert.DoesNotContain(
            services,
            descriptor => IsLegacyRepositoryType(descriptor.ServiceType.FullName));
        Assert.DoesNotContain(
            services,
            descriptor => IsLegacyRepositoryType(descriptor.ImplementationType?.FullName));
    }

    private static void AssertLegacyTypesAbsent(System.Reflection.Assembly assembly, IReadOnlyList<string> fullNames)
    {
        foreach (var fullName in fullNames)
        {
            Assert.Null(assembly.GetType(fullName, throwOnError: false, ignoreCase: false));
        }
    }

    private static bool IsLegacyRepositoryType(string? fullName)
    {
        return fullName is
            "SuperChat.Domain.Features.Auth.IAppUserRepository" or
            "SuperChat.Domain.Features.Integrations.Matrix.IMatrixIdentityRepository" or
            "SuperChat.Domain.Features.Integrations.Telegram.ITelegramConnectionRepository" or
            "SuperChat.Domain.Features.Messaging.INormalizedMessageRepository" or
            "SuperChat.Infrastructure.Features.Auth.EfAppUserRepository" or
            "SuperChat.Infrastructure.Features.Integrations.Matrix.EfMatrixIdentityRepository" or
            "SuperChat.Infrastructure.Features.Integrations.Telegram.EfTelegramConnectionRepository" or
            "SuperChat.Infrastructure.Features.Messaging.EfNormalizedMessageRepository";
    }
}
