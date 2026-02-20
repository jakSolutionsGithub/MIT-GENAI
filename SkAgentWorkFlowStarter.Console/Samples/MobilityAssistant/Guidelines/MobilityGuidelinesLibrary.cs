using System.Text.RegularExpressions;
using SkAgentWorkFlowStarter.Console.Framework.Guidelines;

namespace SkAgentWorkFlowStarter.Console.Samples.MobilityAssistant.Guidelines;


public sealed class MobilityGuidelinesLibrary : IGuidelinesLibrary
{
    private static readonly IReadOnlyList<MobilityGuidelineEntry> Library = BuildLibrary();

    public IReadOnlyList<GuidelineHit> Search(
        string query, string? contextFilter = null, int max = 8)
    {
        var q = (query ?? string.Empty).Trim();
        var normalizedFilter = string.IsNullOrWhiteSpace(contextFilter)
            ? null
            : NormalizeEventType(contextFilter);

        var tokens = Tokenize(q);
        var scored = new List<(MobilityGuidelineEntry Entry, int Score)>(capacity: Library.Count);

        foreach (var entry in Library)
        {
            if (string.Equals(entry.Category, "Intro", StringComparison.OrdinalIgnoreCase))
                continue;

            var typeBoost = 0;
            if (normalizedFilter is not null)
            {
                if (entry.EventTypes.Count == 0)                          typeBoost = 1;
                else if (entry.EventTypes.Contains(normalizedFilter))     typeBoost = 6;
                else                                                       typeBoost = -2;
            }

            var haystack = $"{entry.Category} {entry.Topic} {entry.Text} {string.Join(' ', entry.Tags)}";
            var score = typeBoost + ScoreTokens(tokens, haystack);

            score += BoostIfContains(haystack, q, "corporate travel plan", 12);
            score += BoostIfContains(haystack, q, "brussels",               6);
            score += BoostIfContains(haystack, q, "rush hour",              5);
            score += BoostIfContains(haystack, q, "shuttle",                5);
            score += BoostIfContains(haystack, q, "carpool",                5);
            score += BoostIfContains(haystack, q, "mobility plan",          5);
            score += BoostIfContains(haystack, q, "train",                  4);

            if (score > 0) scored.Add((entry, score));
        }

        return scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Entry.SortKey)
            .Take(Math.Clamp(max, 1, 50))
            .Select(s =>
            {
                var hit = ToHit(s.Entry);
                hit.Score = s.Score;
                return hit;
            })
            .ToList();
    }

    public GuidelineHit? GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var entry = Library.FirstOrDefault(e =>
            string.Equals(e.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
        return entry is null ? null : ToHit(entry);
    }

    public IReadOnlyList<string> ListCategories()
        => Library
            .Select(e => $"{e.Category} :: {e.Topic}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();


    private static GuidelineHit ToHit(MobilityGuidelineEntry e) => new()
    {
        Id = e.Id, Category = e.Category, Topic = e.Topic,
        Text = e.Text, Tags = e.Tags.ToList()
    };

    private static string NormalizeEventType(string raw)
    {
        var v = raw.Trim().ToLowerInvariant();
        if (v.Contains("corpor")) return "corporate";
        if (v.Contains("trade"))  return "public";
        if (v.Contains("public")) return "public";
        if (v.Contains("incent")) return "incentive";
        if (v.Contains("team"))   return "incentive";
        return "public";
    }

    private static IReadOnlyList<string> Tokenize(string input)
    {
        var cleaned = Regex.Replace(input.ToLowerInvariant(), @"[^\p{L}\p{N}\s]+", " ");
        return cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 3).Distinct().ToList();
    }

    private static int ScoreTokens(IReadOnlyList<string> tokens, string haystack)
    {
        var h = haystack.ToLowerInvariant();
        return tokens.Sum(t => h.Contains(t, StringComparison.OrdinalIgnoreCase) ? 3 : 0);
    }

    private static int BoostIfContains(string haystack, string query, string phrase, int boost)
    {
        var q = query.ToLowerInvariant();
        var h = haystack.ToLowerInvariant();
        var p = phrase.ToLowerInvariant();
        if (q.Contains(p)) return boost;
        var first = p.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (first is not null && h.Contains(p) && q.Contains(first)) return Math.Max(1, boost / 2);
        return 0;
    }


    private sealed record MobilityGuidelineEntry(
        string Id, string Category, string Topic, string Text,
        int SortKey, HashSet<string> EventTypes, IReadOnlyList<string> Tags);

    private static MobilityGuidelineEntry E(
        string id, string category, string topic, string text,
        int sortKey, IReadOnlyList<string> eventTypes, IReadOnlyList<string> tags)
        => new(id, category, topic, text, sortKey,
            new HashSet<string>(eventTypes, StringComparer.OrdinalIgnoreCase), tags);


    private static IReadOnlyList<MobilityGuidelineEntry> BuildLibrary()
    {
        var list = new List<MobilityGuidelineEntry>(capacity: 64);

        list.AddRange([
            E("CORP-CHOICE-01","Corporate Events","Choice of venue & date","If your client is a Brussels-based organisation with more than 100 employees, ask your client for its (mandatory) Corporate Travel Plan.",10,["corporate"],["Brussels","Corporate Travel Plan","≥100 employees","mandatory"]),
            E("CORP-CHOICE-02","Corporate Events","Choice of venue & date","Opt for an event schedule likely to have a single starting point for participants (their workplace) to enable integrated mobility solutions.",11,["corporate"],["single starting point","workplace","integrated solutions"]),
            E("CORP-CHOICE-03","Corporate Events","Choice of venue & date","Choose a venue area as close as possible to participants' starting point(s).",12,["corporate"],["distance reduction","venue selection"]),
            E("CORP-CHOICE-04","Corporate Events","Choice of venue & date","Choose venues based on soft mobility options nearby (bus/tram/metro, train stations, bike-sharing, carpool areas).",13,["corporate"],["public transport","multimodal","bike-sharing","carpool"]),
            E("CORP-CHOICE-05","Corporate Events","Choice of venue & date","Apply event schedules aligned with public transport availability near the venue.",14,["corporate"],["timetables","PT availability"]),
            E("CORP-CHOICE-06","Corporate Events","Choice of venue & date","Avoid organising during rush hour (cars in traffic jams generate higher CO2).",15,["corporate"],["rush hour","timing","CO2"]),
            E("CORP-CHOICE-07","Corporate Events","Choice of venue & date","If shuttles are used, provide stopping places, clear signage, and dedicated staff.",16,["corporate"],["shuttles","signage","ops"]),
            E("CORP-VENUE-01","Corporate Events","Venue equipment / arrangements","Draw up a Mobility Plan early: access + timetables for PT, shared transport, bike & car parking, carpool areas, charging stations.",20,["corporate"],["Mobility Plan","parking","charging","timetables"]),
            E("CORP-VENUE-02","Corporate Events","Venue equipment / arrangements","Provide sufficient supervised bicycle parking; cloakroom ticket systems improve security and trust.",21,["corporate"],["bike parking","security","cloakroom"]),
            E("CORP-VENUE-03","Corporate Events","Venue equipment / arrangements","Provide charging points for electric vehicles (bikes, scooters, cars).",22,["corporate"],["EV","charging","e-bike","scooter"]),
            E("CORP-VENUE-04","Corporate Events","Venue equipment / arrangements","During the event, communicate sustainability commitments (posters, flyers, table cards) to add value and reinforce sponsor brand.",23,["corporate"],["communication","on-site","sponsor"]),
            E("CORP-INFO-01","Corporate Events","Information / participant relations","Obtain participant list early + departure/return destinations (often home postal codes) via client or registration form to optimize venue and return journey.",30,["corporate"],["registration","postal code","origins"]),
            E("CORP-INFO-02","Corporate Events","Information / participant relations","Inform participants early about accessible mobility solutions (including multimodal combinations) for arrival and return.",31,["corporate"],["multimodal","arrival","return"]),
            E("CORP-INFO-03","Corporate Events","Information / participant relations","Motivate carpooling (between colleagues), even with rewards (free parking, drinks, etc.).",32,["corporate"],["carpool","incentives","rewards"]),
            E("CORP-INFO-04","Corporate Events","Information / participant relations","Provide a platform to help participants organize carpooling.",33,["corporate"],["carpool platform","matching"]),
            E("CORP-INFO-05","Corporate Events","Information / participant relations","Inform participants of travel impact and reward most virtuous transport modes.",34,["corporate"],["behavior change","rewards","impact awareness"]),
            E("CORP-INFO-06","Corporate Events","Information / participant relations","On-site: signage for mobility options (PT stops, bike parking) and timetables, ideally real-time.",35,["corporate"],["signage","real-time","wayfinding"]),
            E("CORP-PREF-01","Corporate Events","Preferred mobility solutions","Always consider both arrival AND return journeys (often returning home).",40,["corporate"],["arrival","return"]),
            E("CORP-PREF-02","Corporate Events","Preferred mobility solutions","If all participants share a starting point (workplace), prioritize collective transport (train, shuttles—prefer electric/hybrid).",41,["corporate"],["train","shuttle","collective transport"]),
            E("CORP-PREF-03","Corporate Events","Preferred mobility solutions","If participants must travel by car, encourage carpooling as much as possible.",42,["corporate"],["car","carpool"]),
        ]);

        list.AddRange([
            E("PUB-CHOICE-01","Public Events / Trade Shows","Choice of venue & date","Assess the approximate attendance area (local/provincial/regional/national) to guide venue area selection.",50,["public"],["catchment area","attendance area"]),
            E("PUB-CHOICE-02","Public Events / Trade Shows","Choice of venue & date","Choose a venue as central as possible relative to the estimated attendance area.",51,["public"],["central","accessibility"]),
            E("PUB-CHOICE-03","Public Events / Trade Shows","Choice of venue & date","Prioritize venues with the widest range of mobility options (proximity, accessibility, multimodality).",52,["public"],["multimodality","options"]),
            E("PUB-CHOICE-04","Public Events / Trade Shows","Choice of venue & date","Align schedules with public transport availability near the venue.",53,["public"],["timetables","PT availability"]),
            E("PUB-VENUE-01","Public Events / Trade Shows","Venue equipment / arrangements","Draft a Mobility Plan early: access + timetables, shared transport, bike/car parking, carpool areas, charging stations.",60,["public"],["Mobility Plan","charging","parking"]),
            E("PUB-VENUE-02","Public Events / Trade Shows","Venue equipment / arrangements","Provide sufficient supervised bicycle parking.",61,["public"],["bike parking"]),
            E("PUB-VENUE-03","Public Events / Trade Shows","Venue equipment / arrangements","Provide charging points for electric vehicles (bikes, scooters, cars).",62,["public"],["EV charging"]),
            E("PUB-INFO-01","Public Events / Trade Shows","Information / participant relations","If feasible, ask participants for departure/return locations (e.g., website form) to tailor the mobility offer.",70,["public"],["data collection","origins"]),
            E("PUB-INFO-02","Public Events / Trade Shows","Information / participant relations","Include detailed mobility info (Mobility Plan) in all comms: flyers, posters, website, socials.",71,["public"],["communications","Mobility Plan"]),
            E("PUB-INFO-03","Public Events / Trade Shows","Information / participant relations","Motivate carpooling and reward virtuous participants; provide a carpool platform if possible.",72,["public"],["carpool","platform","rewards"]),
            E("PUB-INFO-04","Public Events / Trade Shows","Information / participant relations","On-site: signage for access to mobility solutions and timetables, ideally real-time.",73,["public"],["signage","real-time"]),
            E("PUB-INFO-05","Public Events / Trade Shows","Information / participant relations","Routinely include a link to the event's Mobility Plan and Brussels Mobility recommendations in participant communications.",74,["public"],["Brussels Mobility","Mobility Plan"]),
            E("PUB-INFO-06","Public Events / Trade Shows","Information / participant relations","On arrival, ask for departure/return + mode used to produce a Mobility Report after the event.",75,["public"],["survey","Mobility Report"]),
            E("PUB-PREF-01","Public Events / Trade Shows","Preferred mobility solutions","If venue is far from major public transport, provide shuttle buses (preferably electric or hybrid).",80,["public"],["shuttle","electric bus"]),
            E("PUB-PREF-02","Public Events / Trade Shows","Preferred mobility solutions","Where possible, partner with public transport operators for fare reductions or special arrangements.",81,["public"],["PT operators","discounts","extended timetables"]),
            E("PUB-PREF-03","Public Events / Trade Shows","Preferred mobility solutions","If participants travel by car, encourage carpooling as much as possible.",82,["public"],["carpool","car"]),
        ]);

        list.AddRange([
            E("INC-CHOICE-01","Incentive / Teambuilding","Choice of venue & date","Prefer venues easily accessible by train (high-speed train for long distances).",90,["incentive"],["train","high-speed","long distance"]),
            E("INC-CHOICE-02","Incentive / Teambuilding","Choice of venue & date","Negotiate preferential tariffs early with public transport operators (trains/high-speed/buses).",91,["incentive"],["tariffs","operators"]),
            E("INC-CHOICE-03","Incentive / Teambuilding","Choice of venue & date","Set the event date according to public transport availability and prices; consider weekends if tickets are cheaper.",92,["incentive"],["pricing","timetables","weekend"]),
            E("INC-CHOICE-04","Incentive / Teambuilding","Choice of venue & date","Select the venue based on local soft mobility offer (shared bikes, public transport).",93,["incentive"],["soft mobility","shared bikes"]),
            E("INC-VENUE-01","Incentive / Teambuilding","Venue equipment / arrangements","Provide soft mobility options at the venue (shared bikes, etc.).",100,["incentive"],["shared bikes","on-site mobility"]),
            E("INC-INFO-01","Incentive / Teambuilding","Information / participant relations","Encourage participants to use public transport or carpooling to access the collective departure point.",110,["incentive"],["collective departure","carpool","public transport"]),
            E("INC-INFO-02","Incentive / Teambuilding","Information / participant relations","On site, provide practical information for public transport use (maps, timetables, etc.).",111,["incentive"],["maps","timetables"]),
            E("INC-INFO-03","Incentive / Teambuilding","Information / participant relations","Choose non-polluting activities; avoid activities involving cars, quad bikes, etc.",112,["incentive"],["activities","avoid cars","awareness"]),
            E("INC-PREF-01","Incentive / Teambuilding","Preferred mobility solutions","Avoid flying whenever possible (it is the most polluting mode).",120,["incentive"],["avoid flying","plane"]),
            E("INC-PREF-02","Incentive / Teambuilding","Preferred mobility solutions","Use the most ecological collective transport: trains/high-speed trains, then electric/hybrid buses.",121,["incentive"],["train","electric bus"]),
            E("INC-PREF-03","Incentive / Teambuilding","Preferred mobility solutions","On-site travel: prioritize public transport (preferably electric/hybrid) and soft mobility (bikes, e-bikes).",122,["incentive"],["on-site","soft mobility"]),
        ]);

        list.AddRange([
            E("CHK-BEFORE-01","Checklist","Before the event","Inform the client of commitment to sustainable mobility solutions; identify participant departure/return points; select venue based on accessibility; develop a Mobility Plan; verify public transport operational; prepare questionnaire for Mobility Report.",200,[],["checklist","before","Mobility Plan","Mobility Report"]),
            E("CHK-BEFORE-02","Checklist","Before the event","Check for strikes/demonstrations that could limit travel; contact operators for special rates; ensure bike parking/charging/signage are adequate.",201,[],["strikes","operators","signage","charging"]),
            E("CHK-DURING-01","Checklist","During the event","Ensure participants inform you of their place of departure (postal code) and modes of transport (questionnaire).",210,[],["during","survey","postal code"]),
            E("CHK-AFTER-01","Checklist","After the event","Collect/process mobility data for a Mobility Report; inform client of results; thank and reward virtuous participants; thank suppliers.",220,[],["after","Mobility Report","reward"]),
        ]);

        list.Add(E("INTRO-01","Intro","What this library is","This library guides event managers on sustainable mobility: selecting venues with good public transport, aligning schedules with soft mobility, informing participants, and optimizing logistics.",1,[],["overview","sustainable mobility"]));

        return list;
    }
}