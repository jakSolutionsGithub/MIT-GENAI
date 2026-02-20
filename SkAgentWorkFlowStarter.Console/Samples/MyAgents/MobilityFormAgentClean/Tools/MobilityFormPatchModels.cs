using SkAgentWorkFlowStarter.Console.Framework.State;

namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;


public sealed class MobilityFormState : IAgentState
{
    public MobilityFormStructured? Structured { get; set; }
    
    public double TotalCo2Kg =>
        (Structured?.ParticipantSegments?.Sum(s => s.Co2Kg ?? 0) ?? 0) +
        (Structured?.StaffSegments?.Sum(s => s.Co2Kg ?? 0) ?? 0) +
        (Structured?.Freight?.Sum(s => s.Co2Kg ?? 0) ?? 0);
    
    public List<string> Assumptions { get; set; } = [];
    public List<string> Conflicts { get; set; } = [];
}



public sealed class MobilityFormPatch
{
    public MobilityFormStructured? Structured { get; set; }
    public List<string>? Assumptions { get; set; }
    public List<string>? Conflicts { get; set; }
}


public sealed class MobilityFormStructured
{
    public string? EventType { get; set; }
    public string? Location { get; set; }
    
    public int? Attendees
    {
        get
        {
            if (ParticipantSegments is null || ParticipantSegments.Count == 0) return null;
            var sum = ParticipantSegments.Sum(s => s.Count ?? 0);
            return sum > 0 ? sum : null;
        }
    }
    
    public string? Format { get; set; }
    public int? DurationDays { get; set; }
    
    public List<MobilityParticipantSegment>? ParticipantSegments { get; set; }
    public List<MobilityStaffSegment>? StaffSegments { get; set; }
    public List<MobilityFreightItem>? Freight { get; set; }
    public List<string>? Constraints { get; set; }
}



public sealed class MobilityParticipantSegment
{
    public string? Origin { get; set; }
    public string? Mode { get; set; }
    public int? Count { get; set; }
    public double? DistanceKm { get; set; }
    public double? Co2Kg { get; set; }
}

public sealed class MobilityStaffSegment
{
    public string? Mode { get; set; }
    public int? Count { get; set; }
    public double? DistanceKm { get; set; }
    public double? Co2Kg { get; set; }
}

public sealed class MobilityFreightItem
{
    public string? Mode { get; set; }
    public double? WeightKg { get; set; }
    public double? DistanceKm { get; set; }
    public int? RoundTrips { get; set; }
    public double? Co2Kg { get; set; }
}