using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel; 
using SkAgentWorkFlowStarter.Console.Framework.Prompting;
using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Models;
using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Rules;

namespace SkAgentWorkFlowStarter.Console.Samples.Services;

public interface IMobilityChatService
{
    Task RunInteractiveAsync(CancellationToken ct);
}

public sealed class MobilityChatService : IMobilityChatService
{
    private readonly IPromptBuilder _promptBuilder;
    private readonly ChatClient _chatClient;

    private MobilityFormState _state = new();
    private JsonDocument _baseline = default!;

    private readonly List<Turn> _recentTurns = new();
    private const int RecentTurnsMax = 6;

    private readonly Dictionary<string, object?> _overrides = new(StringComparer.OrdinalIgnoreCase);

    private bool _awaitingConflictResolution;
    private Conflict? _pendingConflict;
    private string? _pendingUserMessageToReplay;

    private const string MobilityPromptFileName = "MobilityFormPrompt";

    public MobilityChatService(IPromptBuilder promptBuilder, IConfiguration config)
    {
        _promptBuilder = promptBuilder;

        var model = config["OpenAI:Model"] ?? "gpt-5";
        var baseUrl = config["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1";

        var apiKey =
            config["OpenAI:ApiKey"] ??
            Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key not configured. Set it via `dotnet user-secrets set \"OpenAI:ApiKey\" \"...\"` " +
                "or export OPENAI_API_KEY in your environment.");
        }

        var options = new OpenAIClientOptions();

        var endpointProp =
            options.GetType().GetProperty("Endpoint")
            ?? options.GetType().GetProperty("BaseUri")
            ?? options.GetType().GetProperty("BaseUrl");

        if (endpointProp is not null && endpointProp.CanWrite)
        {
            endpointProp.SetValue(options, new Uri(baseUrl));
        }

