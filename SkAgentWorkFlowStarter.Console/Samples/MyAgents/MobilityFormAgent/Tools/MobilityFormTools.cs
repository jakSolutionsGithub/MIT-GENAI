using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgent.Tools;

public class MobilityFormTools
{
    [KernelFunction("normalize_yes_no")]
    public bool? NormalizeYesNo([Description("User text like yes/no/y/n")] string text)
    {
        var t = text.Trim().ToLowerInvariant();
        if (t is "yes" or "y" or "true") return true;
        if (t is "no" or "n" or "false") return false;
        return null;
    }
}
