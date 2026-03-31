using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SuperChat.Contracts.Features.Integrations;
using SuperChat.Contracts.Features.Operations;
using SuperChat.Contracts.Features.WorkItems;
using SuperChat.Domain.Features.Auth;
using SuperChat.Domain.Features.Integrations;
using SuperChat.Domain.Features.Intelligence;
using SuperChat.Web.Pages;

namespace SuperChat.Web.Tests;

public sealed class TodayPageActionTests
{
    [Fact]
    public async Task OnPostCompleteAsync_CompletesRequestCard_AndRedirectsBackToSelectedSection()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var requestCommands = new FakeRequestWorkItemCommandService();
        var model = CreateModel(userId, requestCommands: requestCommands);

        var result = await model.OnPostCompleteAsync(itemId, WorkItemType.Request, "waiting", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Today", redirect.PageName);
        Assert.Equal("waiting", redirect.RouteValues!["section"]);
        Assert.Equal((userId, itemId), requestCommands.Completed.Single());
    }

    [Fact]
    public async Task OnPostDismissAsync_DismissesActionItemCard_AndRedirectsBackToSelectedSection()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var actionCommands = new FakeActionItemWorkItemCommandService();
        var model = CreateModel(userId, actionItemCommands: actionCommands);

        var result = await model.OnPostDismissAsync(itemId, WorkItemType.ActionItem, "today", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Today", redirect.PageName);
        Assert.Equal("today", redirect.RouteValues!["section"]);
        Assert.Equal((userId, itemId), actionCommands.Dismissed.Single());
    }

    [Fact]
    public async Task OnPostCompleteAsync_CompletesEventCard_AndRedirectsBackToSelectedSection()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var eventCommands = new FakeEventWorkItemCommandService();
        var model = CreateModel(userId, eventCommands: eventCommands);

        var result = await model.OnPostCompleteAsync(itemId, WorkItemType.Event, "meetings", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Today", redirect.PageName);
        Assert.Equal("meetings", redirect.RouteValues!["section"]);
        Assert.Equal((userId, itemId), eventCommands.Completed.Single());
    }

    private static TodayModel CreateModel(
        Guid userId,
        IRequestWorkItemCommandService? requestCommands = null,
        IActionItemWorkItemCommandService? actionItemCommands = null,
        IEventWorkItemCommandService? eventCommands = null)
    {
        var model = new TodayModel(
            new FakeDigestService(),
            new FakeWorkItemService(),
            new FakeIntegrationConnectionService(),
            requestCommands ?? new FakeRequestWorkItemCommandService(),
            actionItemCommands ?? new FakeActionItemWorkItemCommandService(),
            eventCommands ?? new FakeEventWorkItemCommandService(),
            TimeProvider.System)
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

        return model;
    }

    private sealed class FakeDigestService : IDigestService
    {
        public Task<IReadOnlyList<WorkItemCardViewModel>> GetTodayAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<WorkItemCardViewModel>>([]);
        }

        public Task<IReadOnlyList<WorkItemCardViewModel>> GetWaitingAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<WorkItemCardViewModel>>([]);
        }

        public Task<IReadOnlyList<WorkItemCardViewModel>> GetMeetingsAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<WorkItemCardViewModel>>([]);
        }
    }

    private sealed class FakeWorkItemService : IWorkItemService
    {
        public Task IngestRangeAsync(IEnumerable<ExtractedItem> items, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WorkItemRecord>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<WorkItemRecord>>([]);
        }

        public Task<IReadOnlyList<WorkItemRecord>> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<WorkItemRecord>>([]);
        }

        public Task<bool> CompleteAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<bool> DismissAsync(Guid userId, Guid workItemId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
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

    private sealed class FakeRequestWorkItemCommandService : IRequestWorkItemCommandService
    {
        public List<(Guid UserId, Guid ItemId)> Completed { get; } = [];

        public List<(Guid UserId, Guid ItemId)> Dismissed { get; } = [];

        public Task<bool> CompleteAsync(Guid userId, Guid requestId, CancellationToken cancellationToken)
        {
            Completed.Add((userId, requestId));
            return Task.FromResult(true);
        }

        public Task<bool> DismissAsync(Guid userId, Guid requestId, CancellationToken cancellationToken)
        {
            Dismissed.Add((userId, requestId));
            return Task.FromResult(true);
        }
    }

    private sealed class FakeActionItemWorkItemCommandService : IActionItemWorkItemCommandService
    {
        public List<(Guid UserId, Guid ItemId)> Completed { get; } = [];

        public List<(Guid UserId, Guid ItemId)> Dismissed { get; } = [];

        public Task<bool> CompleteAsync(Guid userId, Guid actionItemId, CancellationToken cancellationToken)
        {
            Completed.Add((userId, actionItemId));
            return Task.FromResult(true);
        }

        public Task<bool> DismissAsync(Guid userId, Guid actionItemId, CancellationToken cancellationToken)
        {
            Dismissed.Add((userId, actionItemId));
            return Task.FromResult(true);
        }
    }

    private sealed class FakeEventWorkItemCommandService : IEventWorkItemCommandService
    {
        public List<(Guid UserId, Guid ItemId)> Completed { get; } = [];

        public List<(Guid UserId, Guid ItemId)> Dismissed { get; } = [];

        public Task<bool> CompleteAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
        {
            Completed.Add((userId, eventId));
            return Task.FromResult(true);
        }

        public Task<bool> DismissAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
        {
            Dismissed.Add((userId, eventId));
            return Task.FromResult(true);
        }
    }
}
