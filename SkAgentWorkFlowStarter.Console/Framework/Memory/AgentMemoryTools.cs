using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;

namespace SkAgentWorkFlowStarter.Console.Framework.Memory;


public sealed class AgentMemoryTools<TRecord>(
    IAgentMemorySearch<TRecord> search,
    IAgentMemoryIngestion<TRecord> ingestion,
    ILogger<AgentMemoryTools<TRecord>>? logger = null)
    where TRecord : IAgentMemoryRecord
{
    private const double DefaultMinScore = 0.65;
    private const int EvidenceMaxChars = 400;

    private readonly ILogger _logger =
        (ILogger?)logger ?? NullLogger.Instance;


    public sealed record MemoryHitDto(
        string Id,
        string Title,
        string EvidenceSnippet,
        double Score,
        string? StateJson,
        string? TagsCsv,
        string CreatedAtUtc
    );

    public sealed record MemorySearchResultDto(
        string QueryUsed,
        IReadOnlyList<MemoryHitDto> Hits,
        int TotalRetrieved
    );

    public sealed record SimilarRecordHitDto(
        string Id,
        string Title,
        double SimilarityScore,
        string EvidenceSnippet,
        string? StateJson,
        string? PinnedValueJson
    );

    public sealed record SimilarRecordResultDto(
        string QueryUsed,
        IReadOnlyList<SimilarRecordHitDto> Hits,
        int TotalHitsBeforeFilter
    );


    [KernelFunction("search_history")]
    [Description(
        "Semantic search over historical case records. " +
        "Use before making assumptions, suggesting defaults, or validating estimates. " +
        "Compose a rich natural-language query — include domain context, scale, " +
        "constraints, and what you are looking for. " +
        "Do NOT pass raw JSON as the query. " +
        "If Hits is empty: broaden the query or increase topK.")]
    public async Task<MemorySearchResultDto> SearchHistoryAsync(
        [Description(
            "Natural-language prose query with full context. " +
            "Example for mobility: 'corporate event Brussels 300 attendees train and car, " +
            "typical participant distance to venue'.")]
        string query,

        [Description("Number of candidates to retrieve (1–10). Start with 5.")]
        int topK = 5,

        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new MemorySearchResultDto(query ?? "", [], 0);

        topK = Math.Clamp(topK, 1, 10);

        _logger.LogInformation("[search_history] topK={TopK} query={Query}", topK, query);

        var hits = await search.SearchAsync(query, topK, ct).ConfigureAwait(false);

        var dtos = hits
            .Select(h => new MemoryHitDto(
                Id:              h.Record.Id,
                Title:           h.Record.Title,
                EvidenceSnippet: TrimToSentence(h.Record.Content, EvidenceMaxChars),
                Score:           h.Score,
                StateJson:       h.Record.StateJson,
                TagsCsv:         h.Record.TagsCsv,
                CreatedAtUtc:    h.Record.CreatedAtUtc))
            .ToList();

        return new MemorySearchResultDto(
            QueryUsed:      query,
            Hits:           dtos,
            TotalRetrieved: dtos.Count);
    }

    [KernelFunction("ingest_history")]
    [Description(
        "Saves a case outcome to memory for future recall. " +
        "Call when the user confirms a plan or a stable case outcome is reached. " +
        "Write Content as natural-language prose — NOT raw JSON. " +
        "Include: domain context, scale, key metrics, decisions made, what worked. " +
        "Structured state is snapshotted separately via includeCurrentState. " +
        "Returns the new record ID.")]
    public async Task<string> IngestHistoryAsync(
        [Description("Short human-readable label for this case.")]
        string title,

        [Description(
            "Natural-language prose summary — this is what gets embedded. " +
            "Write to be semantically searchable. Do NOT put raw JSON here.")]
        string content,

        [Description("Optional CSV tags for filtering (e.g. 'corporate,brussels,train').")]
        string? tagsCsv = null,

        [Description("Raw JSON of the current domain state to store as sidecar (not embedded).")]
        string? currentStateJson = null,

        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("content cannot be empty.", nameof(content));

        if (string.IsNullOrWhiteSpace(title)) title = "Case record";

        _logger.LogInformation("[ingest_history] title='{Title}'", title);

        var id = await ingestion.IngestAsync(
            title.Trim(),
            content.Trim(),
            tagsCsv?.Trim(),
            currentStateJson,
            ct).ConfigureAwait(false);

        return id;
    }

    [KernelFunction("search_similar_records")]
    [Description(
        "Searches for similar historical records to suggest a value for a missing parameter. " +
        "Use when the user does not know a specific value. " +
        "Compose a rich natural-language query from all available context. " +
        "Use pinJsonPath to auto-extract a specific field from matching records. " +
        "Returns TotalHitsBeforeFilter: if > 0 but Hits empty, retry with minScore=0.50. " +
        "If TotalHitsBeforeFilter = 0, try broader query terms. " +
        "Do NOT update state with a suggested value until the user confirms.")]
    public async Task<SimilarRecordResultDto> SearchSimilarRecordsAsync(
        [Description(
            "Natural-language query with full available context. " +
            "Include: domain type, location, scale, constraints, and the missing parameter name.")]
        string searchQuery,

        [Description("Number of candidates to retrieve (1–10). Default 5.")]
        int topK = 5,

        [Description(
            "Minimum similarity score (0.0–1.0). Default 0.65. " +
            "Lower to 0.50 if Hits is empty but TotalHitsBeforeFilter > 0.")]
        double minScore = DefaultMinScore,

        [Description(
            "Optional camelCase JSON path to auto-extract a scalar value from each hit's StateJson. " +
            "Example: 'structured.participantSegments[0].distanceKm'. " +
            "Leave null to receive full StateJson for LLM-side extraction.")]
        string? pinJsonPath = null,

        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
            throw new ArgumentException("searchQuery cannot be empty.", nameof(searchQuery));

        topK     = Math.Clamp(topK, 1, 10);
        minScore = Math.Clamp(minScore, 0.0, 1.0);

        _logger.LogInformation(
            "[search_similar_records] query={Query} topK={TopK} minScore={MinScore} pin={Pin}",
            searchQuery, topK, minScore, pinJsonPath ?? "(none)");

        var rawHits = await search.SearchAsync(searchQuery, topK, ct).ConfigureAwait(false);
        var totalBeforeFilter = rawHits.Count;

        var hits = new List<SimilarRecordHitDto>(capacity: topK);

        foreach (var hit in rawHits)
        {
            if (hit.Score < minScore) continue;

            string? pinnedValue = null;
            if (!string.IsNullOrWhiteSpace(pinJsonPath) &&
                !string.IsNullOrWhiteSpace(hit.Record.StateJson))
            {
                TryReadByPath(hit.Record.StateJson!, pinJsonPath!, out pinnedValue);
                if (IsNullishJson(pinnedValue)) pinnedValue = null;
            }

            hits.Add(new SimilarRecordHitDto(
                Id:              hit.Record.Id,
                Title:           hit.Record.Title,
                SimilarityScore: hit.Score,
                EvidenceSnippet: TrimToSentence(hit.Record.Content, EvidenceMaxChars),
                StateJson:       hit.Record.StateJson,
                PinnedValueJson: pinnedValue));
        }

        hits.Sort((a, b) => b.SimilarityScore.CompareTo(a.SimilarityScore));

        _logger.LogInformation(
            "[search_similar_records] {Filtered}/{Total} hits above threshold.",
            hits.Count, totalBeforeFilter);

        return new SimilarRecordResultDto(
            QueryUsed:            searchQuery,
            Hits:                 hits,
            TotalHitsBeforeFilter: totalBeforeFilter);
    }

    [KernelFunction("get_record_by_id")]
    [Description(
        "Fetches a single historical record by its exact ID. " +
        "Use after search_similar_records to inspect a field not visible in EvidenceSnippet. " +
        "Returns null if not found.")]
    public async Task<SimilarRecordHitDto?> GetRecordByIdAsync(
        [Description("Record ID exactly as returned in a previous search result.")]
        string recordId,

        [Description("Optional camelCase JSON path to auto-extract a specific field.")]
        string? pinJsonPath = null,

        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recordId)) return null;

        var record = await search.GetByIdAsync(recordId, ct).ConfigureAwait(false);
        if (record is null) return null;

        string? pinnedValue = null;
        if (!string.IsNullOrWhiteSpace(pinJsonPath) &&
            !string.IsNullOrWhiteSpace(record.StateJson))
        {
            TryReadByPath(record.StateJson!, pinJsonPath!, out pinnedValue);
            if (IsNullishJson(pinnedValue)) pinnedValue = null;
        }

        return new SimilarRecordHitDto(
            Id:              record.Id,
            Title:           record.Title,
            SimilarityScore: 1.0,
            EvidenceSnippet: TrimToSentence(record.Content, EvidenceMaxChars),
            StateJson:       record.StateJson,
            PinnedValueJson: pinnedValue);
    }


    private static bool TryReadByPath(string json, string path, out string? valueJson)
    {
        valueJson = null;
        try
        {
            var root = JsonNode.Parse(json);
            if (root is null) return false;

            JsonNode? current = root;

            foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current is null) return false;

                var bracketOpen  = segment.IndexOf('[', StringComparison.Ordinal);
                var bracketClose = segment.IndexOf(']', StringComparison.Ordinal);

                if (bracketOpen > 0 && bracketClose > bracketOpen)
                {
                    var propName = segment[..bracketOpen];
                    var idxStr   = segment[(bracketOpen + 1)..bracketClose];
                    if (!int.TryParse(idxStr, out var idx)) return false;
                    current = current[propName]?[idx];
                }
                else
                {
                    current = current[segment];
                }
            }

            valueJson = current?.ToJsonString();
            return valueJson is not null;
        }
        catch { return false; }
    }

    private static bool IsNullishJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return true;
        return json.Trim() switch
        {
            "null" or "\"\"" or "[]" or "{}" => true,
            _ => false
        };
    }

    private static string TrimToSentence(string? content, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(content)) return "";
        content = content.Trim();
        if (content.Length <= maxChars) return content;
        var cutoff = content.LastIndexOf(". ", maxChars, StringComparison.Ordinal);
        return cutoff > maxChars / 2
            ? content[..(cutoff + 1)].Trim()
            : content[..maxChars].TrimEnd() + "…";
    }
}


public interface IAgentMemoryIngestion<TRecord>
    where TRecord : IAgentMemoryRecord
{
    Task<string> IngestAsync(
        string title,
        string content,
        string? tagsCsv,
        string? stateJson,
        CancellationToken ct = default);
}