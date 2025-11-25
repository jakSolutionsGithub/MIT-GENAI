namespace SkAgentWorkFlowStarter.Console.Framework.Agents.Models;

public record AgentRequest<TVariables>(string UserMessage, TVariables? Variables);
