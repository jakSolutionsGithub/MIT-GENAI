using SkAgentWorkFlowStarter.Console.Framework.Workflow.Models;

namespace SkAgentWorkFlowStarter.Console.Framework.Workflow;

public interface IWorkflowStep
{
    string Name { get; }
    Task<WorkflowStepResult> ExecuteAsync(WorkflowContext context, CancellationToken ct);
}
