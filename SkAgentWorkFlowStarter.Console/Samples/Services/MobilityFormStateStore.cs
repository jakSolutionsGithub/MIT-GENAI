using SkAgentWorkFlowStarter.Console.Framework.State;
using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Rules;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;

namespace SkAgentWorkFlowStarter.Console.Samples.Services;


public sealed class MobilityStateStore : IAgentStateStore<MobilityFormState, MobilityFormPatch>
{
    private readonly object _lock = new();
    private MobilityFormState _state = new();

    public MobilityFormState GetState()
    {
        lock (_lock) return _state;
    }

    public void ApplyPatch(MobilityFormPatch patch)
    {
        if (patch is null) return;

        lock (_lock)
        {
            if (patch.Structured is not null)
            {
                _state.Structured ??= new MobilityFormStructured();
                MergeStructured(_state.Structured, patch.Structured);
                RecomputeCo2(_state.Structured);
            }

            if (patch.Assumptions is { Count: > 0 })
                _state.Assumptions.AddRange(patch.Assumptions);

            if (patch.Conflicts is { Count: > 0 })
                _state.Conflicts.AddRange(patch.Conflicts);
        }
    }

    public void Reset()
    {
        lock (_lock) _state = new MobilityFormState();
    }


    private static void MergeStructured(MobilityFormStructured target, MobilityFormStructured patch)
    {
        
        if (!string.IsNullOrWhiteSpace(patch.EventType))  target.EventType  = patch.EventType;
        if (!string.IsNullOrWhiteSpace(patch.Location))   target.Location   = patch.Location;
        if (!string.IsNullOrWhiteSpace(patch.Format))     target.Format     = patch.Format;
        if (patch.DurationDays is > 0)                    target.DurationDays = patch.DurationDays;

        if (patch.ParticipantSegments is { Count: > 0 })
            target.ParticipantSegments = patch.ParticipantSegments;

        if (patch.StaffSegments is { Count: > 0 })
            target.StaffSegments = patch.StaffSegments;

        if (patch.Freight is { Count: > 0 })
            target.Freight = patch.Freight;

        if (patch.Constraints is { Count: > 0 })
            target.Constraints = patch.Constraints;
    }


    private static void RecomputeCo2(MobilityFormStructured structured)
    {
        if (structured.ParticipantSegments is { Count: > 0 })
            foreach (var s in structured.ParticipantSegments)
                s.Co2Kg = Co2Calculator.ParticipantsKg(s.Mode, s.Count, s.DistanceKm);

        if (structured.StaffSegments is { Count: > 0 })
            foreach (var s in structured.StaffSegments)
                s.Co2Kg = Co2Calculator.StaffKg(s.Mode, s.Count, s.DistanceKm);

        if (structured.Freight is { Count: > 0 })
            foreach (var s in structured.Freight)
                s.Co2Kg = Co2Calculator.FreightKg(s.Mode, s.WeightKg, s.DistanceKm, s.RoundTrips);
    }
}