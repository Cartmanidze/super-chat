using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Prometheus;
using SuperChat.Api.Features.Auth;
using SuperChat.Api.Features.Admin;
using SuperChat.Api.Features.Chat;
using SuperChat.Api.Features.Documentation;
using SuperChat.Api.Features.Feedback;
using SuperChat.Api.Features.Health;
using SuperChat.Api.Features.Integrations;
using SuperChat.Api.Features.Integrations.Telegram;
using SuperChat.Api.Features.Me;
using SuperChat.Api.Features.Search;
using SuperChat.Api.Features.WorkItems;
using SuperChat.Infrastructure.Composition;
using SuperChat.Infrastructure.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.AddSuperChatStructuredLogging("superchat-api");

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SuperChat.Api.Security.InvalidSessionExceptionHandler>();
builder.Services
    .AddAuthentication(ApiSessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiSessionAuthenticationHandler>(
        ApiSessionAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization();
builder.Services.AddValidatorsFromAssemblyContaining<SuperChat.Api.Program>();
builder.Services.AddApiDocumentation();
builder.Services.AddSuperChatBootstrap(
    builder.Configuration,
    enableMatrixSyncWorker: false,
    enablePipelineScheduling: false,
    enablePipelineConsumers: false);

var app = builder.Build();

app.UseExceptionHandler();
app.UseSuperChatRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.MapApiDocumentation();
app.UseHttpMetrics();
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api/v1");
api.MapHealthEndpoints();
api.MapAuthEndpoints();
api.MapMeEndpoints();
api.MapIntegrationEndpoints();
api.MapTelegramEndpoints();
api.MapChatEndpoints();
api.MapWorkItemEndpoints();
api.MapSearchEndpoints();
api.MapFeedbackEndpoints();
api.MapAdminEndpoints();

app.MapMetrics().ExcludeFromDescription();
app.Run();

namespace SuperChat.Api
{
    public partial class Program;
}
