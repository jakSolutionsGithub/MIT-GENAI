using SkAgentWorkFlowStarter.Console.Framework.Workflow;
using SkAgentWorkFlowStarter.Console.Framework.Workflow.Models;
using SkAgentWorkFlowStarter.Console.Samples.Workflow;

namespace SkAgentWorkFlowStarter.Console.Samples.Services;

public interface IWorkflowSampleService
{
    Task<WorkflowResult> RunStaticWorkflow(CancellationToken ct);
}

public class WorkflowSampleService(WorkflowRunner workflowRunner, IncidentTriageWorkflow incidentTriageWorkflow) : IWorkflowSampleService
{
    public async Task<WorkflowResult> RunStaticWorkflow(CancellationToken ct)
    {
        return await workflowRunner.RunAsync(incidentTriageWorkflow.Definition, ct);
    }
}
