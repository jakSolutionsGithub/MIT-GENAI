namespace SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Models;

public enum DataProvenance
{
    UserProvided = 0,
    AiEstimated = 1,
    FromReferenceDoc = 2
}

public record Provenanced<T>(T Value, DataProvenance Provenance);
