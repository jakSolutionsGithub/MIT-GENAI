using SkAgentWorkFlowStarter.Console.Framework.Agents.Models;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.IncidentTriageAgent;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.IncidentTriageAgent.Models;

namespace SkAgentWorkFlowStarter.Console.Samples.Services;

public interface IIncidentTriageAgentService
{
    Task<TriageResponse> CallIncidentTriageAgent(CancellationToken ct);
}

public class IncidentTriageAgentService(IncidentTriageAgent incidentTriageAgent) : IIncidentTriageAgentService
{
    public async Task<TriageResponse> CallIncidentTriageAgent(CancellationToken ct)
    {
        var request = new AgentRequest<IncidentTriageInput>(
            "Please triage and propose immediate actions.",
            new IncidentTriageInput(
                Incident: "API outage on checkout flow",
                Impact: "High",
                Team: "Core Checkout",
                Channel: "statuspage"));

        var result = await incidentTriageAgent.AskAsync(request, ct);
        return new TriageResponse(result.Content ?? string.Empty);
    }
}

public record TriageResponse(string Summary);
