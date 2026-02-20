namespace SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Models;

public sealed record Co2Breakdown(
    double ParticipantsKg,
    double StaffKg,
    double FreightKg,
    double TotalKg,
    Dictionary<string, double> ByModeKg
);