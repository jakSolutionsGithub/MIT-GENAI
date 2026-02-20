using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SkAgentWorkFlowStarter.Console.Framework.Agents.Models;
using SkAgentWorkFlowStarter.Console.Framework.Prompting;

namespace SkAgentWorkFlowStarter.Console.Framework.Agents.Abstractions;



public abstract class AgentBase<TVariables>(Kernel kernel, IPromptBuilder promptBuilder)
    : IAgent<TVariables>
{
    private readonly IChatCompletionService _chatService =
        kernel.GetRequiredService<IChatCompletionService>();

    private readonly ChatHistory _persistentHistory = new();

    protected abstract string PromptFileName { get; }
    protected abstract IEnumerable<KernelFunction> AuthorizedKernelFunctions { get; }
    protected virtual bool UsePersistentHistory => false;


    protected virtual int MaxAutoInvokeAttempts => 10;



    public void ResetHistory() => _persistentHistory.Clear();

    public async Task<AgentResponse> AskAsync(
        AgentRequest<TVariables> request,
        CancellationToken ct)
    {
        var (history, _) = await PrepareHistoryAsync(request, ct);

        var result = await _chatService.GetChatMessageContentAsync(
            history,
            BuildExecutionSettings(),
            kernel,
            ct);

        var content = result.Content ?? string.Empty;

        if (UsePersistentHistory)
        {
            _persistentHistory.AddUserMessage(request.UserMessage);
            _persistentHistory.AddAssistantMessage(content);
        }

        return new AgentResponse(content);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        AgentRequest<TVariables> request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var (history, _) = await PrepareHistoryAsync(request, ct);

        var sb = new StringBuilder();

        await foreach (var update in _chatService.GetStreamingChatMessageContentsAsync(
            history,
            BuildExecutionSettings(),
            kernel,
            ct))
        {
            var chunk = update.Content;
            if (string.IsNullOrEmpty(chunk)) continue;
            sb.Append(chunk);
            yield return chunk;
        }

       
        if (!UsePersistentHistory) yield break;
        _persistentHistory.AddUserMessage(request.UserMessage);
        _persistentHistory.AddAssistantMessage(sb.ToString());
    }


    protected IEnumerable<KernelFunction> GetKernelFunctionsForPlugin<T>()
        where T : class
    {
        var pluginName = typeof(T).Name.Split('`')[0];

        if (!kernel.Plugins.TryGetPlugin(pluginName, out var plugin))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AgentBase] WARNING: Plugin '{pluginName}' not registered in kernel. " +
                $"Verify AddFromObject(..., \"{pluginName}\") in SkConfiguration.");
            yield break;
        }

        foreach (var method in typeof(T).GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var attr = method.GetCustomAttribute<KernelFunctionAttribute>();
            if (attr is null) continue;

            var functionName = attr.Name ?? method.Name;

            if (plugin.TryGetFunction(functionName, out var fn))
                yield return fn;
            else
                System.Diagnostics.Debug.WriteLine(
                    $"[AgentBase] WARNING: Function '{functionName}' not found in plugin '{pluginName}'.");
        }
    }

    protected virtual KernelArguments CreateKernelArguments(TVariables? variables)
    {
        var args = new KernelArguments();
        if (variables is null) return args;

        foreach (var prop in typeof(TVariables)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            args[prop.Name] = prop.GetValue(variables);
        }

        return args;
    }



    private async Task<(ChatHistory history, string systemPrompt)> PrepareHistoryAsync(
        AgentRequest<TVariables> request,
        CancellationToken ct)
    {
        var kernelArguments = CreateKernelArguments(request.Variables);
        var systemPrompt    = await GetSystemPromptAsync(kernelArguments, ct);

        var history = new ChatHistory();
        history.AddSystemMessage(systemPrompt);

        if (UsePersistentHistory)
        {
            foreach (var msg in _persistentHistory)
            {
                if (msg.Role == AuthorRole.System) continue;
                history.AddMessage(msg.Role, msg.Content ?? string.Empty);
            }
        }

        history.AddUserMessage(request.UserMessage);
        return (history, systemPrompt);
    }

    private PromptExecutionSettings BuildExecutionSettings() =>
        new()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                AuthorizedKernelFunctions,
                options: new FunctionChoiceBehaviorOptions
                {
                    //MaximumAutoInvokeAttempts = MaxAutoInvokeAttempts
                })
        };

    private async ValueTask<string> GetSystemPromptAsync(
        KernelArguments args, CancellationToken ct)
        => await promptBuilder.BuildAsync(
            PromptFileName, args,
            assembly: GetType().Assembly,
            ct: ct);
}