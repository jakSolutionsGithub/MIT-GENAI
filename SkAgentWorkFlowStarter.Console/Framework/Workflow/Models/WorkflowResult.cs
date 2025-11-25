namespace SkAgentWorkFlowStarter.Console.Framework.Workflow.Models;

public record WorkflowResult(string Name, IReadOnlyList<WorkflowStepResult> Steps, IReadOnlyDictionary<string, object?> State);

public record WorkflowStepResult(string StepName, string? Output, string? StoredAt = null);
