using System.Text.Json;
using SkAgentWorkFlowStarter.Console.Framework.Agents.Models;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgent.Models;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean;

namespace SkAgentWorkFlowStarter.Console.Samples.Services;

public interface IMobilityChatServiceClean
{
    Task RunInteractiveAsync(CancellationToken ct);
}

public sealed class MobilityChatServiceClean : IMobilityChatServiceClean
{
    private readonly MobilityFormAgentClean _agent;
    private readonly IMobilityFormStateStore _stateStore;
    private string _baselineJson = "{}";
    public MobilityChatServiceClean(MobilityFormAgentClean agent, IMobilityFormStateStore stateStore)
    {
        _agent = agent;
        _stateStore = stateStore;
    }

    public async Task RunInteractiveAsync(CancellationToken ct)
    {
        _agent.ResetHistory();
        LoadBaseline();

        System.Console.WriteLine("Mobility Assistant (clean-light, type 'done' to finish, 'state' to print context).");

        while (!ct.IsCancellationRequested)
        {
            System.Console.Write("\nYou: ");
            var user = System.Console.ReadLine() ?? "";

            if (string.Equals(user.Trim(), "done", StringComparison.OrdinalIgnoreCase))
                break;

            if (string.Equals(user.Trim(), "state", StringComparison.OrdinalIgnoreCase))
            {
                PrintContext();
                continue;
            }

            var input = new MobilityFormInput(
                BaselineJson: _baselineJson,
                SessionOverridesJson: "{}",
                CurrentStateJson: JsonSerializer.Serialize(_stateStore.GetState(), new JsonSerializerOptions { WriteIndented = false }),
                RecentTurnsJson: "[]",
                MissingSummary: "Let the model decide what is missing.",
                UserMessage: user);

            var printedAssistantLabel = false;
            await foreach (var chunk in _agent.StreamAsync(
                               new AgentRequest<MobilityFormInput>(user, input),
                               ct))
            {
                if (!printedAssistantLabel)
                {
                    System.Console.Write("\nAssistant: ");
                    printedAssistantLabel = true;
                }
                System.Console.Write(chunk);
            }
            if (printedAssistantLabel)
                System.Console.WriteLine();
        }
    }

    private void LoadBaseline()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Samples", "MobilityAssistant", "BaselineForm.json");
        if (!File.Exists(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), "Samples", "MobilityAssistant", "BaselineForm.json");

        _baselineJson = File.Exists(path) ? File.ReadAllText(path) : "{}";
    }

    private void PrintContext()
    {
        System.Console.WriteLine("\n--- CURRENT STATE ---");
        System.Console.WriteLine(JsonSerializer.Serialize(_stateStore.GetState(), new JsonSerializerOptions { WriteIndented = true }));
    }
}
