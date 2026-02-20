using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SkAgentWorkFlowStarter.Console.Framework.Guidelines;


public interface IGuidelineEntry
{
    string Id       { get; }
    string Category { get; }
    string Topic    { get; }
    string Text     { get; }
    IReadOnlyList<string> Tags { get; }
}


public interface IGuidelinesLibrary
{
    IReadOnlyList<GuidelineHit> Search(string query, string? contextFilter = null, int max = 8);

    GuidelineHit? GetById(string id);

    IReadOnlyList<string> ListCategories();
}

public sealed class GuidelineHit
{
    public string Id       { get; set; } = "";
    public string Category { get; set; } = "";
    public string Topic    { get; set; } = "";
    public string Text     { get; set; } = "";
    public IReadOnlyList<string> Tags { get; set; } = [];
    public int? Score { get; set; }
}


public sealed class GuidelinesResult(IReadOnlyList<GuidelineHit> hits)
{
    public IReadOnlyList<GuidelineHit> Hits { get; } = hits;
}


[Description(
    "Domain-specific guideline and best-practice library. " +
    "Use get_guidelines as the primary entrypoint for any policy or best-practice question. " +
    "Always retrieve guidelines via tools — never rely on training knowledge alone.")]
public sealed class GuidelinesTools(IGuidelinesLibrary library)
{
    [KernelFunction("get_guidelines")]
    [Description(
        "PRIMARY entrypoint. Retrieves the most relevant guideline snippets for a situation. " +
        "Always call this before making non-trivial recommendations. " +
        "Pack the query with all available context: domain type, location, audience, " +
        "constraints, and the specific topic (venue, transport, comms, checklist, etc.). " +
        "Returns ranked snippets with stable IDs you can use with get_guideline_by_id. " +
        "If 0 hits returned, try broader terms or call list_guideline_categories first.")]
    public GuidelinesResult GetGuidelines(
        [Description(
            "Natural-language query with full context. " +
            "Example: 'corporate event Brussels 300 attendees rush hour shuttle carpool'.")]
        string query,

        [Description("Optional context type filter (e.g. event type, department, domain).")]
        string? contextFilter = null,

        [Description("Max snippets to return (default 8).")]
        int max = 8)
        => new(library.Search(query, contextFilter, max));

    [KernelFunction("get_guidelines_by_type")]
    [Description(
        "Returns ALL guidelines for a specific context type in canonical order. " +
        "Use for comprehensive structured overviews, not targeted advice. " +
        "Prefer get_guidelines for targeted recommendations.")]
    public GuidelinesResult GetGuidelinesByType(
        [Description("Context type exactly as listed by list_guideline_categories.")]
        string contextType,

        [Description("Max guidelines to return (default 25).")]
        int max = 25)
        => new(library.Search("", contextType, max));

    [KernelFunction("get_guideline_by_id")]
    [Description(
        "Fetches a single guideline by its stable ID (e.g. CORP-CHOICE-01). " +
        "Use after get_guidelines to retrieve full text of a specific entry. " +
        "Returns null if ID not found.")]
    public GuidelineHit? GetGuidelineById(
        [Description("Guideline ID exactly as returned by get_guidelines.")]
        string id)
        => library.GetById(id);

    [KernelFunction("list_guideline_categories")]
    [Description(
        "Returns all available 'Category :: Topic' pairs in the guideline library. " +
        "Call this when unsure what topics exist before formulating a search query.")]
    public IReadOnlyList<string> ListGuidelineCategories()
        => library.ListCategories();
}