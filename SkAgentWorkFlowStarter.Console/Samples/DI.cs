using Microsoft.Extensions.DependencyInjection;
using SkAgentWorkFlowStarter.Console.Framework.Prompting;
using SkAgentWorkFlowStarter.Console.Framework.Workflow;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.IncidentTriageAgent;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.SummarizationAgent;
using SkAgentWorkFlowStarter.Console.Samples.Services;
using SkAgentWorkFlowStarter.Console.Samples.Workflow;

namespace SkAgentWorkFlowStarter.Console.Samples;

public static class DI
{
    public static IServiceCollection AddAgents(this IServiceCollection services)
    {
        services.AddSingleton<IncidentTriageAgent>();
        services.AddSingleton<SummarizationAgent>();
        services.AddSingleton<IPromptBuilder, PromptBuilder>();
        services.AddSingleton<IncidentTriageWorkflow>();
        services.AddSingleton<IIncidentTriageAgentService, IncidentTriageAgentService>();
        services.AddSingleton<IWorkflowSampleService, WorkflowSampleService>();
        services.AddTransient<WorkflowRunner>();
        return services;
    }
}
