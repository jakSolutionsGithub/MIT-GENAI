using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;

public class MobilityLightTools
{
    [KernelFunction("co2_participants_kg")]
    public double Co2ParticipantsKg(
        [Description("Mode: walk|bike|metro|tram|train|bus|coach|car|electric car|carpool|car sharing|taxi|plane|ship|other")] string mode,
        [Description("Number of participants")] int count,
        [Description("Round-trip distance in km")] double distanceKm)
    {
        var factor = ModeFactor(mode);
        return Math.Round(factor * count * distanceKm, 2);
    }

    [KernelFunction("co2_staff_kg")]
    public double Co2StaffKg(
        [Description("Mode: walk|bike|metro|tram|train|bus|coach|car|electric car|carpool|car sharing|taxi|plane|ship|other")] string mode,
        [Description("Number of staff")] int count,
        [Description("Round-trip distance in km")] double distanceKm)
    {
        var factor = ModeFactor(mode);
        return Math.Round(factor * count * distanceKm, 2);
    }

    [KernelFunction("co2_freight_kg")]
    public double Co2FreightKg(
        [Description("Mode: van|electric van|truck 19t|truck 40t|flight|ship|other")] string mode,
        [Description("Weight or quantity in kg (if unknown, pass 200) ")] double weightKg,
        [Description("Total distance in km (one-way)")] double distanceKm,
        [Description("Round trips")] int roundTrips)
    {
        var factor = FreightFactor(mode);
        var tons = Math.Max(0.05, weightKg / 1000.0);
        var km = distanceKm * Math.Max(1, roundTrips);
        return Math.Round(factor * tons * km, 2);
    }

    [KernelFunction("estimate_participant_split")]
    public string EstimateParticipantSplit([Description("Total attendees")] int totalAttendees)
    {
        var local = (int)Math.Round(totalAttendees * 0.8);
        var intl = Math.Max(0, totalAttendees - local);
        return $"local:{local},intl:{intl}";
    }

    private static double ModeFactor(string mode)
    {
        var m = (mode ?? "").Trim().ToLowerInvariant();
        return m switch
        {
            "walk" => 0.0,
            "bike" => 0.0,
            "metro" => 0.03,
            "tram" => 0.03,
            "train" => 0.04,
            "bus" => 0.10,
            "coach" => 0.06,
            "car" => 0.19,
            "electric car" => 0.07,
            "carpool" => 0.10,
            "car sharing" => 0.14,
            "taxi" => 0.22,
            "plane" => 0.25,
            "ship" => 0.03,
            _ => 0.12
        };
    }

    private static double FreightFactor(string mode)
    {
        var m = (mode ?? "").Trim().ToLowerInvariant();
        return m switch
        {
            "van" => 0.35,
            "electric van" => 0.15,
            "truck 19t" => 0.10,
            "truck 40t" => 0.06,
            "flight" => 0.60,
            "ship" => 0.02,
            _ => 0.12
        };
    }
}
