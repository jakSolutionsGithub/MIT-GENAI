using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Connectors.InMemory;
using SkAgentWorkFlowStarter.Console.Framework.Memory;
using SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Historical.Models;

namespace SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Historical.Rules;

public sealed class MobilityHistoryIngestionService : IAgentMemoryIngestion<MobilityHistoryRecord>
{
    public const string CollectionName = "mobility_history";

    private readonly InMemoryVectorStore _vectorStore;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IConfiguration _config;
    private readonly ILogger<MobilityHistoryIngestionService> _logger;

    public MobilityHistoryIngestionService(
        InMemoryVectorStore vectorStore,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IConfiguration config,
        ILogger<MobilityHistoryIngestionService>? logger = null)
    {
        _vectorStore = vectorStore;
        _embeddingGenerator = embeddingGenerator;
        _config = config;
        _logger = logger ?? NullLogger<MobilityHistoryIngestionService>.Instance;
    }

    public async Task<string> IngestAsync(
        string title,
        string content,
        string? tagsCsv,
        string? stateJson,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("content cannot be empty.", nameof(content));

        if (string.IsNullOrWhiteSpace(title))
            title = $"Mobility case {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm}";

        var record = new MobilityHistoryRecord
        {
            Id = CreateStableIdFromContent(content),
            Title = title.Trim(),
            Content = content.Trim(),
            TagsCsv = string.IsNullOrWhiteSpace(tagsCsv) ? null : tagsCsv.Trim(),
            StateJson = string.IsNullOrWhiteSpace(stateJson) ? null : stateJson.Trim(),
        };

        await UpsertAsync(record, ct).ConfigureAwait(false);
        return record.Id;
    }

    public async Task<int> IngestConfiguredFolderAsync(CancellationToken ct = default)
    {
        var folder = _config["MobilityHistory:ExcelFolder"];
        if (string.IsNullOrWhiteSpace(folder))
            throw new InvalidOperationException(
                "Missing configuration key: MobilityHistory:ExcelFolder. " +
                "Set it in appsettings.json or user-secrets.");

        return await IngestExcelFolderAsync(folder, ct).ConfigureAwait(false);
    }

    public async Task<int> IngestExcelFolderAsync(string folderPath, CancellationToken ct = default)
    {
        var resolved = ResolveFolderPath(folderPath);

        if (!Directory.Exists(resolved))
            throw new DirectoryNotFoundException($"Mobility history folder not found: {resolved}");

        var files = Directory.EnumerateFiles(resolved, "*.xlsx", SearchOption.TopDirectoryOnly).ToList();

        _logger.LogInformation(
            "[MobilityHistoryIngestionService] Found {Count} Excel file(s) in {Folder}.",
            files.Count, resolved);

        var total = 0;
        foreach (var file in files)
        {
            total += await IngestExcelFileAsync(file, ct).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "[MobilityHistoryIngestionService] Ingested {Total} record(s) from {Folder}.",
            total, resolved);

        return total;
    }

    public async Task<int> IngestExcelFileAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Excel file not found.", filePath);

        _logger.LogInformation(
            "[MobilityHistoryIngestionService] Reading '{File}'.", Path.GetFileName(filePath));

        using var wb = new XLWorkbook(filePath);

        var ws =
            wb.Worksheets.FirstOrDefault(s =>
                string.Equals(s.Name, "MobilityHistory", StringComparison.OrdinalIgnoreCase))
            ?? wb.Worksheets.First();

        var records = ReadRecordsFromWorksheet(filePath, ws);
        if (records.Count == 0)
        {
            _logger.LogWarning(
                "[MobilityHistoryIngestionService] No records found in '{File}'.",
                Path.GetFileName(filePath));
            return 0;
        }

