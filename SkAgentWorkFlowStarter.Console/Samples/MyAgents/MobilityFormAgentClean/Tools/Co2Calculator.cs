namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;

public static class Co2Calculator
{
    public static double ParticipantsKg(string? mode, int? count, double? distanceKm)
    {
        if (count is null || distanceKm is null) return 0;
        var factor = ModeFactor(mode);
        return Math.Round(factor * count.Value * distanceKm.Value, 2);
    }

    public static double StaffKg(string? mode, int? count, double? distanceKm)
    {
        if (count is null || distanceKm is null) return 0;
        var factor = ModeFactor(mode);
        return Math.Round(factor * count.Value * distanceKm.Value, 2);
    }

    public static double FreightKg(string? mode, double? weightKg, double? distanceKm, int? roundTrips)
    {
        if (weightKg is null || distanceKm is null) return 0;
        var factor = FreightFactor(mode);
        var tons = Math.Max(0.05, weightKg.Value / 1000.0);
        var km = distanceKm.Value * Math.Max(1, roundTrips ?? 1);
        return Math.Round(factor * tons * km, 2);
    }

    private static double ModeFactor(string? mode)
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
            "remote" => 0.0,
            _ => 0.12
        };
    }

    private static double FreightFactor(string? mode)
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
