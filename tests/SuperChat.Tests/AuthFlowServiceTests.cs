using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Services;
using SuperChat.Infrastructure.State;

namespace SuperChat.Tests;

public sealed class AuthFlowServiceTests
{
    [Fact]
    public async Task RequestMagicLink_RejectsEmailOutsidePilotList()
    {
        var options = new PilotOptions
        {
            AllowedEmails = ["pilot@example.com"],
            BaseUrl = "https://localhost:8080",
            MagicLinkMinutes = 15
        };
        var store = new SuperChatStore(options);
        var matrix = new InMemoryMatrixProvisioningService(store, new MatrixOptions(), TimeProvider.System);
        var service = new InMemoryAuthFlowService(store, matrix, options, TimeProvider.System);

        var result = await service.RequestMagicLinkAsync("blocked@example.com", CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Null(result.DevelopmentLink);
    }

    [Fact]
    public async Task Verify_CreatesUserAndMatrixIdentity()
    {
        var options = new PilotOptions
        {
            AllowedEmails = ["pilot@example.com"],
            BaseUrl = "https://localhost:8080",
            MagicLinkMinutes = 15
        };
        var store = new SuperChatStore(options);
        var matrix = new InMemoryMatrixProvisioningService(store, new MatrixOptions { UserIdPrefix = "superchat" }, TimeProvider.System);
        var service = new InMemoryAuthFlowService(store, matrix, options, TimeProvider.System);

        var linkResult = await service.RequestMagicLinkAsync("pilot@example.com", CancellationToken.None);
        var token = linkResult.DevelopmentLink!.Query.Split("token=", StringSplitOptions.TrimEntries)[1];

        var result = await service.VerifyAsync(token, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.NotNull(result.User);
        Assert.StartsWith("@superchat-pilot", store.GetMatrixIdentity(result.User!.Id)!.MatrixUserId, StringComparison.Ordinal);
    }
}
