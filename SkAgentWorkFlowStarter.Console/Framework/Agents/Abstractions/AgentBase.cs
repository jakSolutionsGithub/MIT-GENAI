using System.Reflection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SkAgentWorkFlowStarter.Console.Framework.Agents.Models;
using SkAgentWorkFlowStarter.Console.Framework.Prompting;

namespace SkAgentWorkFlowStarter.Console.Framework.Agents.Abstractions;

public abstract class AgentBase<TVariables>(Kernel kernel, IPromptBuilder promptBuilder) : IAgent<TVariables>
{
    private readonly IChatCompletionService _chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
    protected abstract string PromptFileName { get; }
    protected abstract IEnumerable<KernelFunction> AuthorizedKernelFunctions { get; }

    public async Task<AgentResponse> AskAsync(AgentRequest<TVariables> request, CancellationToken ct)
    {
        var history = new ChatHistory();
        var kernelArguments = CreateKernelArguments(request.Variables);

        history.AddSystemMessage(await GetSystemPromptAsync(kernelArguments, ct));
        history.AddUserMessage(request.UserMessage);
        return await AskAsync(history, ct);
    }

    private async Task<AgentResponse> AskAsync(ChatHistory history, CancellationToken ct)
    {
        var result = await _chatCompletionService.GetChatMessageContentAsync(
            history,
            new PromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(AuthorizedKernelFunctions,
                    options: new FunctionChoiceBehaviorOptions())
            },
            kernel,
            ct);
        return new AgentResponse(result.Content);
    }

    protected IEnumerable<KernelFunction> GetKernelFunctionsForPlugin<T>()
        where T : class
    {
        var methods = typeof(T).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        foreach (var method in methods)
        {
            var kernelFunctionAttribute = method.GetCustomAttribute<KernelFunctionAttribute>();

            if (kernelFunctionAttribute == null) continue;
            var functionName = kernelFunctionAttribute.Name ?? method.Name;

            var kernelFunction = kernel.Plugins.GetFunction(typeof(T).Name, functionName);
            yield return kernelFunction;
        }
    }

    private async ValueTask<string> GetSystemPromptAsync(KernelArguments kernelArguments, CancellationToken ct)
    {
        return await promptBuilder.BuildAsync(PromptFileName, kernelArguments,
            assembly: GetType().Assembly, ct: ct);
    }

    protected virtual KernelArguments CreateKernelArguments(TVariables? variables)
    {
        var args = new KernelArguments();

        if (variables is null) return args;

        foreach (var prop in typeof(TVariables).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = prop.GetValue(variables);
            args[prop.Name] = value;
        }

        return args;
    }
}
