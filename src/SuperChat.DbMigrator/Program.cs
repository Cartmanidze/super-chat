using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SuperChat.DbMigrator;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Persistence;
using SuperChat.Infrastructure.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});
builder.Services
    .AddOptions<PersistenceOptions>()
    .Bind(builder.Configuration.GetSection(PersistenceOptions.SectionName));
builder.Services.AddSuperChatQdrant(builder.Configuration);
builder.Services.AddDbContextFactory<SuperChatDbContext>((serviceProvider, optionsBuilder) =>
{
    var persistence = serviceProvider.GetRequiredService<IOptions<PersistenceOptions>>().Value;
    var connectionString = SuperChatDbContextConfiguration.ResolveConnectionString(builder.Configuration, persistence);
    SuperChatDbContextConfiguration.Configure(optionsBuilder, connectionString, persistence.Provider);
});

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var logger = scope.ServiceProvider
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger("SuperChat.DbMigrator");
var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SuperChatDbContext>>();
var qdrantOptions = scope.ServiceProvider.GetRequiredService<IOptions<QdrantOptions>>().Value;
var cancellationToken = scope.ServiceProvider.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
await LegacyDatabaseMigrationBootstrapper.PrepareAsync(dbContext, logger, cancellationToken);

var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
if (pendingMigrations.Count == 0)
{
    logger.LogInformation("No pending EF Core migrations.");
}
else
{
    logger.LogInformation(
        "Applying {MigrationCount} pending EF Core migrations: {MigrationList}",
        pendingMigrations.Count,
        string.Join(", ", pendingMigrations));

    await dbContext.Database.MigrateAsync(cancellationToken);
    logger.LogInformation("Database migrations completed successfully.");
}

await QdrantBootstrapRunner.EnsureInitializedAsync(scope.ServiceProvider, qdrantOptions, logger, cancellationToken);
