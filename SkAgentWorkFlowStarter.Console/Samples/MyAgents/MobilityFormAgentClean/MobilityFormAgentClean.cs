using System.Text.Json;
using Microsoft.SemanticKernel;
using SkAgentWorkFlowStarter.Console.Framework.Agents.Abstractions;
using SkAgentWorkFlowStarter.Console.Framework.Guidelines;
using SkAgentWorkFlowStarter.Console.Framework.Memory;
using SkAgentWorkFlowStarter.Console.Framework.Prompting;
using SkAgentWorkFlowStarter.Console.Framework.State;
using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Historical.Models;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Models;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;

namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean;


public sealed class MobilityFormAgentClean(Kernel kernel, IPromptBuilder promptBuilder)
    : AgentBase<MobilityFormInput>(kernel, promptBuilder)
{
    protected override string PromptFileName => "MobilityFormPromptClean";

    protected override bool UsePersistentHistory => true;

    
    protected override int MaxAutoInvokeAttempts => 15;

    protected override IEnumerable<KernelFunction> AuthorizedKernelFunctions =>
        // Framework tools
        GetKernelFunctionsForPlugin<AgentStateTools<MobilityFormState, MobilityFormPatch>>()
        .Concat(GetKernelFunctionsForPlugin<AgentMemoryTools<MobilityHistoryRecord>>())
        .Concat(GetKernelFunctionsForPlugin<GuidelinesTools>())
        .Concat(GetKernelFunctionsForPlugin<BaselineConflictTools<JsonDocument>>())
        // Domain tools
        .Concat(GetKernelFunctionsForPlugin<Co2Tools>())
        .Concat(GetKernelFunctionsForPlugin<MobilityLightTools>());
}


//        .Concat(GetKernelFunctionsForPlugin<MobilityConflictTools>())
