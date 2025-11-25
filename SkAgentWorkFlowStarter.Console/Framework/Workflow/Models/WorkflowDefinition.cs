namespace SkAgentWorkFlowStarter.Console.Framework.Workflow.Models;

public record WorkflowDefinition(string Name, IReadOnlyList<IWorkflowStep> Steps);
