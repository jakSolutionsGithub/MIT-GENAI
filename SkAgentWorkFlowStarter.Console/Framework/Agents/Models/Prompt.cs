namespace SkAgentWorkFlowStarter.Console.Framework.Agents.Models;

public record Prompt
{
    public string Name { get; init; } = null!;
    public string Template { get; init; } = null!;
}
