using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.IncidentTriageAgent.Tools;
using SkAgentWorkFlowStarter.Console.Samples.Workflow.Tools;

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

                pluginCollection.AddFromType<IncidentTriageTools>(serviceProvider: serviceProvider,
                    pluginName: nameof(IncidentTriageTools),
                    jsonSerializerOptions: jsonOptions);

                pluginCollection.AddFromType<WorkflowTools>(serviceProvider: serviceProvider,
                    pluginName: nameof(WorkflowTools),
                    jsonSerializerOptions: jsonOptions);

                return pluginCollection;
            }
        );

        services.AddTransient(serviceProvider =>
        {
            var pluginCollection = serviceProvider.GetRequiredService<KernelPluginCollection>();
            var kernelBuilder = Kernel.CreateBuilder();

            kernelBuilder.AddOpenAIChatCompletion("gpt-4.1",
                new Uri("https://api.openai.com/v1"),
                "YOUR_KEY", serviceId: "openAi");

            foreach (var plugin in pluginCollection) kernelBuilder.Plugins.Add(plugin);

            return kernelBuilder.Build();
        });
        return services;
    }
}
