using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using SuperChat.Contracts.Configuration;
using SuperChat.Infrastructure.Services;
using SuperChat.Infrastructure.Abstractions;
using SuperChat.Web.Localization;
using SuperChat.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = AppCultures.SupportedCultureInfos.ToList();

    options.DefaultRequestCulture = new RequestCulture(AppCultures.DefaultCultureName);
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders =
    [
        new QueryStringRequestCultureProvider
        {
            QueryStringKey = "lang",
            UIQueryStringKey = "lang"
        },
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});

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

var configuredAdminEmails = (builder.Configuration.GetSection(PilotOptions.SectionName).Get<PilotOptions>()?.AdminEmails ?? [])
    .Select(email => email.Trim())
    .Where(email => !string.IsNullOrWhiteSpace(email))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminAuthorizationPolicy.PolicyName, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {
            var userEmail = context.User.GetEmail();
            return configuredAdminEmails.Contains(userEmail, StringComparer.OrdinalIgnoreCase);
        });
    });
});

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
        options.Conventions.AuthorizeFolder("/Admin", AdminAuthorizationPolicy.PolicyName);
    })
    .AddViewLocalization();
builder.Services.AddSingleton<IUiTextService, UiTextService>();
builder.Services.AddSuperChatBootstrap(builder.Configuration);

var app = builder.Build();
var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseRequestLocalization(localizationOptions);
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/localization/set-language", (
    string culture,
    string? returnUrl,
    HttpContext httpContext) =>
{
    var resolvedCulture = AppCultures.IsSupported(culture)
        ? culture
        : AppCultures.DefaultCultureName;

    httpContext.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(resolvedCulture)),
        new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        });

    return Results.LocalRedirect(IsSafeLocalReturnUrl(returnUrl) ? returnUrl! : "/");
});

app.MapGet("/health", async (
    IHealthSnapshotService healthSnapshotService,
    IOptions<PilotOptions> pilotOptions,
    IOptions<DeepSeekOptions> deepSeekOptions,
    IOptions<TelegramBridgeOptions> telegramOptions,
    CancellationToken cancellationToken) =>
{
    var snapshot = await healthSnapshotService.GetAsync(cancellationToken);
    return Results.Json(new
    {
        status = "ok",
        demoMode = pilotOptions.Value.DevSeedSampleData,
        invitedUsers = snapshot.AllowedEmailCount,
        knownUsers = snapshot.KnownUserCount,
        pendingMessages = snapshot.PendingMessageCount,
        extractedItems = snapshot.ExtractedItemCount,
        aiModel = deepSeekOptions.Value.Model,
        bridgeBot = telegramOptions.Value.BotUserId
    });
});

app.MapRazorPages();
app.Run();

static bool IsSafeLocalReturnUrl(string? returnUrl)
{
    return !string.IsNullOrWhiteSpace(returnUrl) &&
           returnUrl.StartsWith("/", StringComparison.Ordinal) &&
           !returnUrl.StartsWith("//", StringComparison.Ordinal) &&
           !returnUrl.StartsWith("/\\", StringComparison.Ordinal);
}

public partial class Program;
