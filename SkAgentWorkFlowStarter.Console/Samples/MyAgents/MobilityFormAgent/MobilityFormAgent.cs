using Microsoft.SemanticKernel;
using SkAgentWorkFlowStarter.Console.Framework.Agents.Abstractions;
using SkAgentWorkFlowStarter.Console.Framework.Prompting;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgent.Models;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgent.Tools;

namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgent;

public class MobilityFormAgent(Kernel kernel, IPromptBuilder promptBuilder)
    : AgentBase<MobilityFormInput>(kernel, promptBuilder)
{
    protected override string PromptFileName => "MobilityFormPrompt";
    protected override IEnumerable<KernelFunction> AuthorizedKernelFunctions =>
        GetKernelFunctionsForPlugin<MobilityFormTools>();
}
