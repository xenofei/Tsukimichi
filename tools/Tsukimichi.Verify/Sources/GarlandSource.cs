using System.Text.Json.Nodes;
using Tsukimichi.Verify.Net;

namespace Tsukimichi.Verify.Sources;

internal sealed record GarlandQuest(uint RowId, string Url, bool Found, string Name, uint InstanceId, string InstanceName, IReadOnlyList<uint> PrerequisiteQuests);

/// <summary>
/// Garland Tools per-quest document, used only for <c>reward.instance</c> on the curated duty unlocks (the instance
/// partial carries the duty name). Everything else in the document is derived from the same sheets the plugin reads.
/// </summary>
internal sealed class GarlandSource(PoliteHttp http)
{
    public static string Url(uint rowId) => $"https://www.garlandtools.org/db/doc/quest/en/2/{rowId}.json";

    public static string SiteUrl(uint rowId) => $"https://www.garlandtools.org/db/#quest/{rowId}";

    public async Task<GarlandQuest?> GetAsync(uint rowId, CancellationToken ct)
    {
        var url = Url(rowId);
        var fetched = await http.GetAsync(url, ct);
        if (fetched.NoResponse)
        {
            return null;
        }

        if (!fetched.Ok)
        {
            return new GarlandQuest(rowId, url, false, string.Empty, 0, string.Empty, []);
        }

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(fetched.Body)?.AsObject();
        }
        catch (System.Text.Json.JsonException)
        {
            return new GarlandQuest(rowId, url, false, string.Empty, 0, string.Empty, []);
        }

        var quest = root?["quest"] as JsonObject;
        if (quest is null)
        {
            return new GarlandQuest(rowId, url, false, string.Empty, 0, string.Empty, []);
        }

        var instanceId = quest["reward"]?["instance"] is JsonValue iv && iv.TryGetValue<uint>(out var i) ? i : 0u;
        var instanceName = string.Empty;
        foreach (var p in (root?["partials"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (p["type"]?.GetValue<string>() == "instance" && p["obj"]?["i"] is JsonValue pv && pv.TryGetValue<uint>(out var pid) && pid == instanceId)
            {
                instanceName = p["obj"]?["n"]?.GetValue<string>() ?? string.Empty;
            }
        }

        var prereqs = new List<uint>();
        foreach (var q in (quest["reqs"]?["quests"] as JsonArray ?? []))
        {
            if (q is JsonValue qv && qv.TryGetValue<uint>(out var qid))
            {
                prereqs.Add(qid);
            }
        }

        return new GarlandQuest(rowId, url, true, quest["name"]?.GetValue<string>() ?? string.Empty, instanceId, instanceName, prereqs);
    }
}
