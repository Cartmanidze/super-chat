using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SuperChat.Contracts.Features.Operations;

namespace SuperChat.Infrastructure.Shared.Persistence;

public static class SuperChatDbContextConfiguration
{
    private const string DefaultConnectionString = "Host=localhost;Port=5432;Database=superchat_app;Username=postgres;Password=postgres";

    public static string ResolveConnectionString(IConfiguration configuration, PersistenceOptions persistence)
    {
        var connectionString = configuration.GetConnectionString("SuperChatDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = persistence.ConnectionString;
        }

        if (string.IsNullOrWhiteSpace(connectionString) && persistence.Provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            connectionString = $"Data Source={persistence.DatabaseName}.db";
        }

        return string.IsNullOrWhiteSpace(connectionString)
            ? DefaultConnectionString
            : connectionString;
    }

    public static void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString, string provider)
    {
        if (ShouldUseSqlite(connectionString, provider))
        {
            optionsBuilder.UseSqlite(connectionString, sqlite => sqlite.MigrationsAssembly(typeof(SuperChatDbContext).Assembly.GetName().Name));
            return;
        }

        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(SuperChatDbContext).Assembly.GetName().Name));
    }

    public static bool ShouldUseSqlite(string connectionString, string provider)
    {
        return provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
               connectionString.EndsWith(".db", StringComparison.OrdinalIgnoreCase);
    }
}
