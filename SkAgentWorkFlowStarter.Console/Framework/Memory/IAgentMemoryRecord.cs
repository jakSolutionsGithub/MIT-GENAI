namespace SkAgentWorkFlowStarter.Console.Framework.Memory;


public interface IAgentMemoryRecord
{
    string Id { get; }


    string Title { get; }


    string Content { get; }


    string? StateJson { get; }


    string? TagsCsv { get; }

    
    string CreatedAtUtc { get; }
}