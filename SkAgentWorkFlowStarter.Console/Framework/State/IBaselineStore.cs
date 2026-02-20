namespace SkAgentWorkFlowStarter.Console.Framework.State;


public interface IBaselineStore<TBaseline>
{
    TBaseline GetBaseline();


    object? GetEffectiveValue(string fieldPath);


    void FlagConflict(
        string fieldPath,
        string fieldLabel,
        object baselineValue,
        object userMentionedValue,
        string questionToUser);


    void ResolveConflict(string fieldPath, object confirmedValue);

    
    BaselineConflict? GetPendingConflict();

    void Reset();
}


public sealed record BaselineConflict(
    string FieldPath,
    string FieldLabel,
    object BaselineValue,
    object UserMentionedValue,
    string QuestionToUser
);