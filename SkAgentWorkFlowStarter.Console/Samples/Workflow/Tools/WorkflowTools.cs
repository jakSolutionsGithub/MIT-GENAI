using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using SkAgentWorkFlowStarter.Console.Framework.Agents.Models;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.SummarizationAgent;
using SkAgentWorkFlowStarter.Console.Samples.MyAgents.SummarizationAgent.Models;

namespace SkAgentWorkFlowStarter.Console.Samples.Workflow.Tools;

public class WorkflowTools(IServiceProvider serviceProvider)
{
    [KernelFunction("fetch_incident")]
    public string FetchIncident([Description("Incident identifier to load")] int incidentId) =>
        $"Incident {incidentId}: API outage reported by customer";

    [KernelFunction("classify_incident")]
    public string ClassifyIncident([Description("Incident description")] string description) =>
        $"Classification: High severity | Details: {description}";

    [KernelFunction("assign_incident")]
    public string AssignIncident(
        [Description("Incident description with classification or summary")] string description,
        [Description("Owner to assign")] string owner) =>
        $"Assigned to {owner} with note: {description}";

    [KernelFunction("summarize_incident")]
    public async Task<string> SummarizeIncident(
        [Description("Incident description")] string incident,
        [Description("Incident classification label")] string classification,
        CancellationToken cancellationToken = default)
    {
        var agent = serviceProvider.GetRequiredService<SummarizationAgent>();
        var response = await agent.AskAsync(
            new AgentRequest<SummarizationInput>(
                "Summarize incident for handoff.",
                new SummarizationInput(incident, classification)),
            cancellationToken);
        return response.Content ?? string.Empty;
    }
}
