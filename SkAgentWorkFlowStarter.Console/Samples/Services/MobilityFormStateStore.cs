using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;

namespace SkAgentWorkFlowStarter.Console.Samples.Services;

public interface IMobilityFormStateStore
{
    MobilityFormState GetState();
    void ApplyPatch(MobilityFormPatch patch);
}

public sealed class MobilityFormStateStore : IMobilityFormStateStore
{
    private readonly object _lock = new();
    private MobilityFormState _state = new();

    public MobilityFormState GetState()
    {
        lock (_lock)
        {
            return _state;
        }
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
            }

            if (patch.Assumptions is { Count: > 0 })
                _state.Assumptions.AddRange(patch.Assumptions);

            if (patch.Conflicts is { Count: > 0 })
                _state.Conflicts.AddRange(patch.Conflicts);
        }
    }

    private static void MergeStructured(MobilityFormStructured target, MobilityFormStructured patch)
    {
        if (!string.IsNullOrWhiteSpace(patch.EventType)) target.EventType = patch.EventType;
        if (!string.IsNullOrWhiteSpace(patch.Location)) target.Location = patch.Location;
        if (patch.Attendees is > 0) target.Attendees = patch.Attendees;
        if (!string.IsNullOrWhiteSpace(patch.Format)) target.Format = patch.Format;
        if (patch.DurationDays is > 0) target.DurationDays = patch.DurationDays;

        if (patch.ParticipantSegments is { Count: > 0 })
            (target.ParticipantSegments ??= []).AddRange(patch.ParticipantSegments);

        if (patch.StaffSegments is { Count: > 0 })
            (target.StaffSegments ??= []).AddRange(patch.StaffSegments);

        if (patch.Freight is { Count: > 0 })
            (target.Freight ??= []).AddRange(patch.Freight);

        if (patch.Constraints is { Count: > 0 })
            (target.Constraints ??= []).AddRange(patch.Constraints);
    }
}
