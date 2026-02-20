using System.ComponentModel;
using Microsoft.SemanticKernel;
using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Models;
using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Rules;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;

namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;


[Description(
    "CO2 emission estimation tools for mobility planning. " +
    "Use for rough mid-conversation estimates before committing to state. " +
    "Authoritative CO2 is computed automatically after update_state.")]
public sealed class Co2Tools
{
    [KernelFunction("co2_participants_kg")]
    [Description(
        "Estimates CO2 (kg) for a participant travel segment. " +
        "distanceKm is the ONE-WAY distance — round trip is computed automatically. " +
        "Returns kg CO2e for the full segment (all participants, round trip). " +
        "Label result as 'rough order of magnitude' in your response.")]
    public double Co2ParticipantsKg(
        [Description(
            "Transport mode as a string. Supported: walk, bike, ebike, metro, tram, train, " +
            "bus, coach, car, electriccar, carpool, carsharing, taxi, plane, ferry, other.")]
        string mode,

        [Description("Number of participants in this segment.")]
        int count,

        [Description("One-way distance in kilometres.")]
        double distanceKm)
        => Co2Calculator.ParticipantsKg(mode, count, distanceKm);

    [KernelFunction("co2_staff_kg")]
    [Description(
        "Estimates CO2 (kg) for a staff travel segment. " +
        "distanceKm is ONE-WAY — round trip computed automatically. " +
        "Returns kg CO2e for the full staff segment.")]
    public double Co2StaffKg(
        [Description("Transport mode (same options as co2_participants_kg).")]
        string mode,

        [Description("Number of staff members in this segment.")]
        int count,

        [Description("One-way distance in kilometres.")]
        double distanceKm)
        => Co2Calculator.StaffKg(mode, count, distanceKm);

    [KernelFunction("co2_freight_kg")]
    [Description(
        "Estimates CO2 (kg) for a freight logistics segment. " +
        "weightKg: total weight of goods. distanceKm: one-way distance. " +
        "roundTrips: typically 2 (outbound + return), use 1 for one-way. " +
        "Returns kg CO2e for the full freight segment.")]
    public double Co2FreightKg(
        [Description("Freight mode. Supported: van, electricvan, truck, truck19t, truck40t, " +
                     "plane, ship, ferry, train, other.")]
        string mode,

        [Description("Weight of goods in kilograms.")]
        double weightKg,

        [Description("One-way distance in kilometres.")]
        double distanceKm,

        [Description("Number of trips (default 2 for round-trip logistics).")]
        int roundTrips = 2)
        => Co2Calculator.FreightKg(mode, weightKg, distanceKm, roundTrips);

    [KernelFunction("co2_full_breakdown")]
    [Description(
        "Computes the full CO2 breakdown across the entire current state. " +
        "Returns ParticipantsKg, StaffKg, FreightKg, TotalKg, and a ByMode breakdown. " +
        "Use this after update_state when you want to present a complete estimate. " +
        "The values here match what is stored in the state after the last patch.")]
    public Co2Breakdown Co2FullBreakdown(
        [Description("The current mobility form state (pass from get_state result).")]
        MobilityFormState currentState)
        => Co2Calculator.Compute(currentState);
}