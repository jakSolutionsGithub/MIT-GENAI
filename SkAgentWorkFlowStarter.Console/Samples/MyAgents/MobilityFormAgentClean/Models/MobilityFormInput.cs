namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Models;




public record MobilityFormInput(
    string BaselineJson,
    string CurrentStateJson,
    string UserMessage
);