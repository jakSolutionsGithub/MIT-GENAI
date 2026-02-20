using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;

namespace SkAgentWorkFlowStarter.Console.Framework.State;


public interface IAgentState { }


public interface IAgentStateStore<TState, TPatch>
    where TState : IAgentState
{
    TState GetState();
    void ApplyPatch(TPatch patch);
    void Reset();
}


[Description(
    "State management tools. update_state captures new structured facts. " +
    "get_state reads current state. Derived fields (CO2, totals) are computed " +
    "automatically — never include them in a patch.")]
public sealed class AgentStateTools<TState, TPatch>(
    IAgentStateStore<TState, TPatch> store,
    ILogger<AgentStateTools<TState, TPatch>>? logger = null)
    where TState : IAgentState
{
    private readonly ILogger _logger =
        (ILogger?)logger ?? NullLogger.Instance;

    [KernelFunction("update_state")]
    [Description(
        "Apply a structured patch to the current state. " +
        "Call when you have extracted NEW or CHANGED facts from the user message. " +
        "Prefer ONE merged patch per turn over multiple small patches. " +
        "Do NOT include computed fields (co2Kg, totals, attendees) — they are " +
        "recalculated automatically after every patch. " +
        "Returns the full updated state.")]
    public TState UpdateState(
        [Description(
            "Partial patch matching the domain's patch schema. " +
            "Only include fields that changed. Omit computed/derived fields.")]
        TPatch patch)
    {
        _logger.LogInformation("[update_state] Applying patch.");
        store.ApplyPatch(patch);
        var state = store.GetState();
        _logger.LogInformation("[update_state] Done.");
        return state;
    }

    [KernelFunction("get_state")]
    [Description(
        "Returns the current state. " +
        "Use the CurrentStateJson in the prompt context first — " +
        "call this only when that context looks stale or incomplete.")]
    public TState GetState()
    {
        _logger.LogInformation("[get_state] Reading state.");
        return store.GetState();
    }
}