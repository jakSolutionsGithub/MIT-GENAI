using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Rules;

namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;


[Description("Quick estimation helpers for CO2 and participant distribution.")]
public sealed class MobilityLightTools(ILogger<MobilityLightTools>? logger = null)
{
    private readonly ILogger _logger = logger ?? NullLogger<MobilityLightTools>.Instance;

    [KernelFunction("co2_participants_kg")]
    [Description("Quick CO2 estimate for participants. One-way distance × 2 for round-trip.")]
    public double Co2ParticipantsKg(
        [Description("Mode: train|car|bus|plane|etc.")]
        string mode,

        [Description("Number of participants")]
        int count,

        [Description("One-way distance in km")]
        double distanceKm)
    {
        _logger.LogInformation(
            "[co2_participants_kg] mode={Mode} count={Count} distance={Distance}",
            mode, count, distanceKm);

        return Co2Calculator.ParticipantsKg(mode, count, distanceKm);
    }

    [KernelFunction("co2_staff_kg")]
    [Description("Quick CO2 estimate for staff. One-way distance × 2 for round-trip.")]
    public double Co2StaffKg(
        [Description("Mode: train|car|bus|etc.")]
        string mode,

        [Description("Number of staff")]
        int count,

        [Description("One-way distance in km")]
        double distanceKm)
    {
        _logger.LogInformation(
            "[co2_staff_kg] mode={Mode} count={Count} distance={Distance}",
            mode, count, distanceKm);

        return Co2Calculator.StaffKg(mode, count, distanceKm);
    }

    [KernelFunction("co2_freight_kg")]
    [Description("Quick CO2 estimate for freight.")]
    public double Co2FreightKg(
        [Description("Mode: van|truck|plane|ship|etc.")]
        string mode,

        [Description("Weight in kg")]
        double weightKg,

        [Description("One-way distance in km")]
        double distanceKm,

        [Description("Number of trips (default 2 for round-trip)")]
        int roundTrips = 2)
    {
        _logger.LogInformation(
            "[co2_freight_kg] mode={Mode} weight={Weight} distance={Distance} trips={Trips}",
            mode, weightKg, distanceKm, roundTrips);

        return Co2Calculator.FreightKg(mode, weightKg, distanceKm, roundTrips);
    }

    [KernelFunction("estimate_participant_split")]
    [Description(
        "Quick heuristic split for participants: 80% local, 20% international. " +
        "Use search_similar_records for data-driven estimates.")]
    public string EstimateParticipantSplit(
        [Description("Total number of attendees")]
        int totalAttendees)
    {
        _logger.LogInformation(
            "[estimate_participant_split] total={Total}", totalAttendees);

        var local = (int)Math.Round(totalAttendees * 0.8);
        var intl  = Math.Max(0, totalAttendees - local);

        return $"local:{local},intl:{intl}";
    }
}