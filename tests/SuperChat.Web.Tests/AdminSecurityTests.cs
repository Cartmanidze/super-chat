using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Contracts.Features.Admin;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Web.Pages.Admin;
using SuperChat.Web.Security;

namespace SuperChat.Web.Tests;

public sealed class AdminSecurityTests : IClassFixture<WebTestApplicationFactory>
{
    private readonly WebTestApplicationFactory _factory;

    public AdminSecurityTests(WebTestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void HasAdminAccess_ReturnsTrueOnlyForConfiguredEmailWithUnlockClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Email, "admin@example.com"),
            new Claim(AdminClaimTypes.AdminUnlocked, AdminClaimTypes.TrueValue)
        ], "test"));

        var result = principal.HasAdminAccess(new PilotOptions
        {
            AdminEmails = ["admin@example.com"]
        });

        Assert.True(result);
    }

    [Fact]
    public void HasAdminAccess_ReturnsFalseWithoutUnlockClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Email, "admin@example.com")
        ], "test"));

        var result = principal.HasAdminAccess(new PilotOptions
        {
            AdminEmails = ["admin@example.com"]
        });

        Assert.False(result);
    }

    [Fact]
    public void AdminPasswordHasher_VerifyAcceptsMatchingPassword()
    {
        var hash = AdminPasswordHasher.Hash("super-secret");

        Assert.True(AdminPasswordHasher.Verify("super-secret", hash));
        Assert.False(AdminPasswordHasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void AdminPasswordService_VerifyAcceptsBase64WrappedHash()
    {
        var hash = AdminPasswordHasher.Hash("super-secret");
        var wrapped = "base64:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(hash));
        var service = new AdminPasswordService(Options.Create(new PilotOptions
        {
            AdminPasswordHash = wrapped
        }));

        Assert.True(service.IsConfigured);
        Assert.True(service.Verify("super-secret"));
        Assert.False(service.Verify("wrong-password"));
    }

    [Fact]
    public void AdminPasswordService_TreatsInvalidBase64PayloadAsUnconfigured()
    {
        var service = new AdminPasswordService(Options.Create(new PilotOptions
        {
            AdminPasswordHash = "base64:not-valid-base64"
        }));

        Assert.False(service.IsConfigured);
        Assert.False(service.Verify("super-secret"));
    }

    [Fact]
    public async Task AdminIndex_RedirectsConfiguredAdminWithoutUnlockedSessionToUnlockPage()
    {
        var model = CreateIndexModel(new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Email, "admin@example.com")
        ], "test")));

        var result = await model.OnGetAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Admin/Unlock", redirect.PageName);
        Assert.Equal("/admin", redirect.RouteValues!["returnUrl"]);
    }

    [Fact]
    public async Task AdminPage_RedirectsAnonymousUserToMagicLinkRequest()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/auth/request-link", response.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    private static IndexModel CreateIndexModel(ClaimsPrincipal user)
    {
        var model = new IndexModel(
            new FakePilotInviteAdminService(),
            new FakeWorkerRuntimeMonitor(),
            Options.Create(new PilotOptions
            {
                AdminEmails = ["admin@example.com"]
            }))
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user
                }
            }
        };

        return model;
    }

    private sealed class FakePilotInviteAdminService : IPilotInviteAdminService
    {
        public Task<IReadOnlyList<AdminInviteViewModel>> GetInvitesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<AdminInviteViewModel>>([]);
        }

        public Task<AdminInviteMutationResult> AddInviteAsync(string email, string invitedBy, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AdminInviteMutationResult(true, "ok"));
        }
    }

    private sealed class FakeWorkerRuntimeMonitor : IWorkerRuntimeMonitor
    {
        public void RegisterWorker(string key, string displayName)
        {
        }

        public void MarkRunning(string key, string displayName, string? details = null)
        {
        }

        public void MarkSucceeded(string key, string displayName, string? details = null)
        {
        }

        public void MarkFailed(string key, string displayName, Exception exception, string? details = null)
        {
        }

        public void MarkDisabled(string key, string displayName, string? details = null)
        {
        }

        public IReadOnlyList<WorkerRuntimeStatusViewModel> GetStatuses()
        {
            return [];
        }
    }
}
