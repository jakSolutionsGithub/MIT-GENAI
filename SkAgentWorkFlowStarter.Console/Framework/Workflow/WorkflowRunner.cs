using Microsoft.SemanticKernel;
using SkAgentWorkFlowStarter.Console.Framework.Workflow.Models;

namespace SkAgentWorkFlowStarter.Console.Framework.Workflow;

public class WorkflowRunner(Kernel kernel)
{
    public async Task<WorkflowResult> RunAsync(WorkflowDefinition definition, CancellationToken ct)
    {
        var context = new WorkflowContext(kernel);
        var stepResults = new List<WorkflowStepResult>();

        foreach (var step in definition.Steps)
        {
            var result = await step.ExecuteAsync(context, ct);
            stepResults.Add(result);
        }

        return new WorkflowResult(definition.Name, stepResults, new Dictionary<string, object?>(context.State));
    }
}
