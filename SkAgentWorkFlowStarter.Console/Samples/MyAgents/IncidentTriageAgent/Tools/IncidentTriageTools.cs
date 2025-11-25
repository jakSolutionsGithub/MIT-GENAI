using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.IncidentTriageAgent.Tools;

public class IncidentTriageTools
{
    [KernelFunction("draft_status_update")]
    public string DraftStatusUpdate([Description("Short incident summary to share")] string summary) =>
        $"Status update: {summary} | Next update in 15 minutes.";

    [KernelFunction("suggest_runbook")]
    public string SuggestRunbook([Description("Incident or request type")] string topic) =>
        topic.ToLowerInvariant() switch
        {
            "api outage" => "Runbook: check gateway health, rollback last deploy, notify statuspage.",
            "latency" => "Runbook: capture traces, scale out replicas, run canary compare.",
            "security" => "Runbook: isolate traffic, rotate keys, open security incident ticket.",
            _ => $"Runbook: create JIRA, triage logs, loop in SME for '{topic}'."
        };

    [KernelFunction("contact_oncall")]
    public string ContactOncall([Description("Team name to page")] string team) =>
        $"Paging on-call for {team}: slack @oncall-{team.ToLowerInvariant().Replace(' ', '-')}, fallback phone bridge +1-555-0100.";
}
