using System.ComponentModel;
using Microsoft.SemanticKernel;
using SkAgentWorkFlowStarter.Console.Samples.Services;

namespace SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;

public class MobilityFormUpdateTools(IMobilityFormStateStore store)
{
    [KernelFunction("update_mobility_form")]
    public MobilityFormState UpdateMobilityForm(
        [Description("Partial patch to update the mobility form state")] MobilityFormPatch patch)
    {
        System.Console.Error.WriteLine("[action] Mise à jour du formulaire mobilité (nouvelle info détectée).");
        store.ApplyPatch(patch);
        return store.GetState();
    }

    [KernelFunction("get_mobility_form_state")]
    public MobilityFormState GetMobilityFormState()
    {
        System.Console.Error.WriteLine("[action] Lecture du formulaire mobilité.");
        return store.GetState();
    }
}
