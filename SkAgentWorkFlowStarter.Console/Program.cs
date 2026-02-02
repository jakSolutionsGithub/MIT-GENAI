using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkAgentWorkFlowStarter.Console.Infrastructure.Sk;
using SkAgentWorkFlowStarter.Console.Samples;
using SkAgentWorkFlowStarter.Console.Samples.Services;



var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddSk();
builder.Services.AddAgents();
builder.Logging.AddConsole();

using var host = builder.Build();

//var triageService = host.Services.GetRequiredService<IIncidentTriageAgentService>();
//var result = await triageService.CallIncidentTriageAgent(CancellationToken.None);
//Console.WriteLine($"Incident triage agent response: {result.Summary}");

//var workflowService = host.Services.GetRequiredService<IWorkflowSampleService>();
//var workflowResult = await workflowService.RunStaticWorkflow(CancellationToken.None);
//Console.WriteLine($"Workflow '{workflowResult.Name}' steps:");
//foreach (var step in workflowResult.Steps)
//{
  //  Console.WriteLine($"- {step.StepName}: {step.Output}");
//}

var mobilityChat = host.Services.GetRequiredService<IMobilityChatService>();
await mobilityChat.RunInteractiveAsync(CancellationToken.None);

