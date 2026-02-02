namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgent.Models;

public record MobilityFormInput(
    string BaselineJson,
    string SessionOverridesJson,
    string CurrentStateJson,
    string RecentTurnsJson,
    string MissingSummary,
    string UserMessage
);
