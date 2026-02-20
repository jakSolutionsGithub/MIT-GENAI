using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;
using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Models;
namespace SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Rules;


public static class Co2Calculator
{
    
    private static readonly Dictionary<string, double> PassengerKmFactors =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["walk"]         = 0.000,
            ["bike"]         = 0.000,
            ["ebike"]        = 0.005,
            ["escooter"]     = 0.012,
            ["metro"]        = 0.004,
            ["tram"]         = 0.004,
            ["train"]        = 0.006,   
            ["thalys"]       = 0.006,
            ["eurostar"]     = 0.006,
            ["bus"]          = 0.089,  
            ["coach"]        = 0.027,   
            ["car"]          = 0.193,   
            ["electriccar"]  = 0.053,   
            ["electric car"] = 0.053,
            ["carpool"]      = 0.064,  
            ["carsharing"]   = 0.120,
            ["taxi"]         = 0.210,
            ["rideshare"]    = 0.155,
            ["plane"]        = 0.255,   
            ["flight"]       = 0.255,
            ["ferry"]        = 0.019,
            ["ship"]         = 0.019,
            ["other"]        = 0.150,
        };

    
    private static readonly Dictionary<string, double> FreightTonneKmFactors =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["van"]          = 0.350,
            ["electricvan"]  = 0.130,
            ["electric van"] = 0.130,
            ["truck"]        = 0.096,   
            ["truck19t"]     = 0.096,
            ["truck40t"]     = 0.062,
            ["plane"]        = 0.602,
            ["flight"]       = 0.602,
            ["ship"]         = 0.016,
            ["ferry"]        = 0.016,
            ["train"]        = 0.028,
            ["other"]        = 0.120,
        };


    public static double ParticipantsKg(string? mode, int? count, double? distanceKm)
    {
        if (count is null or 0 || distanceKm is null or 0) return 0;
        var factor = ResolvePassengerFactor(mode);
        return Math.Round(factor * count.Value * distanceKm.Value * 2.0, 2);
    }


    public static double StaffKg(string? mode, int? count, double? distanceKm)
    {
        if (count is null or 0 || distanceKm is null or 0) return 0;
        var factor = ResolvePassengerFactor(mode);
        return Math.Round(factor * count.Value * distanceKm.Value * 2.0, 2);
    }

 
    public static double FreightKg(
        string? mode, double? weightKg, double? distanceKm, int? roundTrips)
    {
        if (weightKg is null or 0 || distanceKm is null or 0) return 0;
        var factor   = ResolveFreightFactor(mode);
        var tonnes   = Math.Max(0.001, weightKg.Value / 1000.0);
        var trips    = Math.Max(1, roundTrips ?? 1);
        return Math.Round(factor * tonnes * distanceKm.Value * trips, 2);
    }


    public static Co2Breakdown Compute(MobilityFormState state)
    {
        var byMode = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        double participants = 0, staff = 0, freight = 0;

        foreach (var seg in state.Structured?.ParticipantSegments ?? [])
        {
            var kg = ParticipantsKg(seg.Mode, seg.Count, seg.DistanceKm);
            participants += kg;
            Accumulate(byMode, $"participants:{seg.Mode ?? "other"}", kg);
        }

        foreach (var seg in state.Structured?.StaffSegments ?? [])
        {
            var kg = StaffKg(seg.Mode, seg.Count, seg.DistanceKm);
            staff += kg;
            Accumulate(byMode, $"staff:{seg.Mode ?? "other"}", kg);
        }

        foreach (var item in state.Structured?.Freight ?? [])
        {
            var kg = FreightKg(item.Mode, item.WeightKg, item.DistanceKm, item.RoundTrips);
            freight += kg;
            Accumulate(byMode, $"freight:{item.Mode ?? "other"}", kg);
        }

        return new Co2Breakdown(
            ParticipantsKg: Math.Round(participants, 2),
            StaffKg:        Math.Round(staff, 2),
            FreightKg:      Math.Round(freight, 2),
            TotalKg:        Math.Round(participants + staff + freight, 2),
            ByModeKg:       byMode);
    }

    

    private static double ResolvePassengerFactor(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return PassengerKmFactors["other"];

        var key = mode.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");

        if (PassengerKmFactors.TryGetValue(key, out var exact)) return exact;

        foreach (var (k, v) in PassengerKmFactors)
            if (key.Contains(k) || k.Contains(key)) return v;

        return PassengerKmFactors["other"];
    }

    private static double ResolveFreightFactor(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return FreightTonneKmFactors["other"];

        var key = mode.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");

        if (FreightTonneKmFactors.TryGetValue(key, out var exact)) return exact;

        foreach (var (k, v) in FreightTonneKmFactors)
            if (key.Contains(k) || k.Contains(key)) return v;

        return FreightTonneKmFactors["other"];
    }

    private static void Accumulate(Dictionary<string, double> dict, string key, double value)
        => dict[key] = dict.TryGetValue(key, out var existing) ? existing + value : value;
}