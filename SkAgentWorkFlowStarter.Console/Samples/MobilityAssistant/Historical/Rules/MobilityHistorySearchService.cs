using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using SkAgentWorkFlowStarter.Console.Framework.Memory;
using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Historical.Models;

namespace SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Historical.Rules;

public sealed class MobilityHistorySearchService : IAgentMemorySearch<MobilityHistoryRecord>
{
    private readonly InMemoryVectorStore _vectorStore;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ILogger<MobilityHistorySearchService> _logger;

    public MobilityHistorySearchService(
        InMemoryVectorStore vectorStore,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger<MobilityHistorySearchService>? logger = null)
    {
        _vectorStore = vectorStore;
        _embeddingGenerator = embeddingGenerator;
        _logger = logger ?? NullLogger<MobilityHistorySearchService>.Instance;
    }

    public async Task<IReadOnlyList<AgentMemoryHit<MobilityHistoryRecord>>> SearchAsync(
        string query,
        int topK = 5,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("[MobilityHistorySearchService] Empty query — returning no results.");
            return [];
        }

        topK = Math.Clamp(topK, 1, 20);

        _logger.LogInformation(
            "[MobilityHistorySearchService] Searching topK={TopK} | query={Query}",
            topK, query);

        var collection = _vectorStore.GetCollection<string, MobilityHistoryRecord>(
            MobilityHistoryIngestionService.CollectionName);

        var embedding = await _embeddingGenerator
            .GenerateAsync(query, cancellationToken: ct)
            .ConfigureAwait(false);

        var hits = new List<AgentMemoryHit<MobilityHistoryRecord>>(capacity: topK);

        await foreach (var result in collection.SearchAsync(embedding.Vector, topK, null, ct)
            .ConfigureAwait(false))
        {
            if (result.Record is null) continue;

            hits.Add(new AgentMemoryHit<MobilityHistoryRecord>(
                Record: result.Record,
                Score: result.Score ?? 0.0));
        }

        _logger.LogInformation(
            "[MobilityHistorySearchService] Found {Count} hit(s) for query: {Query}",
            hits.Count, query);

        return hits;
    }

    public async Task<MobilityHistoryRecord?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var collection = _vectorStore.GetCollection<string, MobilityHistoryRecord>(
            MobilityHistoryIngestionService.CollectionName);

        try
        {
            return await collection.GetAsync(id, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[MobilityHistorySearchService] GetByIdAsync failed for id={Id}.", id);
            return null;
        }
    }
}
