using Microsoft.SemanticKernel;
using SkAgentWorkFlowStarter.Console.Framework.Workflow.Models;
using SkAgentWorkFlowStarter.Console.Framework.Workflow.Steps;

namespace SkAgentWorkFlowStarter.Console.Framework.Workflow;

public class WorkflowBuilder
{
    private readonly string _name;
    private readonly List<IWorkflowStep> _steps = [];

    private WorkflowBuilder(string name)
    {
        _name = name;
    }

    public static WorkflowBuilder Create(string name) => new(name);

    public WorkflowBuilder ThenCallFunction(
        string pluginName,
        string functionName,
        Action<KernelArguments>? configureArguments = null,
        string? stepName = null,
        string? storeResultAs = null)
    {
        _steps.Add(new KernelFunctionStep(pluginName, functionName, _ =>
        {
            var args = new KernelArguments();
            configureArguments?.Invoke(args);
            return args;
        }, stepName, storeResultAs));

        return this;
    }

    public WorkflowBuilder ThenCallFunction(
        string pluginName,
        string functionName,
        Func<WorkflowContext, KernelArguments> argumentFactory,
        string? stepName = null,
        string? storeResultAs = null)
    {
        _steps.Add(new KernelFunctionStep(pluginName, functionName, argumentFactory, stepName, storeResultAs));
        return this;
    }

    public WorkflowDefinition Build() => new(_name, _steps);
}