        var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        _chatClient = client.GetChatClient(model);
    }

    public async Task RunInteractiveAsync(CancellationToken ct)
    {
        LoadBaseline();

        System.Console.WriteLine("Mobility Assistant (type 'done' to finish, 'state' to print state).");

        while (!ct.IsCancellationRequested)
        {
            System.Console.Write("\nYou: ");
            var user = System.Console.ReadLine() ?? "";

            if (string.Equals(user.Trim(), "done", StringComparison.OrdinalIgnoreCase))
            {
                MobilityEstimator.EstimateIfNeeded(_state, GetTotalAttendees());
                break;
            }

            if (string.Equals(user.Trim(), "state", StringComparison.OrdinalIgnoreCase))
            {
                PrintState();
                continue;
            }

            if (_awaitingConflictResolution && _pendingConflict is not null)
            {
                var resolution = TryResolveConflict(user, _pendingConflict);
                if (!resolution.Resolved)
                {
                    System.Console.WriteLine($"- {resolution.Reprompt}");
                    continue;
                }

                ApplyOverride(_pendingConflict.Key, resolution.Value!);
                _awaitingConflictResolution = false;

                AddTurn("user", user);
                AddTurn("assistant", $"Confirmed {_pendingConflict.Label} = {FormatValueForChat(resolution.Value!)}.");

                System.Console.WriteLine($"- ✅ Got it — we’ll use {FormatValueForChat(resolution.Value!)} for {_pendingConflict.Label} going forward.");

                var replay = _pendingUserMessageToReplay;
                _pendingUserMessageToReplay = null;
                _pendingConflict = null;

                if (!string.IsNullOrWhiteSpace(replay))
                {
                    user = replay!;
                }
                else
                {
                    continue;
                }
            }

            
            var conflict = DetectFirstConflict(user);
            if (conflict is not null)
            {
                _state.DetectedConflicts.Add(conflict.Message);
                _awaitingConflictResolution = true;
                _pendingConflict = conflict;
                _pendingUserMessageToReplay = user;

                System.Console.WriteLine($"- ⚠️ Inconsistency detected: {conflict.Message}");
                System.Console.WriteLine($"- {conflict.Question}");

                AddTurn("user", user);
                AddTurn("assistant", $"Inconsistency detected: {conflict.Message}");
                continue;
            }

            
            AddTurn("user", user);

            
            var missing = ComputeMissingSummary(_state);
            var policyQs = MissingDataPolicy.NextQuestions(_state, _baseline);
            if (policyQs.Count > 0)
                missing += " | Missing-data policy suggests asking: " + policyQs[0];

            
            var baselineJson = _baseline.RootElement.GetRawText();
            var currentStateJson = JsonSerializer.Serialize(_state, JsonOptionsPrettyFalse());
            var overridesJson = BuildSessionOverridesJson();
            var recentTurnsJson = BuildRecentTurnsJson();

            var args = new KernelArguments
            {
                ["BaselineJson"] = baselineJson,
                ["SessionOverridesJson"] = overridesJson,
                ["CurrentStateJson"] = currentStateJson,
                ["RecentTurnsJson"] = recentTurnsJson,
                ["MissingSummary"] = missing,
                ["UserMessage"] = user
            };

            var systemPrompt = await _promptBuilder.BuildAsync(
                templateName: MobilityPromptFileName,
                kernelArguments: args,
                assembly: GetType().Assembly,
                ct: ct);

            systemPrompt = WebUtility.HtmlDecode(systemPrompt);

            systemPrompt += """

IMPORTANT OUTPUT RULES (MUST FOLLOW):
- Return EXACTLY ONE JSON object matching the MobilityTurn schema.
- Do NOT wrap in markdown or code fences.
- Do NOT include any text before or after the JSON.
- If info is missing, still return valid JSON:
  - short_reply asks ONE best next question
  - recommendations max 2
  - capture only when confident (otherwise null)
""";

            WriteDebugFile("mobility_last_system_prompt.txt", systemPrompt);

            var raw = await CallOpenAiDirectAsync(systemPrompt, user, ct);
            WriteDebugFile("mobility_last_raw.txt", raw ?? "");

            if (string.IsNullOrWhiteSpace(raw))
            {
                System.Console.WriteLine("- I couldn’t get a model response. Check mobility_last_exception.txt");
                continue;
            }

            var jsonText = ExtractFirstJsonObject(raw);
            if (jsonText is null)
            {
                System.Console.WriteLine("- I couldn’t find a JSON object in the model output. Check mobility_last_raw.txt");
                continue;
            }

            MobilityTurn? turn;
            try
            {
                turn = JsonSerializer.Deserialize<MobilityTurn>(jsonText, JsonOptions());
            }
            catch
            {
                System.Console.WriteLine("- JSON found but couldn't be deserialized. Check mobility_last_raw.txt");
                continue;
            }

            if (turn is null)
            {
                System.Console.WriteLine("- JSON deserialized to null. Check mobility_last_raw.txt");
                continue;
            }

            ApplyCapture(turn, ref _state);

            var hardErrors = ValidateHardConstraints(_state);
            if (hardErrors.Count > 0)
            {
                foreach (var e in hardErrors) System.Console.WriteLine($"- ⚠️ {e}");
                System.Console.WriteLine("- Please correct the value(s) above.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(turn.ShortReply))
                System.Console.WriteLine(turn.ShortReply.Trim());
            else
                System.Console.WriteLine("- (No short_reply returned.)");

            if (turn.Recommendations is { Count: > 0 })
            {
                foreach (var r in turn.Recommendations.Take(2))
                {
                    var title = string.IsNullOrWhiteSpace(r.Title) ? "Suggestion" : r.Title.Trim();
                    var action = (r.Action ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(action))
                        System.Console.WriteLine($"- 💡 {title}: {action}");
                }
            }

            AddTurn("assistant", turn.ShortReply ?? "");

            if (IsCompleted(_state))
            {
                MobilityEstimator.EstimateIfNeeded(_state, GetTotalAttendees());
                System.Console.WriteLine("\n✅ Form completion looks complete. Type 'done' to finish or keep refining.");
                PrintFinalReport();
            }
        }

        System.Console.WriteLine("\nFinal output:");
        MobilityEstimator.EstimateIfNeeded(_state, GetTotalAttendees());
        PrintFinalReport();
    }



    private Conflict? DetectFirstConflict(string userMessage)
    {
        
        foreach (var rule in BuildConflictRules())
        {
            var mentioned = rule.Extract(userMessage);
            if (mentioned is null) continue;

            var reference = GetReferenceValue(rule.Key);
            if (reference is null) continue;

            if (!rule.AreEqual(reference, mentioned))
            {
                return new Conflict(
                    Key: rule.Key,
                    Label: rule.Label,
                    BaselineOrCurrent: reference,
                    Mentioned: mentioned,
                    Message: $"{rule.Label} is {FormatValueForChat(reference)}, but you mentioned {FormatValueForChat(mentioned)}.",
                    Question: rule.ConfirmationQuestion,
                    ParseResolution: rule.ParseResolution
                );
            }
        }

        return null;
    }

    private IEnumerable<ConflictRule> BuildConflictRules()
    {
        // NOTE: keys match BaselineForm.json structure:
        // - root keys: "eventName", "eventType" ...
        // - form keys: "Q2_EventName", "Q8_OnSiteParticipants", ...
        yield return new ConflictRule(
            Key: "eventName",
            Label: "Event name",
            Extract: msg => ExtractLabeledString(msg, "event name", "name"),
            AreEqual: (a, b) => StringEqualsNormalized(a, b),
            ConfirmationQuestion: "What is the correct EVENT NAME?",
            ParseResolution: msg => ExtractLabeledString(msg, "event name", "name") ?? msg.Trim()
        );

        yield return new ConflictRule(
            Key: "form.Q2_EventName",
            Label: "Q2 Event name",
            Extract: msg => ExtractLabeledString(msg, "event name", "name"),
            AreEqual: (a, b) => StringEqualsNormalized(a, b),
            ConfirmationQuestion: "What is the correct EVENT NAME?",
            ParseResolution: msg => ExtractLabeledString(msg, "event name", "name") ?? msg.Trim()
        );

        yield return new ConflictRule(
            Key: "form.Q4_EventDates",
            Label: "Q4 Event date",
            Extract: msg => ExtractEventDate(msg),
            AreEqual: (a, b) => StringEqualsNormalized(NormalizeDateString(a), NormalizeDateString(b)),
            ConfirmationQuestion: "What is the correct EVENT DATE? (e.g., 26/11/2025 or 2025-11-26)",
            ParseResolution: msg => ExtractEventDate(msg) ?? msg.Trim()
        );

        yield return new ConflictRule(
            Key: "form.Q5_EventType",
            Label: "Q5 Event type",
            Extract: msg => ExtractLabeledEnum(msg, new[] { "gala", "awards", "conference", "corporate", "festival", "trade show", "expo" }, "event type", "type"),
            AreEqual: (a, b) => StringEqualsNormalized(a, b),
            ConfirmationQuestion: "What is the correct EVENT TYPE?",
            ParseResolution: msg => ExtractLabeledEnum(msg, new[] { "gala", "awards", "conference", "corporate", "festival", "trade show", "expo" }, "event type", "type") ?? msg.Trim()
        );

        yield return new ConflictRule(
            Key: "form.Q6_EventScope",
            Label: "Q6 Event scope",
            Extract: msg => ExtractLabeledEnum(msg, new[] { "local", "regional", "national", "international" }, "scope"),
            AreEqual: (a, b) => StringEqualsNormalized(a, b),
            ConfirmationQuestion: "What is the correct EVENT SCOPE? (local/regional/national/international)",
            ParseResolution: msg => ExtractLabeledEnum(msg, new[] { "local", "regional", "national", "international" }, "scope") ?? msg.Trim()
        );

        yield return new ConflictRule(
            Key: "form.Q7_EventFormat",
            Label: "Q7 Event format",
            Extract: msg => ExtractLabeledEnum(msg, new[] { "live", "online", "hybrid", "in-person", "in person" }, "format"),
            AreEqual: (a, b) => StringEqualsNormalized(NormalizeFormat(a), NormalizeFormat(b)),
            ConfirmationQuestion: "What is the correct EVENT FORMAT? (Live/Online/Hybrid)",
            ParseResolution: msg => ExtractLabeledEnum(msg, new[] { "live", "online", "hybrid", "in-person", "in person" }, "format") ?? msg.Trim()
        );

        yield return new ConflictRule(
            Key: "form.Q8_OnSiteParticipants",
            Label: "Q8 On-site participants",
            Extract: msg => ExtractLabeledInt(msg, "on-site participants", "onsite participants", "in-person participants", "participants"),
            AreEqual: (a, b) => IntEquals(a, b),
            ConfirmationQuestion: "What is the correct TOTAL number of ON-SITE participants?",
            ParseResolution: msg => ExtractAnyInteger(msg)
        );

        yield return new ConflictRule(
            Key: "form.Q9_OnlineParticipants",
            Label: "Q9 Online participants",
            Extract: msg => ExtractLabeledInt(msg, "online participants", "remote participants"),
            AreEqual: (a, b) => IntEquals(a, b),
            ConfirmationQuestion: "What is the correct TOTAL number of ONLINE participants?",
            ParseResolution: msg => ExtractAnyInteger(msg)
        );

        yield return new ConflictRule(
            Key: "form.Q15_InstallationDays",
            Label: "Q15 Installation days",
            Extract: msg => ExtractLabeledInt(msg, "installation days", "setup days", "days for setup"),
            AreEqual: (a, b) => IntEquals(a, b),
            ConfirmationQuestion: "What is the correct number of INSTALLATION/SETUP days?",
            ParseResolution: msg => ExtractAnyInteger(msg)
        );

        yield return new ConflictRule(
            Key: "form.Q16_InstallationStaffFTE",
            Label: "Q16 Installation staff (FTE)",
            Extract: msg => ExtractLabeledInt(msg, "installation staff", "setup staff", "people required for the setup"),
            AreEqual: (a, b) => IntEquals(a, b),
            ConfirmationQuestion: "What is the correct number of people for INSTALLATION/SETUP?",
            ParseResolution: msg => ExtractAnyInteger(msg)
        );

        yield return new ConflictRule(
            Key: "form.Q17_DisassemblyDays",
            Label: "Q17 Disassembly days",
            Extract: msg => ExtractLabeledInt(msg, "disassembly days", "dismantling days", "days for dismantling"),
            AreEqual: (a, b) => IntEquals(a, b),
            ConfirmationQuestion: "What is the correct number of DISASSEMBLY/DISMANTLING days?",
            ParseResolution: msg => ExtractAnyInteger(msg)
        );

        yield return new ConflictRule(
            Key: "form.Q18_DisassemblyStaffFTE",
            Label: "Q18 Disassembly staff (FTE)",
            Extract: msg => ExtractLabeledInt(msg, "disassembly staff", "dismantling staff", "people required for the dismantling"),
            AreEqual: (a, b) => IntEquals(a, b),
            ConfirmationQuestion: "What is the correct number of people for DISASSEMBLY/DISMANTLING?",
            ParseResolution: msg => ExtractAnyInteger(msg)
        );
    }

    private (bool Resolved, object? Value, string Reprompt) TryResolveConflict(string userInput, Conflict conflict)
    {

        var s = userInput.Trim();
        if (string.IsNullOrWhiteSpace(s))
            return (false, null, conflict.Question);

        var lower = s.ToLowerInvariant();
        if (lower.Contains("baseline") || lower.Contains("keep the baseline") || lower.Contains("keep baseline"))
        {
            return (true, conflict.BaselineOrCurrent, conflict.Question);
        }

        var parsed = conflict.ParseResolution(s);
        if (parsed is not null)
            return (true, parsed, conflict.Question);

        if (conflict.Mentioned is string)
            return (true, s, conflict.Question);

        return (false, null, conflict.Question);
    }

    private object? GetReferenceValue(string key)
    {
    
        if (_overrides.TryGetValue(key, out var ov) && ov is not null)
            return ov;

        return TryGetBaselineValue(key);
    }

    private void ApplyOverride(string key, object value)
    {
        if (value is string str)
        {
            if (key.EndsWith("Q4_EventDates", StringComparison.OrdinalIgnoreCase))
                value = NormalizeDateString(str);
            if (key.EndsWith("Q7_EventFormat", StringComparison.OrdinalIgnoreCase))
                value = NormalizeFormat(str);
        }

        _overrides[key] = value;

        if (key.EndsWith("Q8_OnSiteParticipants", StringComparison.OrdinalIgnoreCase) && value is int i)
        {
            _overrides["total_attendees"] = i;
        }
    }

    private int? GetTotalAttendees()
    {
        if (_overrides.TryGetValue("total_attendees", out var a) && a is int ai) return ai;
        if (_overrides.TryGetValue("form.Q8_OnSiteParticipants", out var b) && b is int bi) return bi;

        var baseVal = TryGetBaselineValue("form.Q8_OnSiteParticipants");
        return baseVal is int n ? n : null;
    }

    private object? TryGetBaselineValue(string key)
    {
        try
        {
            var root = _baseline.RootElement;

            if (!key.Contains('.', StringComparison.Ordinal))
            {
                if (root.TryGetProperty(key, out var p))
                    return JsonElementToObject(p);
                return null;
            }

            var parts = key.Split('.', 2);
            if (parts.Length != 2) return null;

            var section = parts[0];
            var field = parts[1];

            if (!root.TryGetProperty(section, out var sec)) return null;
            if (!sec.TryGetProperty(field, out var p2)) return null;

            return JsonElementToObject(p2);
        }
        catch
        {
            return null;
        }
    }

    private static object? JsonElementToObject(JsonElement e)
    {
        return e.ValueKind switch
        {
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Number => e.TryGetInt32(out var i) ? i : e.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => e.GetRawText()
        };
    }

    private static bool StringEqualsNormalized(object a, object b)
    {
        var sa = (a?.ToString() ?? "").Trim();
        var sb = (b?.ToString() ?? "").Trim();
        return string.Equals(sa, sb, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IntEquals(object a, object b)
    {
        if (a is int ai && b is int bi) return ai == bi;
        if (a is string sa && int.TryParse(sa.Replace(",", ""), out var ax) && b is int by) return ax == by;
        if (b is string sb && int.TryParse(sb.Replace(",", ""), out var bx) && a is int ay) return ay == bx;
        return string.Equals(a?.ToString(), b?.ToString(), StringComparison.OrdinalIgnoreCase);
    }


    private static string? ExtractLabeledString(string text, params string[] labels)
    {

        foreach (var lab in labels)
        {
            var pattern = $@"(?i)(?:\b{Regex.Escape(lab)}\b)\s*[:=]\s*(.+?)(?:[,}}\n\r]|$)";
            var m = Regex.Match(text, pattern);
            if (m.Success)
            {
                var v = m.Groups[1].Value.Trim();
                v = v.Trim('"', '\'', ' ');
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
        }
        return null;
    }

    private static object? ExtractLabeledInt(string text, params string[] labels)
    {
        foreach (var lab in labels)
        {
            var pattern = $@"(?i)(?:\b{Regex.Escape(lab)}\b).{{0,30}}?\b(\d{{1,9}})\b";
            var m = Regex.Match(text.Replace(",", ""), pattern);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n))
                return n;
        }
        return null;
    }

    private static string? ExtractEventDate(string text)
    {
        var m1 = Regex.Match(text, @"\b(\d{2})/(\d{2})/(\d{4})\b");
        if (m1.Success)
            return $"{m1.Groups[1].Value}/{m1.Groups[2].Value}/{m1.Groups[3].Value}";

        var m2 = Regex.Match(text, @"\b(\d{4})-(\d{2})-(\d{2})\b");
        if (m2.Success)
            return $"{m2.Groups[1].Value}-{m2.Groups[2].Value}-{m2.Groups[3].Value}";

        var labeled = ExtractLabeledString(text, "event date", "date");
        if (labeled is null) return null;

        if (DateTime.TryParse(labeled, CultureInfo.GetCultureInfo("fr-BE"), DateTimeStyles.None, out var dt))
            return dt.ToString("yyyy-MM-dd");
        if (DateTime.TryParse(labeled, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            return dt.ToString("yyyy-MM-dd");

        return null;
    }

    private static string NormalizeDateString(object v)
    {
        var s = (v?.ToString() ?? "").Trim();
        if (string.IsNullOrWhiteSpace(s)) return s;

        if (DateTime.TryParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt.ToString("yyyy-MM-dd");

        if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            return dt.ToString("yyyy-MM-dd");

        if (DateTime.TryParse(s, CultureInfo.GetCultureInfo("fr-BE"), DateTimeStyles.None, out dt))
            return dt.ToString("yyyy-MM-dd");

        return s;
    }

    private static string NormalizeFormat(object v)
    {
        var s = (v?.ToString() ?? "").Trim().ToLowerInvariant();
        if (s is "in person" or "in-person") return "Live";
        if (s == "live") return "Live";
        if (s == "online") return "Online";
        if (s == "hybrid") return "Hybrid";
        return v?.ToString() ?? "";
    }

    private static string? ExtractLabeledEnum(string text, IEnumerable<string> allowed, params string[] labels)
    {
        var lower = text.ToLowerInvariant();

        var labelSeen = labels.Any(l => lower.Contains(l.ToLowerInvariant()));
        if (!labelSeen)
        {
            var maybe = ExtractLabeledString(text, labels);
            if (maybe is null) return null;
            lower = maybe.ToLowerInvariant();
        }

        foreach (var a in allowed)
        {
            var token = a.ToLowerInvariant();
            if (Regex.IsMatch(lower, $@"\b{Regex.Escape(token)}\b", RegexOptions.IgnoreCase))
                return a;
        }

        var labeled = ExtractLabeledString(text, labels);
        if (labeled is null) return null;

        var cleaned = labeled.Trim();
        if (cleaned.Length <= 40) return cleaned;

        return null;
    }

    private static int? ExtractAnyInteger(string text)
    {
        var cleaned = text.Replace(",", "");
        var m = Regex.Match(cleaned, @"\b(\d{1,9})\b");
        if (!m.Success) return null;
        return int.TryParse(m.Groups[1].Value, out var n) ? n : null;
    }

    private static string FormatValueForChat(object v)
    {
        return v switch
        {
            null => "(null)",
            string s => s,
            int i => i.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => v.ToString() ?? ""
        };
    }

    private record Conflict(
        string Key,
        string Label,
        object BaselineOrCurrent,
        object Mentioned,
        string Message,
        string Question,
        Func<string, object?> ParseResolution
    );

    private record ConflictRule(
        string Key,
        string Label,
        Func<string, object?> Extract,
        Func<object, object, bool> AreEqual,
        string ConfirmationQuestion,
        Func<string, object?> ParseResolution
    );



    // OpenAI call : 


    private async Task<string?> CallOpenAiDirectAsync(string systemPrompt, string userMessage, CancellationToken ct)
    {
        try
        {
            var messages = new ChatMessage[]
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userMessage)
            };

            var result = await _chatClient.CompleteChatAsync(messages, cancellationToken: ct);
            var completion = result.Value;

            var contentProp = completion.GetType().GetProperty("Content");
            if (contentProp?.GetValue(completion) is System.Collections.IEnumerable parts)
            {
                var sb = new StringBuilder();
                foreach (var part in parts)
                {
                    if (part is null) continue;
                    var textProp = part.GetType().GetProperty("Text");
                    var text = textProp?.GetValue(part) as string;
                    if (!string.IsNullOrWhiteSpace(text))
                        sb.Append(text);
                }

                var joined = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(joined))
                    return joined;
            }

            var outputTextProp =
                completion.GetType().GetProperty("OutputText")
                ?? completion.GetType().GetProperty("Text");

            var outputText = outputTextProp?.GetValue(completion) as string;
            if (!string.IsNullOrWhiteSpace(outputText))
                return outputText.Trim();

            var s = completion.ToString();
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }
        catch (Exception ex)
        {
            WriteDebugFile("mobility_last_exception.txt", $"{ex.GetType().Name}: {ex.Message}\n{ex}");
            return null;
        }
    }



    private void LoadBaseline()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Samples", "MobilityAssistant", "BaselineForm.json");
        if (!File.Exists(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), "Samples", "MobilityAssistant", "BaselineForm.json");

        var json = File.ReadAllText(path);
        _baseline = JsonDocument.Parse(json);

        
        var baseAtt = TryGetBaselineValue("form.Q8_OnSiteParticipants");
        if (baseAtt is int i) _overrides["total_attendees"] = i;
    }

    private void AddTurn(string role, string content)
    {
        _recentTurns.Add(new Turn(role, content));
        if (_recentTurns.Count > RecentTurnsMax)
            _recentTurns.RemoveRange(0, _recentTurns.Count - RecentTurnsMax);
    }

    private string BuildRecentTurnsJson()
    {
        var arr = _recentTurns.Select(t => new { role = t.Role, content = t.Content }).ToArray();
        return JsonSerializer.Serialize(arr, JsonOptionsPrettyFalse());
    }

    private string BuildSessionOverridesJson()
    {
        var send = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (_overrides.TryGetValue("total_attendees", out var a))
            send["total_attendees"] = a;

        foreach (var kv in _overrides)
        {
            // don’t duplicate
            if (string.Equals(kv.Key, "total_attendees", StringComparison.OrdinalIgnoreCase))
                continue;

            send[kv.Key] = kv.Value;
        }

        return JsonSerializer.Serialize(send, JsonOptionsPrettyFalse());
    }

    private static void WriteDebugFile(string filename, string content)
    {
        try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), filename), content); }
        catch { /* ignore */ }
    }

    private static JsonSerializerOptions JsonOptions() => new() { PropertyNameCaseInsensitive = true };
    private static JsonSerializerOptions JsonOptionsPrettyFalse() => new() { WriteIndented = false };

    private static string? ExtractFirstJsonObject(string text)
    {
        text = text.Trim();

        text = text.Replace("```json", "```").Replace("```JSON", "```");
        if (text.StartsWith("```"))
        {
            var end = text.LastIndexOf("```", StringComparison.Ordinal);
            if (end > 0) text = text.Substring(3, end - 3).Trim();
        }

        var start = text.IndexOf('{');
        if (start < 0) return null;

        var inString = false;
        var escape = false;
        var depth = 0;

        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (c == '\\')
            {
                if (inString) escape = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString) continue;

            if (c == '{') depth++;
            else if (c == '}') depth--;

            if (depth == 0)
                return text.Substring(start, i - start + 1);
        }

        return null;
    }

    private sealed record Turn(string Role, string Content);


    private static string ComputeMissingSummary(MobilityFormState state)
    {
        var missing = new List<string>();

        if (state.Q23_Participants.Count == 0) missing.Add("Q23 participants segments missing");
        if (state.Q24_Freight.Count == 0) missing.Add("Q24 freight items missing");
        if (state.Q26_Staff.Count == 0) missing.Add("Q26 staff segments missing");

        if (state.Q27_ParticipantsInformed.Value is null) missing.Add("Q27 informed? missing");
        if (state.Q28_AlternativeToCarsAction.Value is null) missing.Add("Q28 action missing");
        if (state.Q29_SecuredBikeParking.Value is null) missing.Add("Q29 bike parking missing");
        if (state.Q30_AccommodationBooked.Value is null) missing.Add("Q30 accommodation missing");

        return missing.Count == 0 ? "No missing fields." : string.Join("; ", missing);
    }

    private static bool IsCompleted(MobilityFormState state)
    {
        return state.Q23_Participants.Count > 0
               && state.Q24_Freight.Count > 0
               && state.Q26_Staff.Count > 0
               && state.Q27_ParticipantsInformed.Value is not null
               && state.Q28_AlternativeToCarsAction.Value is not null
               && state.Q29_SecuredBikeParking.Value is not null
               && state.Q30_AccommodationBooked.Value is not null;
    }

    private void PrintState()
    {
        System.Console.WriteLine(JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true }));
        System.Console.WriteLine("\n--- SESSION OVERRIDES ---");
        System.Console.WriteLine(BuildSessionOverridesJson());
        System.Console.WriteLine("\n--- RECENT TURNS ---");
        System.Console.WriteLine(BuildRecentTurnsJson());
    }

    private void PrintFinalReport()
    {
        var co2 = Co2Calculator.Compute(_state);

        System.Console.WriteLine("\n--- FORM STATE (JSON) ---");
        System.Console.WriteLine(JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true }));

        System.Console.WriteLine("\n--- CO2 BREAKDOWN (kg CO2e) ---");
        System.Console.WriteLine(JsonSerializer.Serialize(co2, new JsonSerializerOptions { WriteIndented = true }));
    }



    private static void ApplyCapture(MobilityTurn turn, ref MobilityFormState state)
    {
        if (turn.Capture?.Q23ParticipantSegment is not null)
        {
            var s = turn.Capture.Q23ParticipantSegment;
            state.Q23_Participants.Add(new ParticipantSegment(
                new Provenanced<OriginCategory>(ParseOrigin(s.Origin), DataProvenance.UserProvided),
                new Provenanced<TransportMode>(ParseMode(s.Mode), DataProvenance.UserProvided),
                new Provenanced<int>(s.Count, DataProvenance.UserProvided),
                new Provenanced<double>(s.DistanceKm, DataProvenance.UserProvided)
            ));
        }

        if (turn.Capture?.Q24FreightItem is not null)
        {
            var f = turn.Capture.Q24FreightItem;

            var defaultTrips = f.RoundTrips <= 0 ? 4 : f.RoundTrips;
            var tripsProv = f.RoundTrips <= 0 ? DataProvenance.AiEstimated : DataProvenance.UserProvided;

            state.Q24_Freight.Add(new FreightItem(
                new Provenanced<FreightUnit>(ParseFreightUnit(f.Unit), DataProvenance.UserProvided),
                new Provenanced<string>(f.Supplier ?? "", DataProvenance.UserProvided),
                new Provenanced<string>(f.Description ?? "", DataProvenance.UserProvided),
                new Provenanced<FreightMode>(ParseFreightMode(f.Mode), DataProvenance.UserProvided),
                new Provenanced<double?>(f.WeightOrQty, DataProvenance.UserProvided),
                new Provenanced<double>(f.DistanceKm, DataProvenance.UserProvided),
                new Provenanced<int>(defaultTrips, tripsProv)
            ));
        }

        if (turn.Capture?.Q26StaffSegment is not null)
        {
            var st = turn.Capture.Q26StaffSegment;
            state.Q26_Staff.Add(new StaffSegment(
                new Provenanced<TransportMode>(ParseMode(st.Mode), DataProvenance.UserProvided),
                new Provenanced<int>(st.Count, DataProvenance.UserProvided),
                new Provenanced<double>(st.DistanceKm, DataProvenance.UserProvided)
            ));
        }

        if (turn.Capture is null) return;

        if (turn.Capture.Q27Informed is not null)
            state.Q27_ParticipantsInformed = new Provenanced<bool?>(turn.Capture.Q27Informed, DataProvenance.UserProvided);

        if (!string.IsNullOrWhiteSpace(turn.Capture.Q28Action))
            state.Q28_AlternativeToCarsAction =
                new Provenanced<AlternativeToCarsAction?>(ParseAltAction(turn.Capture.Q28Action!), DataProvenance.UserProvided);

        if (!string.IsNullOrWhiteSpace(turn.Capture.Q28OtherText))
            state.Q28_OtherActionText = new Provenanced<string?>(turn.Capture.Q28OtherText, DataProvenance.UserProvided);

        if (turn.Capture.Q29BikeParking is not null)
            state.Q29_SecuredBikeParking = new Provenanced<bool?>(turn.Capture.Q29BikeParking, DataProvenance.UserProvided);

        if (turn.Capture.Q30Accommodation is not null)
            state.Q30_AccommodationBooked = new Provenanced<bool?>(turn.Capture.Q30Accommodation, DataProvenance.UserProvided);
    }

    private static List<string> ValidateHardConstraints(MobilityFormState state)
    {
        var errors = new List<string>();

        foreach (var seg in state.Q23_Participants)
        {
            var count = TryGetProvInt(seg,
                "Count", "ParticipantCount", "Participants", "NbParticipants", "NumberOfParticipants", "Attendees", "People");
            var km = TryGetProvDouble(seg,
                "DistanceKm", "RoundTripDistanceKm", "RoundTripKm", "Distance", "Km");

            if (count is not null && count.Value < 0) errors.Add("Q23 participant count cannot be negative.");
            if (km is not null && km.Value < 0) errors.Add("Q23 distance_km cannot be negative.");
        }

        foreach (var item in state.Q24_Freight)
        {
            var km = TryGetProvDouble(item, "DistanceKm", "Distance", "Km");
            var trips = TryGetProvInt(item, "RoundTrips", "Trips", "NbTrips");
            var weightOrQty = TryGetProvNullableDouble(item, "WeightOrQty", "Weight", "Quantity", "Qty");

            if (km is not null && km.Value < 0) errors.Add("Q24 distance_km cannot be negative.");
            if (trips is not null && trips.Value < 0) errors.Add("Q24 round_trips cannot be negative.");
            if (weightOrQty is not null && weightOrQty.Value is not null && weightOrQty.Value.Value < 0)
                errors.Add("Q24 weight_or_qty cannot be negative.");
        }

        foreach (var seg in state.Q26_Staff)
        {
            var count = TryGetProvInt(seg, "Count", "StaffCount", "NbStaff", "NumberOfStaff", "People");
            var km = TryGetProvDouble(seg, "DistanceKm", "RoundTripDistanceKm", "RoundTripKm", "Distance", "Km");

            if (count is not null && count.Value < 0) errors.Add("Q26 staff count cannot be negative.");
            if (km is not null && km.Value < 0) errors.Add("Q26 distance_km cannot be negative.");
        }

        return errors.Distinct().ToList();
    }

    private static Provenanced<int>? TryGetProvInt(object obj, params string[] preferredNames)
    {
        var prop = FindProperty(obj, typeof(Provenanced<int>), preferredNames)
                   ?? obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                       .FirstOrDefault(p => p.PropertyType == typeof(Provenanced<int>));
        return prop?.GetValue(obj) as Provenanced<int>;
    }

    private static Provenanced<double>? TryGetProvDouble(object obj, params string[] preferredNames)
    {
        var prop = FindProperty(obj, typeof(Provenanced<double>), preferredNames)
                   ?? obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                       .FirstOrDefault(p => p.PropertyType == typeof(Provenanced<double>));
        return prop?.GetValue(obj) as Provenanced<double>;
    }

    private static Provenanced<double?>? TryGetProvNullableDouble(object obj, params string[] preferredNames)
    {
        var prop = FindProperty(obj, typeof(Provenanced<double?>), preferredNames)
                   ?? obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                       .FirstOrDefault(p => p.PropertyType == typeof(Provenanced<double?>));
        return prop?.GetValue(obj) as Provenanced<double?>;
    }

    private static PropertyInfo? FindProperty(object obj, Type propertyType, params string[] preferredNames)
    {
        var props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var name in preferredNames)
        {
            var p = props.FirstOrDefault(x =>
                x.PropertyType == propertyType &&
                string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (p is not null) return p;
        }
        return null;
    }

    private static OriginCategory ParseOrigin(string? s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        return s switch
        {
            "local" or "local_0_15km" => OriginCategory.Local_0_15km,
            "regional" or "regional_15_100km" => OriginCategory.Regional_15_100km,
            "national" => OriginCategory.National,
            "africa" => OriginCategory.Africa,
            "asia" => OriginCategory.Asia,
            "europe" => OriginCategory.Europe,
            "north america" or "northamerica" => OriginCategory.NorthAmerica,
            "south america" or "southamerica" => OriginCategory.SouthAmerica,
            "oceania" => OriginCategory.Oceania,
            _ => OriginCategory.National
        };
    }

    private static TransportMode ParseMode(string? s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        return s switch
        {
            "walk" or "foot" => TransportMode.Walk,
            "bike" or "bicycle" => TransportMode.Bike,
            "car" => TransportMode.Car,
            "electric car" or "ev" => TransportMode.ElectricCar,
            "carpool" => TransportMode.Carpool,
            "car sharing" or "carsharing" => TransportMode.CarSharing,
            "taxi" => TransportMode.Taxi,
            "bus" => TransportMode.Bus,
            "coach" => TransportMode.Coach,
            "train" => TransportMode.Train,
            "metro" => TransportMode.Metro,
            "tram" => TransportMode.Tram,
            "plane" or "flight" => TransportMode.Plane,
            "ship" or "boat" => TransportMode.Ship,
            _ => TransportMode.Other
        };
    }

    private static FreightUnit ParseFreightUnit(string? s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        return s switch
        {
            "kg" => FreightUnit.Kg,
            "m3" or "meter cube" or "cubicmeter" or "cubic meter" => FreightUnit.CubicMeter,
            "nb" or "number" => FreightUnit.NumberOfItems,
            _ => FreightUnit.Kg
        };
    }

    private static FreightMode ParseFreightMode(string? s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        return s switch
        {
            "van" => FreightMode.Van,
            "electric van" or "e-van" => FreightMode.ElectricVan,
            "truck 19t" or "19t" => FreightMode.Truck19T,
            "truck 40t" or "40t" => FreightMode.Truck40T,
            "flight" or "plane" => FreightMode.Flight,
            "ship" => FreightMode.Ship,
            _ => FreightMode.Other
        };
    }

    private static AlternativeToCarsAction ParseAltAction(string s)
    {
        s = s.Trim().ToLowerInvariant();
        return s switch
        {
            "no action" => AlternativeToCarsAction.NoAction,
            "bike or walking distance" => AlternativeToCarsAction.BikeOrWalkingDistance,
            "public transport promotion" => AlternativeToCarsAction.PublicTransportPromotion,
            "private group transport" => AlternativeToCarsAction.PrivateGroupTransport,
            "electric private group transport" => AlternativeToCarsAction.ElectricPrivateGroupTransport,
            "car sharing" => AlternativeToCarsAction.CarSharing,
            _ => AlternativeToCarsAction.Other
        };
    }
}

