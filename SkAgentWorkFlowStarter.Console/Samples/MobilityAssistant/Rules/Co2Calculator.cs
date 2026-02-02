using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Models;

namespace SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Rules;


/// THis is an example emission factors (kg CO2e per passenger-km or ton-km), THIS IS TO BE REPLACED WITH REAL ADCTORS AND VALUES ( to be done automatically ) ONLY for testing purposs and shouldn't be taken as 
/// backbone for the assitant, again kjust an example with random values ! 
public static class Co2Calculator
{
    
    private static readonly Dictionary<TransportMode, double> Pkm = new()
    {
        [TransportMode.Walk] = 0.0,
        [TransportMode.Bike] = 0.0,
        [TransportMode.Metro] = 0.03,
        [TransportMode.Tram] = 0.03,
        [TransportMode.Train] = 0.04,
        [TransportMode.Bus] = 0.10,
        [TransportMode.Coach] = 0.06,
        [TransportMode.Car] = 0.19,
        [TransportMode.ElectricCar] = 0.07,
        [TransportMode.Carpool] = 0.10,     
        [TransportMode.CarSharing] = 0.14,  
        [TransportMode.Taxi] = 0.22,
        [TransportMode.Plane] = 0.25,
        [TransportMode.Ship] = 0.03,
        [TransportMode.Other] = 0.15
    };

    
    private static readonly Dictionary<FreightMode, double> TonKm = new()
    {
        [FreightMode.Van] = 0.35,
        [FreightMode.ElectricVan] = 0.15,
        [FreightMode.Truck19T] = 0.10,
        [FreightMode.Truck40T] = 0.06,
        [FreightMode.Flight] = 0.60,
        [FreightMode.Ship] = 0.02,
        [FreightMode.Other] = 0.12
    };

    public static Co2Breakdown Compute(MobilityFormState state)
    {
        var byMode = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        double participants = 0;
        foreach (var seg in state.Q23_Participants)
        {
            var mode = seg.MainMode.Value;
            var factor = Pkm.GetValueOrDefault(mode, 0.15);
            var kg = factor * seg.ParticipantCount.Value * seg.RoundTripDistanceKm.Value;
            participants += kg;
            Add(byMode, $"participants:{mode}", kg);
        }

        double staff = 0;
        foreach (var seg in state.Q26_Staff)
        {
            var mode = seg.MainMode.Value;
            var factor = Pkm.GetValueOrDefault(mode, 0.15);
            var kg = factor * seg.StaffCount.Value * seg.RoundTripDistanceKm.Value;
            staff += kg;
            Add(byMode, $"staff:{mode}", kg);
        }

        double freight = 0;
        foreach (var item in state.Q24_Freight)
        {
            var mode = item.MainMode.Value;
            var factor = TonKm.GetValueOrDefault(mode, 0.12);

            var weightKg = item.Unit.Value == FreightUnit.Kg ? (item.WeightOrQuantity.Value ?? 200.0) : 200.0;
            var tons = Math.Max(0.05, weightKg / 1000.0);

            var km = item.TotalDistanceKm.Value * item.RoundTrips.Value;
            var kg = factor * tons * km;
            freight += kg;
            Add(byMode, $"freight:{mode}", kg);
        }

        return new Co2Breakdown(
            ParticipantsKg: participants,
            StaffKg: staff,
            FreightKg: freight,
            TotalKg: participants + staff + freight,
            ByModeKg: byMode
        );
    }

    private static void Add(Dictionary<string, double> dict, string key, double value)
    {
        dict[key] = dict.TryGetValue(key, out var existing) ? existing + value : value;
    }
}
