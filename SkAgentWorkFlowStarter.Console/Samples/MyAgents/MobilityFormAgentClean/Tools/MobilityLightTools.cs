using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;

public class MobilityLightTools
{
    [KernelFunction("co2_participants_kg")]
    public double Co2ParticipantsKg(
        [Description("Mode: walk|bike|metro|tram|train|bus|coach|car|electric car|carpool|car sharing|taxi|plane|ship|remote|other")] string mode,
        [Description("Number of participants")] int count,
        [Description("Round-trip distance in km")] double distanceKm)
    {
        System.Console.Error.WriteLine($"[action] Calcul CO2 participants: mode={mode}, count={count}, distance_km={distanceKm}");
        return Co2Calculator.ParticipantsKg(mode, count, distanceKm);
    }

    [KernelFunction("co2_staff_kg")]
    public double Co2StaffKg(
        [Description("Mode: walk|bike|metro|tram|train|bus|coach|car|electric car|carpool|car sharing|taxi|plane|ship|remote|other")] string mode,
        [Description("Number of staff")] int count,
        [Description("Round-trip distance in km")] double distanceKm)
    {
        System.Console.Error.WriteLine($"[action] Calcul CO2 staff: mode={mode}, count={count}, distance_km={distanceKm}");
        return Co2Calculator.StaffKg(mode, count, distanceKm);
    }

    [KernelFunction("co2_freight_kg")]
    public double Co2FreightKg(
        [Description("Mode: van|electric van|truck 19t|truck 40t|flight|ship|other")] string mode,
        [Description("Weight or quantity in kg (if unknown, pass 200) ")] double weightKg,
        [Description("Total distance in km (one-way)")] double distanceKm,
        [Description("Round trips")] int roundTrips)
    {
        System.Console.Error.WriteLine($"[action] Calcul CO2 fret: mode={mode}, weight_kg={weightKg}, distance_km={distanceKm}, aller_retour={roundTrips}");
        return Co2Calculator.FreightKg(mode, weightKg, distanceKm, roundTrips);
    }

    [KernelFunction("estimate_participant_split")]
    public string EstimateParticipantSplit([Description("Total attendees")] int totalAttendees)
    {
        System.Console.Error.WriteLine($"[action] Estimation répartition participants: total={totalAttendees}");
        var local = (int)Math.Round(totalAttendees * 0.8);
        var intl = Math.Max(0, totalAttendees - local);
        return $"local:{local},intl:{intl}";
    }

}
