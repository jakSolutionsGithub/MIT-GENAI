using SkAgentWorkFlowStarter.Console.Framework.Agents.Models;

namespace SkAgentWorkFlowStarter.Console.Framework.Agents.Abstractions;


public interface IAgent<TVariables>
{
    Task<AgentResponse> AskAsync(AgentRequest<TVariables> request, CancellationToken ct);

    
    IAsyncEnumerable<string> StreamAsync(AgentRequest<TVariables> request, CancellationToken ct);

    
    void ResetHistory();
}