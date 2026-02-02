namespace SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Models;

public enum EventType
{
    Corporate,
    PublicOrTradeShow,
    IncentiveOrTeambuilding,
    Unknown
}

public enum OriginCategory
{
    Local_0_15km,
    Regional_15_100km,
    National,
    Africa,
    Asia,
    Europe,
    NorthAmerica,
    SouthAmerica,
    Oceania
}

public enum TransportMode
{
    Walk,
    Bike,
    Car,
    Carpool,
    CarSharing,
    ElectricCar,
    Taxi,
    Bus,
    Train,
    Metro,
    Tram,
    Coach,
    Plane,
    Ship,
    Other
}

public enum FreightUnit
{
    Kg,
    CubicMeter,
    NumberOfItems
}

public enum FreightMode
{
    Van,
    ElectricVan,
    Truck19T,
    Truck40T,
    Flight,
    Ship,
    Other
}

public enum AlternativeToCarsAction
{
    NoAction,
    BikeOrWalkingDistance,
    PublicTransportPromotion,
    PrivateGroupTransport,
    ElectricPrivateGroupTransport,
    CarSharing,
    Other
}
