using Microsoft.SemanticKernel;

namespace SkAgentWorkFlowStarter.Console.Framework.Workflow.Models;

public class WorkflowContext
{
    public WorkflowContext(Kernel kernel)
    {
        Kernel = kernel;
        State = new Dictionary<string, object?>();
    }

    public Kernel Kernel { get; }
    public IDictionary<string, object?> State { get; }
}
