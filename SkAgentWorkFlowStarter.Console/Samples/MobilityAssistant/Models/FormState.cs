namespace SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Models;

public record ParticipantSegment(
    Provenanced<OriginCategory> Origin,
    Provenanced<TransportMode> MainMode,
    Provenanced<int> ParticipantCount,
    Provenanced<double> RoundTripDistanceKm
);

public record FreightItem(
    Provenanced<FreightUnit> Unit,
    Provenanced<string> SupplierName,
    Provenanced<string> Description,
    Provenanced<FreightMode> MainMode,
    Provenanced<double?> WeightOrQuantity,
    Provenanced<double> TotalDistanceKm,
    Provenanced<int> RoundTrips
);

public record StaffSegment(
    Provenanced<TransportMode> MainMode,
    Provenanced<int> StaffCount,
    Provenanced<double> RoundTripDistanceKm
);

public record MobilityFormState
{
    // Q23
    public List<ParticipantSegment> Q23_Participants { get; init; } = [];

    // Q24
    public List<FreightItem> Q24_Freight { get; init; } = [];

    // Q26
    public List<StaffSegment> Q26_Staff { get; init; } = [];

    // Q27–Q30
    public Provenanced<bool?> Q27_ParticipantsInformed { get; set; } = new(null, DataProvenance.UserProvided);
    public Provenanced<AlternativeToCarsAction?> Q28_AlternativeToCarsAction { get; set; } = new(null, DataProvenance.UserProvided);
    public Provenanced<string?> Q28_OtherActionText { get; set; } = new(null, DataProvenance.UserProvided);
    public Provenanced<bool?> Q29_SecuredBikeParking { get; set; } = new(null, DataProvenance.UserProvided);
    public Provenanced<bool?> Q30_AccommodationBooked { get; set; } = new(null, DataProvenance.UserProvided);


    public List<string> DetectedConflicts { get; init; } = [];
}
