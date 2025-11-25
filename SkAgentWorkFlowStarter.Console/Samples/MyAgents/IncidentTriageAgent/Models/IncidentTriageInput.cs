namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.IncidentTriageAgent.Models;

public record IncidentTriageInput(
    string Incident,
    string Impact,
    string Team,
    string Channel);
