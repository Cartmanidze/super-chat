using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SuperChat.Contracts.Features.Operations;

namespace SuperChat.Infrastructure.Shared.Persistence;

public sealed class SuperChatDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SuperChatDbContext>
{
    public SuperChatDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var persistence = configuration.GetSection(PersistenceOptions.SectionName).Get<PersistenceOptions>() ?? new PersistenceOptions();
        var connectionString = SuperChatDbContextConfiguration.ResolveConnectionString(configuration, persistence);
        var optionsBuilder = new DbContextOptionsBuilder<SuperChatDbContext>();

        SuperChatDbContextConfiguration.Configure(optionsBuilder, connectionString, persistence.Provider);
        return new SuperChatDbContext(optionsBuilder.Options);
    }
}
