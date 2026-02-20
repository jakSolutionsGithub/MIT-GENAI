using System.Text.Json;
using SkAgentWorkFlowStarter.Console.Framework.Agents.Models;
using SkAgentWorkFlowStarter.Console.Framework.State;
using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.State;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Models;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.MobilityFormAgentClean.Tools;

namespace SkAgentWorkFlowStarter.Console.Samples.Services;

public interface IMobilityChatServiceClean
{
    Task RunInteractiveAsync(CancellationToken ct);
}


public sealed class MobilityChatServiceClean : IMobilityChatServiceClean
{
    private readonly MobilityFormAgentClean _agent;
    private readonly IAgentStateStore<MobilityFormState, MobilityFormPatch> _stateStore;
    private readonly MobilityBaselineStore _baselineStore;

    private static readonly JsonSerializerOptions JsonCompact =
        new(JsonSerializerDefaults.Web) { WriteIndented = false };

    private static readonly JsonSerializerOptions JsonPretty =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public MobilityChatServiceClean(
        MobilityFormAgentClean agent,
        IAgentStateStore<MobilityFormState, MobilityFormPatch> stateStore,
        MobilityBaselineStore baselineStore)
    {
        _agent         = agent;
        _stateStore    = stateStore;
        _baselineStore = baselineStore;
    }

    public async Task RunInteractiveAsync(CancellationToken ct)
    {
        
        _agent.ResetHistory();
        _stateStore.Reset();
        _baselineStore.Reset();
        LoadBaseline();

        System.Console.WriteLine(
            "Mobility Assistant — type 'done' to finish, 'state' to print current state, " +
            "'baseline' to print baseline.");

        while (!ct.IsCancellationRequested)
        {
            System.Console.Write("\nYou: ");
            var user = System.Console.ReadLine() ?? "";

            if (string.Equals(user.Trim(), "done", StringComparison.OrdinalIgnoreCase))
                break;

            if (string.Equals(user.Trim(), "state", StringComparison.OrdinalIgnoreCase))
            {
                PrintState();
                continue;
            }

            if (string.Equals(user.Trim(), "baseline", StringComparison.OrdinalIgnoreCase))
            {
                PrintBaseline();
                continue;
            }


            var baselineJson = _baselineStore.GetBaseline().RootElement.GetRawText();
            var currentStateJson = JsonSerializer.Serialize(
                _stateStore.GetState(), JsonCompact);

            var input = new MobilityFormInput(
                BaselineJson:     baselineJson,
                CurrentStateJson: currentStateJson,
                UserMessage:      user);

            var request = new AgentRequest<MobilityFormInput>(user, input);

            //  Stream LLM rezponse 
            var printedLabel = false;
            await foreach (var chunk in _agent.StreamAsync(request, ct))
            {
                if (!printedLabel)
                {
                    System.Console.Write("\nAssistant: ");
                    printedLabel = true;
                }
                System.Console.Write(chunk);
            }

            if (printedLabel) System.Console.WriteLine();


            var conflict = _baselineStore.GetPendingConflict();
            if (conflict is not null)
            {
                System.Console.WriteLine($"\n⚠️  {conflict.QuestionToUser}");
                System.Console.WriteLine(
                    $"    (Baseline: {conflict.BaselineValue}, " +
                    $"You mentioned: {conflict.UserMentionedValue})");
            }
        }

        System.Console.WriteLine("\n✅ Session complete.");
        PrintState();
    }



    private void LoadBaseline()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Samples", "MobilityAssistant", "BaselineForm.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Samples", "MobilityAssistant", "BaselineForm.json")
        };

        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null)
        {
            throw new FileNotFoundException(
                "BaselineForm.json not found. Searched: " +
                string.Join(", ", candidates));
        }

        _baselineStore.LoadBaseline(path);
        System.Console.WriteLine($"[Loaded baseline from {Path.GetFileName(path)}]");
    }

    private void PrintState()
    {
        System.Console.WriteLine("\n--- CURRENT STATE (working data) ---");
        System.Console.WriteLine(
            JsonSerializer.Serialize(_stateStore.GetState(), JsonPretty));
        System.Console.WriteLine("------------------------------------");
    }

    private void PrintBaseline()
    {
        System.Console.WriteLine("\n--- BASELINE (reference + overrides) ---");
        System.Console.WriteLine(
            _baselineStore.GetBaseline().RootElement.GetRawText());

        var conflict = _baselineStore.GetPendingConflict();
        if (conflict is not null)
        {
            System.Console.WriteLine("\n[Pending conflict]:");
            System.Console.WriteLine($"  Field: {conflict.FieldLabel}");
            System.Console.WriteLine($"  Baseline: {conflict.BaselineValue}");
            System.Console.WriteLine($"  User mentioned: {conflict.UserMentionedValue}");
        }

        System.Console.WriteLine("----------------------------------------");
    }
}