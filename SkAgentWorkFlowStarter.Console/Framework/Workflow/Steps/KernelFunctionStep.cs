using Microsoft.SemanticKernel;
using SkAgentWorkFlowStarter.Console.Framework.Workflow.Models;

namespace SkAgentWorkFlowStarter.Console.Framework.Workflow.Steps;

public class KernelFunctionStep(
    string pluginName,
    string functionName,
    Func<WorkflowContext, KernelArguments> argumentFactory,
    string? displayName = null,
    string? resultKey = null) : IWorkflowStep
{
    public string Name { get; } = displayName ?? $"{pluginName}.{functionName}";

    public async Task<WorkflowStepResult> ExecuteAsync(WorkflowContext context, CancellationToken ct)
    {
        var arguments = argumentFactory(context);
        var function = context.Kernel.Plugins.GetFunction(pluginName, functionName);
        var functionResult = await context.Kernel.InvokeAsync(function, arguments, ct);
        var stringValue = functionResult?.GetValue<object>()?.ToString();

        if (!string.IsNullOrWhiteSpace(resultKey))
        {
            context.State[resultKey] = stringValue;
        }

        return new WorkflowStepResult(Name, stringValue, resultKey);
    }
}
