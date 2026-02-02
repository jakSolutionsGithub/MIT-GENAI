using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Models;

namespace SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Rules;

public static class MobilityEstimator
{
    public static void EstimateIfNeeded(MobilityFormState state, int? totalAttendees)
    {
       
        if (state.Q23_Participants.Count == 0 && totalAttendees is not null)
        {
       
            var local = (int)Math.Round(totalAttendees.Value * 0.80);
            var intl = totalAttendees.Value - local;

            state.Q23_Participants.Add(new ParticipantSegment(
                new Provenanced<OriginCategory>(OriginCategory.Local_0_15km, DataProvenance.AiEstimated),
                new Provenanced<TransportMode>(TransportMode.Train, DataProvenance.AiEstimated),
                new Provenanced<int>(local, DataProvenance.AiEstimated),
                new Provenanced<double>(15, DataProvenance.AiEstimated) 
            ));

            if (intl > 0)
            {
                state.Q23_Participants.Add(new ParticipantSegment(
                    new Provenanced<OriginCategory>(OriginCategory.NorthAmerica, DataProvenance.AiEstimated),
                    new Provenanced<TransportMode>(TransportMode.Plane, DataProvenance.AiEstimated),
                    new Provenanced<int>(intl, DataProvenance.AiEstimated),
                    new Provenanced<double>(3000, DataProvenance.AiEstimated) 
                ));
            }
        }

       
    }
}