        await UpsertManyAsync(records, ct).ConfigureAwait(false);
        return records.Count;
    }

    public async Task UpsertAsync(MobilityHistoryRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.Content))
            throw new ArgumentException(
                "MobilityHistoryRecord.Content cannot be empty — it is the embeddable text.",
                nameof(record));

        ValidateContentIsNotJson(record.Content, record.Title);

        // SK 1.71.0: GetCollection returns InMemoryCollection<TKey, TRecord>
        var collection = _vectorStore.GetCollection<string, MobilityHistoryRecord>(CollectionName);

        var embedding = await _embeddingGenerator
            .GenerateAsync(record.Content, cancellationToken: ct)
            .ConfigureAwait(false);

        record.ContentEmbedding = embedding.Vector;

        await collection.UpsertAsync(record, cancellationToken: ct).ConfigureAwait(false);

        _logger.LogInformation(
            "[MobilityHistoryIngestionService] Upserted record id={Id} title='{Title}'.",
            record.Id, record.Title);
    }

    public async Task UpsertManyAsync(IReadOnlyList<MobilityHistoryRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;

        var collection = _vectorStore.GetCollection<string, MobilityHistoryRecord>(CollectionName);

        foreach (var r in records)
        {
            if (string.IsNullOrWhiteSpace(r.Content)) continue;

            ValidateContentIsNotJson(r.Content, r.Title);

            var embedding = await _embeddingGenerator
                .GenerateAsync(r.Content, cancellationToken: ct)
                .ConfigureAwait(false);

            r.ContentEmbedding = embedding.Vector;
        }

        // SK 1.71.0: UpsertAsync(IEnumerable<TRecord>, CancellationToken) — the batch overload
        await collection.UpsertAsync(records, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "[MobilityHistoryIngestionService] Batch upserted {Count} record(s).",
            records.Count);
    }

    private static List<MobilityHistoryRecord> ReadRecordsFromWorksheet(string filePath, IXLWorksheet ws)
    {
        var used = ws.RangeUsed();
        if (used is null) return [];

        var firstRow = used.FirstRow();
        var lastRow = used.LastRow();

        var headerMap = BuildHeaderMap(firstRow);

        if (!headerMap.TryGetValue("content", out var contentCol))
            throw new InvalidOperationException(
                $"Excel '{Path.GetFileName(filePath)}' sheet '{ws.Name}' " +
                $"must contain a 'Content' column (natural-language prose, not JSON).");

        headerMap.TryGetValue("title", out var titleCol);
        headerMap.TryGetValue("tagscsv", out var tagsCol);
        headerMap.TryGetValue("statejson", out var stateCol);

        var records = new List<MobilityHistoryRecord>();

        for (var row = firstRow.RowNumber() + 1; row <= lastRow.RowNumber(); row++)
        {
            var content = GetCellString(ws, row, contentCol);
            if (string.IsNullOrWhiteSpace(content)) continue;

            var title = titleCol > 0 ? GetCellString(ws, row, titleCol) : null;
            var tags = tagsCol > 0 ? GetCellString(ws, row, tagsCol) : null;
            var stateJson = stateCol > 0 ? GetCellString(ws, row, stateCol) : null;

            records.Add(new MobilityHistoryRecord
            {
                Id = CreateStableId(Path.GetFileName(filePath), ws.Name, row),
                Title = string.IsNullOrWhiteSpace(title)
                    ? $"Mobility case ({Path.GetFileName(filePath)}:{ws.Name}#{row})"
                    : title.Trim(),
                Content = content.Trim(),
                TagsCsv = string.IsNullOrWhiteSpace(tags) ? null : tags.Trim(),
                StateJson = string.IsNullOrWhiteSpace(stateJson) ? null : stateJson.Trim(),
            });
        }

        return records;
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLRangeRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var cell in headerRow.CellsUsed())
        {
            var raw = cell.GetString();
            if (string.IsNullOrWhiteSpace(raw)) continue;
            map[NormalizeHeader(raw)] = cell.Address.ColumnNumber;
        }

        return map;
    }

    private static string NormalizeHeader(string header)
    {
        var sb = new StringBuilder(header.Length);
        foreach (var ch in header.Trim())
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    private static string GetCellString(IXLWorksheet ws, int rowNum, int col)
        => col <= 0 ? string.Empty : ws.Cell(rowNum, col).GetString();

    private static string CreateStableId(string fileName, string sheetName, int rowNumber)
    {
        var input = $"{fileName}|{sheetName}|{rowNumber}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..32];
    }

    private static string CreateStableIdFromContent(string content)
    {
        var input = $"{DateTimeOffset.UtcNow:yyyy-MM-dd}|{content}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..32];
    }

    private static string ResolveFolderPath(string configuredPath)
        => Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));

    private void ValidateContentIsNotJson(string content, string title)
    {
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            _logger.LogWarning(
                "[MobilityHistoryIngestionService] Record '{Title}': Content appears to be JSON. " +
                "This degrades embedding quality. Content should be natural-language prose. " +
                "Put structured data in StateJson instead.",
                title);
        }
    }
}
