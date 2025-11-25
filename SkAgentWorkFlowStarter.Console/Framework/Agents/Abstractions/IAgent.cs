using SkAgentWorkFlowStarter.Console.Framework.Agents.Models;

namespace SkAgentWorkFlowStarter.Console.Framework.Agents.Abstractions;

public interface IAgent<TVariables>
{
    Task<AgentResponse> AskAsync(AgentRequest<TVariables> request, CancellationToken ct);
}
