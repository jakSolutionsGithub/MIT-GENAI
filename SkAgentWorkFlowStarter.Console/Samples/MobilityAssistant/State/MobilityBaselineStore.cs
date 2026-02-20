using System.Text.Json;
using SkAgentWorkFlowStarter.Console.Framework.State;

namespace SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.State;


public sealed class MobilityBaselineStore : IBaselineStore<JsonDocument>
{
    private readonly object _lock = new();
    private JsonDocument? _baseline;
    private readonly Dictionary<string, object> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private BaselineConflict? _pendingConflict;


    public void LoadBaseline(string jsonFilePath)
    {
        lock (_lock)
        {
            var json = File.ReadAllText(jsonFilePath);
            _baseline = JsonDocument.Parse(json);
        }
    }

    public JsonDocument GetBaseline()
    {
        lock (_lock)
        {
            if (_baseline is null)
                throw new InvalidOperationException(
                    "Baseline not loaded. Call LoadBaseline() at startup.");
            return _baseline;
        }
    }

    public object? GetEffectiveValue(string fieldPath)
    {
        lock (_lock)
        {
            if (_overrides.TryGetValue(fieldPath, out var ov))
                return ov;

            if (_baseline is null) return null;
            return TryReadJsonPath(_baseline.RootElement, fieldPath);
        }
    }

    public void FlagConflict(
        string fieldPath,
        string fieldLabel,
        object baselineValue,
        object userMentionedValue,
        string questionToUser)
    {
        lock (_lock)
        {
            _pendingConflict = new BaselineConflict(
                FieldPath:           fieldPath,
                FieldLabel:          fieldLabel,
                BaselineValue:       baselineValue,
                UserMentionedValue:  userMentionedValue,
                QuestionToUser:      questionToUser);
        }
    }

    public void ResolveConflict(string fieldPath, object confirmedValue)
    {
        lock (_lock)
        {
            _overrides[fieldPath] = confirmedValue;

            _pendingConflict = null;
        }
    }

    public BaselineConflict? GetPendingConflict()
    {
        lock (_lock) return _pendingConflict;
    }

    public void Reset()
    {
        lock (_lock)
        {
            _overrides.Clear();
            _pendingConflict = null;
        }
    }



    private static object? TryReadJsonPath(JsonElement root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = root;

        foreach (var part in parts)
        {
            if (current.ValueKind != JsonValueKind.Object) return null;
            if (!current.TryGetProperty(part, out current)) return null;
        }

        return JsonElementToObject(current);
    }

    private static object? JsonElementToObject(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String  => e.GetString(),
        JsonValueKind.Number  => e.TryGetInt32(out var i) ? i : e.GetDouble(),
        JsonValueKind.True    => true,
        JsonValueKind.False   => false,
        JsonValueKind.Null    => null,
        JsonValueKind.Array   => e.GetRawText(), 
        JsonValueKind.Object  => e.GetRawText(), 
        _ => e.GetRawText()
    };
}