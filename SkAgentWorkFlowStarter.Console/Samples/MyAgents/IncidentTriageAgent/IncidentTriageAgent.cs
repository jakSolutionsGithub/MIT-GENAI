using Microsoft.SemanticKernel;
using SkAgentWorkFlowStarter.Console.Framework.Agents.Abstractions;
using SkAgentWorkFlowStarter.Console.Framework.Prompting;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.IncidentTriageAgent.Models;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.IncidentTriageAgent.Tools;

namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.IncidentTriageAgent;

public class IncidentTriageAgent(Kernel kernel, IPromptBuilder promptBuilder) : AgentBase<IncidentTriageInput>(kernel, promptBuilder)
{
    protected override string PromptFileName => "IncidentTriagePrompt";
    protected override IEnumerable<KernelFunction> AuthorizedKernelFunctions => GetKernelFunctionsForPlugin<IncidentTriageTools>();
}
