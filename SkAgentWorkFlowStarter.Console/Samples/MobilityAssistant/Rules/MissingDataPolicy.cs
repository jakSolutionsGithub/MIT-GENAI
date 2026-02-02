using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Models;
using System.Text.Json;


namespace SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Rules;

public static class MissingDataPolicy
{
    public static List<string> NextQuestions(MobilityFormState state, JsonDocument baseline)
    {
        var qs = new List<string>();

        // Q23
        if (state.Q23_Participants.Count == 0)
        {
            qs.Add("Has a mobility survey been conducted? If yes, what sample size?");
            qs.Add("Do you know where participants come from (Brussels/local vs rest of Belgium vs outside Europe)?");
            qs.Add("Is the event daytime or evening? (evening increases car/taxi use)");
            qs.Add("Is bike parking available? Is car parking available?");
            return qs;
        }

        // Q24
        if (state.Q24_Freight.Count == 0)
        {
            qs.Add("For suppliers/freight: can you ask suppliers directly? If not, tell me supplier category (food/AV/furniture/etc.) and where they come from.");
            qs.Add("How many delivery round-trips are expected? If unknown, I can assume 4.");
            return qs;
        }

        // Q26
        if (state.Q26_Staff.Count == 0)
        {
            qs.Add("How many staff will travel, from where, and what is the main transport mode?");
            qs.Add("Average round-trip distance per staff member (km)?");
            return qs;
        }

        return qs;
    }
}
