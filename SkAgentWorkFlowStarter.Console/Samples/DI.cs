using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SkAgentWorkFlowStarter.Console.Framework.Guidelines;
using SkAgentWorkFlowStarter.Console.Framework.Memory;
using SkAgentWorkFlowStarter.Console.Framework.Prompting;
using SkAgentWorkFlowStarter.Console.Framework.State;
using SkAgentWorkFlowStarter.Console.Framework.Workflow;
using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Guidelines;
using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Historical.Models;
using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Historical.Rules;
using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.State;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.IncidentTriageAgent;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.IncidentTriageAgent.Tools;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.SummarizationAgent;
using SkAgentWorkFlowStarter.Console.Samples.Services;
using SkAgentWorkFlowStarter.Console.Samples.Workflow;
using SkAgentWorkFlowStarter.Console.Samples.Workflow.Tools;


namespace SkAgentWorkFlowStarter.Console.Samples;


public static class DI
{
    public static IServiceCollection AddAgents(this IServiceCollection services)
    {
        //  1. Framework infrastructure 
        services.AddSingleton<IPromptBuilder, PromptBuilder>();
        services.AddTransient<WorkflowRunner>();

        // 2. Domain state 
        // MobilityStateStore implements IAgentStateStore<MobilityFormState, MobilityFormPatch>
      
        services.AddSingleton<MobilityStateStore>();
        services.AddSingleton<IAgentStateStore<MobilityFormState, MobilityFormPatch>>(
            sp => sp.GetRequiredService<MobilityStateStore>());

        //  2b. Domain baseline ref 
        services.AddSingleton<MobilityBaselineStore>();
        services.AddSingleton<IBaselineStore<JsonDocument>>(
            sp => sp.GetRequiredService<MobilityBaselineStore>());

        // 3. RAG
        services.AddSingleton<MobilityHistoryIngestionService>();
        services.AddSingleton<MobilityHistorySearchService>();
        
        services.AddSingleton<IAgentMemorySearch<MobilityHistoryRecord>>(
            sp => sp.GetRequiredService<MobilityHistorySearchService>());
        services.AddSingleton<IAgentMemoryIngestion<MobilityHistoryRecord>>(
            sp => sp.GetRequiredService<MobilityHistoryIngestionService>());

        //  4. guidelines 
        services.AddSingleton<IGuidelinesLibrary, MobilityGuidelinesLibrary>();

        //  5. plugins
      
        services.AddTransient<AgentStateTools<MobilityFormState, MobilityFormPatch>>();
        services.AddTransient<AgentMemoryTools<MobilityHistoryRecord>>();
        services.AddTransient<GuidelinesTools>();
        services.AddTransient<BaselineConflictTools<JsonDocument>>();

     
        services.AddTransient<Co2Tools>();
        services.AddTransient<MobilityLightTools>();
        
        services.AddTransient<MobilityConflictTools>();

        services.AddTransient<IncidentTriageTools>();
        services.AddTransient<WorkflowTools>();

        //  6. Agents ( have to delete Incident and Tirage )
        services.AddSingleton<IncidentTriageAgent>();
        services.AddSingleton<SummarizationAgent>();
        services.AddSingleton<MobilityFormAgentClean>();

        services.AddSingleton<IIncidentTriageAgentService, IncidentTriageAgentService>();
        services.AddSingleton<IWorkflowSampleService, WorkflowSampleService>();
        services.AddSingleton<IMobilityChatServiceClean, MobilityChatServiceClean>();

        services.AddSingleton<IncidentTriageWorkflow>();

        return services;
    }
}