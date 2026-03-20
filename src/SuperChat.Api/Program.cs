using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using SuperChat.Api.Features.Auth;
using SuperChat.Api.Features.Chat;
using SuperChat.Api.Features.Feedback;
using SuperChat.Api.Features.Health;
using SuperChat.Api.Features.Integrations;
using SuperChat.Api.Features.Integrations.Telegram;
using SuperChat.Api.Features.Me;
using SuperChat.Api.Features.Search;
using SuperChat.Api.Features.WorkItems;
using SuperChat.Infrastructure.Composition;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services
    .AddAuthentication(ApiSessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiSessionAuthenticationHandler>(
        ApiSessionAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization();
builder.Services.AddValidatorsFromAssemblyContaining<SuperChat.Api.Program>();
builder.Services.AddSuperChatBootstrap(builder.Configuration, enableBackgroundWorkers: false);

var app = builder.Build();

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
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

app.Run();

namespace SuperChat.Api
{
    public partial class Program;
}