// JSON contracts 
public class MobilityTurn
{
    [JsonPropertyName("question_id")] public string QuestionId { get; set; } = "Q23";
    [JsonPropertyName("short_reply")] public string ShortReply { get; set; } = "";
    [JsonPropertyName("capture")] public MobilityCapture? Capture { get; set; }
    [JsonPropertyName("next_question")] public string NextQuestion { get; set; } = "Q23";
    [JsonPropertyName("recommendations")] public List<MobilityRecommendation> Recommendations { get; set; } = [];
}

public class MobilityRecommendation
{
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("why")] public string? Why { get; set; }
    [JsonPropertyName("action")] public string? Action { get; set; }
}

public class MobilityCapture
{
    [JsonPropertyName("q23_participant_segment")] public Q23Segment? Q23ParticipantSegment { get; set; }
    [JsonPropertyName("q24_freight_item")] public Q24Item? Q24FreightItem { get; set; }
    [JsonPropertyName("q26_staff_segment")] public Q26Segment? Q26StaffSegment { get; set; }

    [JsonPropertyName("q27_informed")] public bool? Q27Informed { get; set; }
    [JsonPropertyName("q28_action")] public string? Q28Action { get; set; }
    [JsonPropertyName("q28_other_text")] public string? Q28OtherText { get; set; }
    [JsonPropertyName("q29_bike_parking")] public bool? Q29BikeParking { get; set; }
    [JsonPropertyName("q30_accommodation")] public bool? Q30Accommodation { get; set; }
}

public class Q23Segment
{
    [JsonPropertyName("origin")] public string Origin { get; set; } = "national";
    [JsonPropertyName("mode")] public string Mode { get; set; } = "train";
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("distance_km")] public double DistanceKm { get; set; }
}

public class Q24Item
{
    [JsonPropertyName("unit")] public string Unit { get; set; } = "kg";
    [JsonPropertyName("supplier")] public string? Supplier { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("mode")] public string Mode { get; set; } = "van";
    [JsonPropertyName("weight_or_qty")] public double? WeightOrQty { get; set; }
    [JsonPropertyName("distance_km")] public double DistanceKm { get; set; }
    [JsonPropertyName("round_trips")] public int RoundTrips { get; set; }
}

public class Q26Segment
{
    [JsonPropertyName("mode")] public string Mode { get; set; } = "train";
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("distance_km")] public double DistanceKm { get; set; }
}
