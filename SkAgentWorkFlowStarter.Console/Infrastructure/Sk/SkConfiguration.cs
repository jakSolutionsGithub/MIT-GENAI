using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.InMemory;

using OpenAI;
using SkAgentWorkFlowStarter.Console.Framework.Guidelines;
using SkAgentWorkFlowStarter.Console.Framework.Memory;
using SkAgentWorkFlowStarter.Console.Framework.State;
using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Historical.Models;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.IncidentTriageAgent.Tools;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;
using SkAgentWorkFlowStarter.Console.Samples.Workflow.Tools;

namespace SkAgentWorkFlowStarter.Console.Infrastructure.Sk;

/// <summary>
/// Semantic Kernel configuration — builds and registers the Kernel with all plugins.
///
/// ── Plugin naming contract ────────────────────────────────────────────────────
///
///   Plugin names are derived from typeof(T).Name with generic suffixes removed.
///   AgentBase.GetKernelFunctionsForPlugin&lt;T&gt;() uses the same expression.
///   They always match because both sides call typeof(T).Name.Split('`')[0].
///
///   For generic types (e.g. AgentStateTools&lt;MobilityFormState, MobilityFormPatch&gt;):
///   typeof(T).Name = "AgentStateTools`2" → "AgentStateTools"
///   This ensures valid plugin names without backticks.
///
/// ── Instance ownership ────────────────────────────────────────────────────────
///
///   All plugins are resolved from DI via sp.GetRequiredService&lt;T&gt;() then passed
///   to AddFromObject(instance, name). The Kernel holds the same object the DI
///   container owns — singleton sharing is correct.
///
/// ── What was removed ─────────────────────────────────────────────────────────
///
///   These old tool registrations were deleted:
///     - MobilityFormUpdateTools     → replaced by AgentStateTools<T,TPatch>
///     - MobilityGuidelinesTools     → replaced by GuidelinesTools
///     - MobilityHistoricalTools     → replaced by AgentMemoryTools<T>
///     - MobilityMissingDataTools    → replaced by AgentMemoryTools<T>
/// </summary>
public static class SkConfiguration
{
    public static IServiceCollection AddSk(this IServiceCollection services)
    {
        // ── 1. Shared JSON options ────────────────────────────────────────────
        services.AddSingleton(_ =>
        {
            var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            opts.Converters.Add(new JsonStringEnumConverter());
            return opts;
        });

        // ── 2. Embedding generator ────────────────────────────────────────────
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var apiKey = config["OpenAI:ApiKey"]
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? throw new InvalidOperationException(
                    "OpenAI API key not configured. " +
                    "Set OpenAI:ApiKey in appsettings/user-secrets or OPENAI_API_KEY env var.");
            var model = config["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
            return new OpenAIClient(apiKey)
                .GetEmbeddingClient(model)
                .AsIEmbeddingGenerator();
        });

        // ── 3. Vector store ───────────────────────────────────────────────────
        services.AddSingleton<InMemoryVectorStore>();
        // ── 4. Kernel ─────────────────────────────────────────────────────────
        // Transient: fresh Kernel per resolution, shared plugin singleton instances.
        services.AddTransient(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();

            var model = config["OpenAI:Model"]
                ?? throw new InvalidOperationException(
                    "OpenAI:Model not configured in appsettings.json.");
            var baseUrl   = config["OpenAI:BaseUrl"]   ?? "https://api.openai.com/v1";
            var serviceId = config["OpenAI:ServiceId"] ?? "openAi";
            var apiKey    = config["OpenAI:ApiKey"]
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? throw new InvalidOperationException("OpenAI API key not configured.");

            var kb = Kernel.CreateBuilder();
            kb.AddOpenAIChatCompletion(
                modelId:   model,
                endpoint:  new Uri(baseUrl),
                apiKey:    apiKey,
                serviceId: serviceId);

            // ── Framework generic plugins (mobility domain) ───────────────────
            Register<AgentStateTools<MobilityFormState, MobilityFormPatch>>(kb, sp);
            Register<AgentMemoryTools<MobilityHistoryRecord>>(kb, sp);
            Register<GuidelinesTools>(kb, sp);
            Register<BaselineConflictTools<JsonDocument>>(kb, sp);

            // ── Domain tools (mobility-specific) ──────────────────────────────
            // NOTE: Only register tools that still exist after cleanup.
            // Removed: MobilityFormUpdateTools, MobilityGuidelinesTools,
            //          MobilityHistoricalTools, MobilityMissingDataTools
            Register<Co2Tools>(kb, sp);
            Register<MobilityLightTools>(kb, sp);
            
            Register<MobilityConflictTools>(kb, sp);

            // ── Other agents' tools ───────────────────────────────────────────
            Register<IncidentTriageTools>(kb, sp);
            Register<WorkflowTools>(kb, sp);

            return kb.Build();
        });

        return services;
    }

    /// <summary>
    /// Resolves T from DI and registers it as a Kernel plugin.
    /// Plugin name = typeof(T).Name with generic suffix removed (e.g. "`2").
    /// This ensures valid plugin names and matches AgentBase.GetKernelFunctionsForPlugin&lt;T&gt;().
    /// </summary>
    private static void Register<T>(IKernelBuilder kb, IServiceProvider sp) where T : class
    {
        var pluginName = typeof(T).Name.Split('`')[0]; // Remove generic suffix like "`2"
        kb.Plugins.AddFromObject(sp.GetRequiredService<T>(), pluginName);
    }
}