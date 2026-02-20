namespace SkAgentWorkFlowStarter.Console.Framework.Memory;


public interface IAgentMemorySearch<TRecord>
    where TRecord : IAgentMemoryRecord
{

    Task<IReadOnlyList<AgentMemoryHit<TRecord>>> SearchAsync(
        string query,
        int topK = 5,
        CancellationToken ct = default);


    Task<TRecord?> GetByIdAsync(string id, CancellationToken ct = default);
}


public sealed record AgentMemoryHit<TRecord>(TRecord Record, double Score)
    where TRecord : IAgentMemoryRecord;