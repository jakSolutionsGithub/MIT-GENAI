using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;

namespace SkAgentWorkFlowStarter.Console.Framework.State;


[Description(
    "Baseline reference data access and conflict resolution tools. " +
    "Use get_baseline to check reference data when validating user input. " +
    "Use flag_baseline_conflict when you detect a mismatch. " +
    "Use resolve_baseline_conflict after user confirms the correct value.")]
public sealed class BaselineConflictTools<TBaseline>(
    IBaselineStore<TBaseline> store,
    ILogger<BaselineConflictTools<TBaseline>>? logger = null)
{
    private readonly ILogger _logger =
        (ILogger?)logger ?? NullLogger.Instance;

    [KernelFunction("get_baseline")]
    [Description(
        "Returns the baseline reference data (readonly form, profile, or ticket). " +
        "Call this when you need to compare user input against reference data " +
        "to detect conflicts or validate values. " +
        "The baseline includes session overrides (user-confirmed corrections). " +
        "Do NOT call this every turn — only when needed for validation.")]
    public TBaseline GetBaseline()
    {
        _logger.LogInformation("[get_baseline] Returning baseline.");
        return store.GetBaseline();
    }

    [KernelFunction("get_baseline_field")]
    [Description(
        "Returns the effective value of a single baseline field " +
        "(session override if exists, else baseline). " +
        "Faster than get_baseline when you only need one field. " +
        "fieldPath examples: 'eventName', 'form.Q8_OnSiteParticipants', 'venue.city'. " +
        "Returns null if field not found.")]
    public object? GetBaselineField(
        [Description("Dot-separated path to the field (e.g. 'form.Q8_OnSiteParticipants').")]
        string fieldPath)
    {
        _logger.LogInformation("[get_baseline_field] path={Path}", fieldPath);
        return store.GetEffectiveValue(fieldPath);
    }

    [KernelFunction("flag_baseline_conflict")]
    [Description(
        "Call when you detect a conflict between baseline reference data and user input. " +
        "The conversation STOPS until the user resolves the conflict. " +
        "Example: baseline says 100 attendees, user says 1000. " +
        "After calling this, wait for the user to confirm the correct value, " +
        "then call resolve_baseline_conflict.")]
    public string FlagConflict(
        [Description("Dot-separated field path (e.g. 'form.Q8_OnSiteParticipants').")]
        string fieldPath,

        [Description("Human-readable field name (e.g. 'number of on-site participants').")]
        string fieldLabel,

        [Description("The value in the baseline.")]
        string baselineValue,

        [Description("The value the user mentioned.")]
        string userMentionedValue,

        [Description(
            "Natural-language question to ask the user. " +
            "Example: 'I see 100 in the form but you mentioned 1000. Which is correct?'")]
        string questionToUser)
    {
        _logger.LogWarning(
            "[flag_baseline_conflict] {Label}: baseline={Baseline} user={User}",
            fieldLabel, baselineValue, userMentionedValue);

        store.FlagConflict(
            fieldPath, fieldLabel, baselineValue, userMentionedValue, questionToUser);

        return $"Conflict flagged for {fieldLabel}. Conversation halted pending user confirmation.";
    }

    [KernelFunction("resolve_baseline_conflict")]
    [Description(
        "Resolves a pending baseline conflict by applying the user-confirmed value. " +
        "Call this after the user has answered the conflict question. " +
        "The confirmed value becomes a session override — all future baseline " +
        "lookups for this field will return the override, not the original baseline.")]
    public string ResolveConflict(
        [Description("Dot-separated field path (same as used in flag_baseline_conflict).")]
        string fieldPath,

        [Description("The value the user confirmed as correct.")]
        string confirmedValue)
    {
        _logger.LogInformation(
            "[resolve_baseline_conflict] {Path} = {Value}", fieldPath, confirmedValue);

        store.ResolveConflict(fieldPath, confirmedValue);

        return $"Conflict resolved. {fieldPath} is now {confirmedValue}.";
    }

    [KernelFunction("check_pending_conflict")]
    [Description(
        "Returns the currently pending conflict (if any). " +
        "If a conflict exists, you MUST wait for user confirmation before proceeding. " +
        "Do NOT call update_state or other tools until the conflict is resolved.")]
    public BaselineConflict? CheckPendingConflict()
    {
        var conflict = store.GetPendingConflict();
        if (conflict is not null)
        {
            _logger.LogInformation(
                "[check_pending_conflict] Pending: {Label}", conflict.FieldLabel);
        }
        return conflict;
    }
}