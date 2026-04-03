using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Contracts.Features.Integrations;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Auth;
using SuperChat.Domain.Features.Integrations;
using SuperChat.Web.Pages;

namespace SuperChat.Web.Tests;

public sealed class TodayPageActionTests
{
    [Fact]
    public async Task OnPostCompleteAsync_CompletesMeetingCard_AndRedirectsBackToToday()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var meetingCommands = new FakeMeetingWorkItemCommandService();
        var model = CreateModel(userId, meetingCommands);

        var result = await model.OnPostCompleteAsync(itemId, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Today", redirect.PageName);
        Assert.Equal((userId, itemId), meetingCommands.Completed.Single());
    }

    [Fact]
    public async Task OnPostDismissAsync_DismissesMeetingCard_AndRedirectsBackToToday()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var meetingCommands = new FakeMeetingWorkItemCommandService();
        var model = CreateModel(userId, meetingCommands);

        var result = await model.OnPostDismissAsync(itemId, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Today", redirect.PageName);
        Assert.Equal((userId, itemId), meetingCommands.Dismissed.Single());
    }

    private static TodayModel CreateModel(Guid userId, IMeetingWorkItemCommandService meetingCommands)
    {
        return new TodayModel(
            new FakeDigestService(),
            new FakeIntegrationConnectionService(),
            meetingCommands)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Email, "user@example.com")
                    ], "test"))
                }
            }
        };
    }

    private sealed class FakeDigestService : IDigestService
    {
        public Task<IReadOnlyList<WorkItemCardViewModel>> GetMeetingsAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<WorkItemCardViewModel>>([]);
        }
    }

    private sealed class FakeIntegrationConnectionService : IIntegrationConnectionService
    {
        public Task<IReadOnlyList<IntegrationConnection>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<IntegrationConnection>>([]);
        }

        public Task<IntegrationConnection> GetStatusAsync(Guid userId, IntegrationProvider provider, CancellationToken cancellationToken)
        {
            return Task.FromResult(new IntegrationConnection(
                userId,
                provider,
                IntegrationTransport.MatrixBridge,
                IntegrationConnectionState.Connected,
                null,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow));
        }

        public Task<IntegrationConnection> StartAsync(AppUser user, IntegrationProvider provider, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IntegrationConnection> ReconnectAsync(AppUser user, IntegrationProvider provider, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IntegrationConnection> StartChatLoginAsync(AppUser user, IntegrationProvider provider, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IntegrationConnection> SubmitLoginInputAsync(AppUser user, IntegrationProvider provider, string input, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task DisconnectAsync(Guid userId, IntegrationProvider provider, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<IntegrationConnection>> GetReadyForDevelopmentSyncAsync(IntegrationProvider provider, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<IntegrationConnection>>([]);
        }

        public Task MarkSynchronizedAsync(Guid userId, IntegrationProvider provider, DateTimeOffset synchronizedAt, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMeetingWorkItemCommandService : IMeetingWorkItemCommandService
    {
        public List<(Guid UserId, Guid ItemId)> Completed { get; } = [];

        public List<(Guid UserId, Guid ItemId)> Dismissed { get; } = [];

        public Task<bool> CompleteAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
        {
            Completed.Add((userId, meetingId));
            return Task.FromResult(true);
        }

        public Task<bool> DismissAsync(Guid userId, Guid meetingId, CancellationToken cancellationToken)
        {
            Dismissed.Add((userId, meetingId));
            return Task.FromResult(true);
        }
    }
}
