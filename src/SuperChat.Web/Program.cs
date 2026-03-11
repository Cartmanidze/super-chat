using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Services;
using SuperChat.Infrastructure.State;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/request-link";
        options.LogoutPath = "/auth/sign-out";
        options.AccessDeniedPath = "/auth/request-link";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();
builder.Services
    .AddRazorPages(options =>
    {
        options.Conventions.AllowAnonymousToPage("/Index");
        options.Conventions.AllowAnonymousToPage("/Error");
        options.Conventions.AllowAnonymousToPage("/Privacy");
        options.Conventions.AllowAnonymousToFolder("/Auth");
        options.Conventions.AuthorizePage("/Connect/Telegram");
        options.Conventions.AuthorizeFolder("/Dashboard");
        options.Conventions.AuthorizeFolder("/Search");
        options.Conventions.AuthorizeFolder("/Feedback");
        options.Conventions.AuthorizeFolder("/Settings");
    });
builder.Services.AddSuperChatBootstrap(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", (
    SuperChatStore store,
    IOptions<PilotOptions> pilotOptions,
    IOptions<DeepSeekOptions> deepSeekOptions,
    IOptions<TelegramBridgeOptions> telegramOptions) =>
{
    return Results.Json(new
    {
        status = "ok",
        demoMode = pilotOptions.Value.DevSeedSampleData,
        invitedUsers = store.AllowedEmailCount,
        knownUsers = store.KnownUserCount,
        pendingMessages = store.PendingMessageCount,
        extractedItems = store.ExtractedItemCount,
        aiModel = deepSeekOptions.Value.Model,
        bridgeBot = telegramOptions.Value.BotUserId
    });
});

app.MapRazorPages();
app.Run();

public partial class Program;
