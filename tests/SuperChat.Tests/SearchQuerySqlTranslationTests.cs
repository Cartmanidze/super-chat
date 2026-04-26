using Microsoft.EntityFrameworkCore;
using SuperChat.Infrastructure.Features.Intelligence.WorkItems;
using SuperChat.Infrastructure.Features.Messaging;
using SuperChat.Infrastructure.Shared.Persistence;

namespace SuperChat.Tests;

public sealed class SearchQuerySqlTranslationTests
{
    [Fact]
    public void WorkItemSearchQuery_TranslatesToPostgreSqlLikeWithLowerAndEscape()
    {
        using var dbContext = CreateNpgsqlDbContext();

        var queryable = dbContext.WorkItems
            .AsNoTracking()
            .ApplySearchFilter(Guid.NewGuid(), "contract")
            .Take(20);

        var sql = queryable.ToQueryString();

        Assert.Contains("LIKE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ESCAPE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LOWER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ILIKE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkItemSearchQuery_FiltersOutFollowUpCandidateAtSqlLevel()
    {
        using var dbContext = CreateNpgsqlDbContext();

        var sql = dbContext.WorkItems
            .AsNoTracking()
            .ApplySearchFilter(Guid.NewGuid(), "contract")
            .ToQueryString();

        Assert.Contains("Follow-up candidate", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizedMessageSearchQuery_TranslatesToPostgreSqlLikeWithLowerAndEscape()
    {
        using var dbContext = CreateNpgsqlDbContext();

        var queryable = dbContext.NormalizedMessages
            .AsNoTracking()
            .ApplySearchFilter(Guid.NewGuid(), "contract")
            .Take(20);

        var sql = queryable.ToQueryString();

        Assert.Contains("LIKE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ESCAPE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LOWER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkItemSearchQuery_PassesEscapedLikePatternAsParameter()
    {
        using var dbContext = CreateNpgsqlDbContext();

        var sql = dbContext.WorkItems
            .AsNoTracking()
            .ApplySearchFilter(Guid.NewGuid(), "50%")
            .ToQueryString();

        Assert.Contains("50\\%", sql, StringComparison.Ordinal);
    }

    private static SuperChatDbContext CreateNpgsqlDbContext()
    {
        var options = new DbContextOptionsBuilder<SuperChatDbContext>()
            .UseNpgsql("Host=ignored;Database=ignored;Username=ignored;Password=ignored")
            .Options;

        return new SuperChatDbContext(options);
    }
}
