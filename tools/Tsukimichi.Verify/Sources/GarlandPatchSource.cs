using System.Text.Json;
using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Verify.Net;

namespace Tsukimichi.Verify.Sources;

/// <summary>One quest as a Garland patch document lists it: row id, the patch Garland files it under, its name there.</summary>
internal sealed record GarlandPatchQuest(uint RowId, string Patch, string Name, string Series);

/// <summary>
/// Garland Tools' patch data, the P8 seed (feature plan v3, dalamud-developer review §9: Garland's patch field, not the
/// wiki). Three documents shapes are read:
/// <list type="bullet">
/// <item>the core data document, whose <c>patch.partialIndex</c> names every patch series Garland tracks ("2.0" …
/// "7.5");</item>
/// <item>one patch document per series (<c>db/doc/patch/en/2/&lt;series&gt;.json</c>), which lists under
/// <c>patch.patches.&lt;patch&gt;.quest</c> every quest first seen in each patch of the series (7.5, 7.51, 7.55 …) —
/// about forty requests for the whole catalog instead of one per quest;</item>
/// <item>the per-quest document (<c>db/doc/quest/en/2/&lt;id&gt;.json</c>, the one <see cref="GarlandSource"/>
/// reads), whose <c>quest.patch</c> is the same fact as a number: read from the cache wherever an earlier run fetched
/// it, and fetched for the quests the patch documents do not list.</item>
/// </list>
/// </summary>
internal sealed class GarlandPatchSource(PoliteHttp http, TextWriter log)
{
    public const string CoreUrl = "https://www.garlandtools.org/db/doc/core/en/3/data.json";

    private const string Host = "www.garlandtools.org";

    public static string SeriesUrl(string series) => $"https://www.garlandtools.org/db/doc/patch/en/2/{series}.json";

    public static string SiteUrl(string series) => $"https://www.garlandtools.org/db/#patch/{series}";

    /// <summary>The patch series Garland tracks, oldest first, and the one it calls current; empty when the core document could not be read.</summary>
    public async Task<(IReadOnlyList<string> Series, string Current)> GetSeriesAsync(CancellationToken ct)
    {
        var fetched = await http.GetAsync(CoreUrl, ct);
        if (!fetched.Ok || Parse(fetched.Body) is not JsonObject root || root["patch"] is not JsonObject patch)
        {
            log.WriteLine($"garland: core document unavailable (status {fetched.Status})");
            return ([], string.Empty);
        }

        var series = new List<string>();
        foreach (var (key, _) in patch["partialIndex"] as JsonObject ?? [])
        {
            series.Add(key);
        }

        series.Sort(PatchVersion.Comparer);
        var current = patch["current"] is JsonValue cv ? cv.ToJsonString().Trim('"') : string.Empty;
        return (series, PatchVersion.Normalize(current));
    }

    /// <summary>
    /// Every quest one series document lists, with the patch of the sub-list it sits in. Null when the document could
    /// not be fetched (offline miss, host gave up); an empty list when it was fetched and lists no quest.
    /// </summary>
    public async Task<IReadOnlyList<GarlandPatchQuest>?> GetSeriesQuestsAsync(string series, CancellationToken ct)
    {
        var fetched = await http.GetAsync(SeriesUrl(series), ct);
        if (fetched.NoResponse)
        {
            return null;
        }

        if (!fetched.Ok || Parse(fetched.Body) is not JsonObject root || root["patch"]?["patches"] is not JsonObject patches)
        {
            return [];
        }

        var quests = new List<GarlandPatchQuest>();
        foreach (var (patchKey, lists) in patches)
        {
            var patch = PatchVersion.Normalize(patchKey);
            foreach (var node in lists?["quest"] as JsonArray ?? [])
            {
                if (node is JsonObject q && q["i"] is JsonValue iv && iv.TryGetValue<uint>(out var id))
                {
                    quests.Add(new GarlandPatchQuest(id, patch, q["n"] is JsonValue nv && nv.TryGetValue<string>(out var n) ? n : string.Empty, series));
                }
            }
        }

        return quests;
    }

    /// <summary>Row id → <c>quest.patch</c> for every per-quest document already in the cache (no request is made).</summary>
    public Dictionary<uint, string> CachedQuestDocuments()
    {
        var result = new Dictionary<uint, string>();
        foreach (var (url, body, _) in http.EnumerateCached(Host, "https://www.garlandtools.org/db/doc/quest/en/2/"))
        {
            var tail = url[(url.LastIndexOf('/') + 1)..];
            if (!tail.EndsWith(".json", StringComparison.Ordinal) || !uint.TryParse(tail[..^".json".Length], out var id) || result.ContainsKey(id))
            {
                continue;
            }

            if (QuestDocumentPatch(body) is { } patch)
            {
                result[id] = patch;
            }
        }

        return result;
    }

    /// <summary>
    /// The per-quest document's patch, through the cache (a cached document costs no request). Null when the document
    /// is unavailable (offline miss, host gave up); empty when Garland has no document or no patch for the quest.
    /// </summary>
    public async Task<string?> GetQuestPatchAsync(uint rowId, CancellationToken ct)
    {
        var fetched = await http.GetAsync(GarlandSource.Url(rowId), ct);
        if (fetched.NoResponse)
        {
            return null;
        }

        return fetched.Ok ? QuestDocumentPatch(fetched.Body) ?? string.Empty : string.Empty;
    }

    /// <summary><c>quest.patch</c> as written (a JSON number such as 2.0 or 7.55), normalized; null when absent.</summary>
    private static string? QuestDocumentPatch(string body)
    {
        if (Parse(body)?["quest"]?["patch"] is not JsonValue pv)
        {
            return null;
        }

        var raw = pv.ToJsonString().Trim('"');
        var patch = PatchVersion.Normalize(raw);
        return PatchVersion.IsPatch(patch) ? patch : null;
    }

    private static JsonNode? Parse(string body)
    {
        try
        {
            return JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
