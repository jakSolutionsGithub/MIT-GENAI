using Microsoft.Extensions.VectorData;
using SkAgentWorkFlowStarter.Console.Framework.Memory;

namespace SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Historical.Models;


public sealed class MobilityHistoryRecord : IAgentMemoryRecord
{

    [VectorStoreKey]
    public string Id { get; set; } = Guid.NewGuid().ToString("n");


    [VectorStoreData]
    public string Title { get; set; } = string.Empty;


    [VectorStoreData]
    public string Content { get; set; } = string.Empty;


    [VectorStoreData]
    public string? StateJson { get; set; }


    [VectorStoreData]
    public string? TagsCsv { get; set; }

    [VectorStoreData]
    public string CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow.ToString("O");

    ///   text-embedding-ada-002  → 1536
    ///
    /// If you change models, update <see cref="MobilityVectorDimensions.ContentEmbedding"/>

    [VectorStoreVector(MobilityVectorDimensions.ContentEmbedding)]
    public ReadOnlyMemory<float> ContentEmbedding { get; set; }
}


public static class MobilityVectorDimensions
{

    public const int ContentEmbedding = 1536;
}