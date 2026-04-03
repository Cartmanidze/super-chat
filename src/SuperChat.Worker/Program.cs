using SuperChat.Infrastructure.Composition;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSuperChatBootstrap(
    builder.Configuration,
    enableMatrixSyncWorker: false,
    enablePipelineScheduling: false,
    enablePipelineConsumers: true);

var host = builder.Build();
await host.RunAsync();
