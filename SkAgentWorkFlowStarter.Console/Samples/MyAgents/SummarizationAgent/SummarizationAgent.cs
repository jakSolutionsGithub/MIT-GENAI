using Microsoft.SemanticKernel;
using SkAgentWorkFlowStarter.Console.Framework.Agents.Abstractions;
using SkAgentWorkFlowStarter.Console.Framework.Prompting;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.SummarizationAgent.Models;

namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.SummarizationAgent;

public class SummarizationAgent(Kernel kernel, IPromptBuilder promptBuilder) : AgentBase<SummarizationInput>(kernel, promptBuilder)
{
    protected override string PromptFileName => "SummarizationPrompt";
    protected override IEnumerable<KernelFunction> AuthorizedKernelFunctions => [];
}
