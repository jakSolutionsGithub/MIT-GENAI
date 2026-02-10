namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;

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
    public int? Attendees { get; set; }
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
}

public sealed class MobilityStaffSegment
{
    public string? Mode { get; set; }
    public int? Count { get; set; }
    public double? DistanceKm { get; set; }
}

public sealed class MobilityFreightItem
{
    public string? Mode { get; set; }
    public double? WeightKg { get; set; }
    public double? DistanceKm { get; set; }
    public int? RoundTrips { get; set; }
}

public sealed class MobilityFormState
{
    public MobilityFormStructured? Structured { get; set; }
    public List<string> Assumptions { get; set; } = [];
    public List<string> Conflicts { get; set; } = [];
}
