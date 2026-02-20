using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using SkAgentWorkFlowStarter.Console.Framework.State;

namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;


[Description(
    "Mobility form domain conflict detection. " +
    "Detects logical inconsistencies, validates form completeness, " +
    "and provides sustainability ratings for transport modes.")]
public sealed class MobilityConflictTools
{
    private readonly IAgentStateStore<MobilityFormState, MobilityFormPatch> _stateStore;
    private readonly ILogger<MobilityConflictTools> _logger;

    public MobilityConflictTools(
        IAgentStateStore<MobilityFormState, MobilityFormPatch> stateStore,
        ILogger<MobilityConflictTools>? logger = null)
    {
        _stateStore = stateStore;
        _logger = logger ?? NullLogger<MobilityConflictTools>.Instance;
    }

    [KernelFunction("detect_form_conflicts")]
    [Description(
        "Scans the current mobility form state for logical inconsistencies " +
        "and missing required data. Returns a numbered list of detected issues, " +
        "or confirms no conflicts if the data is internally consistent. " +
        "Call this when the user has provided most of the form data.")]
    public string DetectFormConflicts()
    {
        var state = _stateStore.GetState();
        var issues = new List<string>();

        var structured = state.Structured;
        if (structured is null)
        {
            return "No form data collected yet — nothing to validate.";
        }

        if (structured.ParticipantSegments is null || structured.ParticipantSegments.Count == 0)
        {
            issues.Add("No participant transport segments defined.");
        }
        else
        {
            foreach (var seg in structured.ParticipantSegments)
            {
                if (string.IsNullOrWhiteSpace(seg.Mode))
                    issues.Add($"Participant segment has no transport mode (count={seg.Count}).");
                if (seg.Count is null or 0)
                    issues.Add($"Participant segment has no count (mode={seg.Mode}).");
                if (seg.DistanceKm is null or 0)
                    issues.Add($"Participant segment has no distance (mode={seg.Mode}, count={seg.Count}).");
            }
        }

        if (structured.StaffSegments is { Count: > 0 })
        {
            foreach (var seg in structured.StaffSegments)
            {
                if (string.IsNullOrWhiteSpace(seg.Mode))
                    issues.Add($"Staff segment has no transport mode (count={seg.Count}).");
                if (seg.Count is null or 0)
                    issues.Add($"Staff segment has no count (mode={seg.Mode}).");
            }
        }

        var totalCo2 = state.TotalCo2Kg;
        if (totalCo2 > 500_000)
        {
            issues.Add(
                $"Total CO2 estimate ({totalCo2:F0} kg CO2e) is unusually high — " +
                "please verify distances, counts, and transport modes.");
        }

        if (state.Conflicts.Count > 0)
        {
            issues.Add(
                $"{state.Conflicts.Count} conflict(s) already flagged: " +
                string.Join("; ", state.Conflicts));
        }

        _logger.LogInformation(
            "[MobilityConflictTools.detect_form_conflicts] Found {Count} issue(s).", issues.Count);

        return issues.Count == 0
            ? "No logical conflicts detected in the current form data."
            : string.Join("\n", issues.Select((issue, i) => $"{i + 1}. {issue}"));
    }

    [KernelFunction("get_sustainability_rating")]
    [Description(
        "Returns a sustainability rating and CO2 factor for a given transport mode. " +
        "Use this when suggesting greener alternatives or explaining the environmental " +
        "impact of a chosen mode to the user.")]
    public string GetSustainabilityRating(
        [Description("Transport mode to evaluate (e.g. plane, car, train, bus, bike).")]
        string mode)
    {
        _logger.LogInformation("[MobilityConflictTools.get_sustainability_rating] mode={Mode}", mode);

        var key = mode.Trim().ToLowerInvariant()
            .Replace(" ", "").Replace("-", "").Replace("_", "");

        return key switch
        {
            "walk" or "bike" =>
                "Rating: Excellent (0 kg CO2e/km). Zero-emission active mobility.",
            "ebike" or "escooter" =>
                "Rating: Very high (~0.005–0.012 kg CO2e/km). Near-zero emissions.",
            "metro" or "tram" =>
                "Rating: High (~0.004 kg CO2e/km). Efficient urban transit.",
            "train" or "thalys" or "eurostar" or "highspeedtrain" =>
                "Rating: High (~0.006 kg CO2e/km). Best for intercity travel.",
            "coach" =>
                "Rating: Medium-High (~0.027 kg CO2e/km). Good for groups.",
            "ferry" or "ship" =>
                "Rating: Medium (~0.019 kg CO2e/km). Variable by vessel type.",
            "electriccar" or "electricvehicle" =>
                "Rating: Medium (~0.053 kg CO2e/km, EU grid mix). Consider carpooling.",
            "carpool" =>
                "Rating: Medium (~0.064 kg CO2e/km). Better than single-occupancy car.",
            "bus" =>
                "Rating: Medium (~0.089 kg CO2e/km). Good for local groups.",
            "carsharing" =>
                "Rating: Low-Medium (~0.120 kg CO2e/km).",
            "rideshare" =>
                "Rating: Low (~0.155 kg CO2e/km).",
            "car" =>
                "Rating: Low (~0.193 kg CO2e/km). Consider train or carpooling for <1500 km.",
            "taxi" =>
                "Rating: Low (~0.210 kg CO2e/km). Suggest public transport.",
            "plane" or "flight" =>
                "Rating: Very low (~0.255 kg CO2e/km). Consider train for routes under 1000 km.",
            _ =>
                $"Rating: Unknown mode '{mode}'. Please specify a recognised transport mode " +
                "(walk, bike, train, car, bus, plane, etc.)."
        };
    }
}
