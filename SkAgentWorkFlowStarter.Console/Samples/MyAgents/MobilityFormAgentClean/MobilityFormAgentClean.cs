using Microsoft.SemanticKernel;
using System.Linq;
using SkAgentWorkFlowStarter.Console.Framework.Agents.Abstractions;
using SkAgentWorkFlowStarter.Console.Framework.Prompting;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgent.Models;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;

namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean;

public class MobilityFormAgentClean(Kernel kernel, IPromptBuilder promptBuilder)
    : AgentBase<MobilityFormInput>(kernel, promptBuilder)
{
    protected override string PromptFileName => "MobilityFormPromptClean";
    protected override IEnumerable<KernelFunction> AuthorizedKernelFunctions =>
        GetKernelFunctionsForPlugin<MobilityLightTools>()
            .Concat(GetKernelFunctionsForPlugin<MobilityFormUpdateTools>());

    protected override bool UsePersistentHistory => true;
}
