using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.IncidentTriageAgent.Tools;
using SkAgentWorkFlowStarter.Console.Samples.Workflow.Tools;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgent.Tools;

namespace SkAgentWorkFlowStarter.Console.Infrastructure.Sk;

public static class SkConfiguration
{
    public static IServiceCollection AddSk(this IServiceCollection services)
    {
        services.AddSingleton<KernelPluginCollection>(serviceProvider =>
        {
            var jsonOptions = new JsonSerializerOptions();
            jsonOptions.Converters.Add(new JsonStringEnumConverter());

            KernelPluginCollection pluginCollection = [];

            pluginCollection.AddFromType<IncidentTriageTools>(
                serviceProvider: serviceProvider,
                pluginName: nameof(IncidentTriageTools),
                jsonSerializerOptions: jsonOptions);

            pluginCollection.AddFromType<WorkflowTools>(
                serviceProvider: serviceProvider,
                pluginName: nameof(WorkflowTools),
                jsonSerializerOptions: jsonOptions);

            pluginCollection.AddFromType<MobilityFormTools>(
                serviceProvider: serviceProvider,
                pluginName: nameof(MobilityFormTools),
                jsonSerializerOptions: jsonOptions);


            return pluginCollection;
        });

        services.AddTransient(serviceProvider =>
        {
            var config = serviceProvider.GetRequiredService<IConfiguration>();

            var model = config["OpenAI:Model"] ?? "gpt-5";
            var baseUrl = config["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1";
            var serviceId = config["OpenAI:ServiceId"] ?? "openAi";

            // Prefer user-secrets/config first, then env var:
            var apiKey =
                config["OpenAI:ApiKey"] ??
                Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "OpenAI API key not configured. Set it via `dotnet user-secrets set \"OpenAI:ApiKey\" \"...\"` " +
                    "or export OPENAI_API_KEY in your environment.");
            }

            var pluginCollection = serviceProvider.GetRequiredService<KernelPluginCollection>();
            var kernelBuilder = Kernel.CreateBuilder();

            kernelBuilder.AddOpenAIChatCompletion(
                model,
                new Uri(baseUrl),
                apiKey,
                serviceId: serviceId);

            foreach (var plugin in pluginCollection)
                kernelBuilder.Plugins.Add(plugin);

            return kernelBuilder.Build();
        });

        return services;
    }
}
