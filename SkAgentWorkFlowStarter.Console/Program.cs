using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkAgentWorkFlowStarter.Console.Infrastructure.Sk;
using SkAgentWorkFlowStarter.Console.Samples;
using SkAgentWorkFlowStarter.Console.Samples.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables();


builder.Services.AddSk();
builder.Services.AddAgents();

builder.Logging.AddConsole();



using var host = builder.Build();

var mobilityChat = host.Services.GetRequiredService<IMobilityChatServiceClean>();
await mobilityChat.RunInteractiveAsync(CancellationToken.None);