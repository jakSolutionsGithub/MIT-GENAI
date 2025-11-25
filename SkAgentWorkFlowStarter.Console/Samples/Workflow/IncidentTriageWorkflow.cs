using Microsoft.SemanticKernel;
using SkAgentWorkFlowStarter.Console.Framework.Workflow;
using SkAgentWorkFlowStarter.Console.Framework.Workflow.Models;
using SkAgentWorkFlowStarter.Console.Samples.Workflow.Tools;

namespace SkAgentWorkFlowStarter.Console.Samples.Workflow;

public class IncidentTriageWorkflow
{
    public WorkflowDefinition Definition { get; }

    public IncidentTriageWorkflow()
    {
        Definition = WorkflowBuilder
            .Create("Incident triage workflow")
            .ThenCallFunction(nameof(WorkflowTools), "fetch_incident", args => args["incidentId"] = 5012, "Fetch incident", "incident")
            .ThenCallFunction(
                nameof(WorkflowTools),
                "classify_incident",
                context =>
                {
                    var arguments = new KernelArguments();
                    var description = context.State.TryGetValue("incident", out var incidentState)
                        ? incidentState?.ToString()
                        : string.Empty;
                    arguments["description"] = description;
                    return arguments;
                },
                "Classify incident",
                "classification")
            .ThenCallFunction(
                nameof(WorkflowTools),
                "summarize_incident",
                context =>
                {
                    var arguments = new KernelArguments();
                    var incident = context.State.TryGetValue("incident", out var incidentState)
                        ? incidentState?.ToString()
                        : string.Empty;
                    var classification = context.State.TryGetValue("classification", out var classificationState)
                        ? classificationState?.ToString()
                        : "Unclassified";
                    arguments["incident"] = incident;
                    arguments["classification"] = classification;
                    return arguments;
                },
                "LLM summary",
                "summary")
            .ThenCallFunction(
                nameof(WorkflowTools),
                "assign_incident",
                context =>
                {
                    var arguments = new KernelArguments();
                    var description = context.State.TryGetValue("incident", out var incidentState)
                        ? incidentState?.ToString()
                        : string.Empty;
                    var classification = context.State.TryGetValue("classification", out var classificationState)
                        ? classificationState?.ToString()
                        : "Unclassified";
                    var summary = context.State.TryGetValue("summary", out var summaryState)
                        ? summaryState?.ToString()
                        : $"{description} | {classification}";
                    arguments["description"] = summary;
                    arguments["owner"] = "on-call-engineer";
                    return arguments;
                },
                "Assign owner",
                "assignment")
            .Build();
    }
}
