using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Verify.Net;
using Tsukimichi.Verify.Verify;

namespace Tsukimichi.Verify.Sources;

internal sealed record CollectSourceRef(string Type, string Text, string? RelatedType, long? RelatedId);

internal sealed record CollectEntry(string Kind, long Id, string Name, uint ItemId, IReadOnlyList<CollectSourceRef> Sources, string Url);

/// <summary>
/// FFXIV Collect's public JSON API. Each kind is one dump (<c>/api/{kind}</c>; the server returns every row on one page
/// today, and the loop keeps paging when <c>count</c> says more exist). Entries are joined to the catalog by item id
/// when both sides have one, else by canonical name.
/// </summary>
internal sealed class CollectSource(PoliteHttp http, TextWriter log)
{
    private const string Base = "https://ffxivcollect.com/api/";

    /// <summary>Reward kinds Collect covers, with the API path.</summary>
    public static readonly IReadOnlyDictionary<RewardKind, string> Paths = new Dictionary<RewardKind, string>
    {
        [RewardKind.Mount] = "mounts",
        [RewardKind.Minion] = "minions",
        [RewardKind.Emote] = "emotes",
        [RewardKind.Orchestrion] = "orchestrions",
        [RewardKind.Barding] = "bardings",
        [RewardKind.Hairstyle] = "hairstyles",
        [RewardKind.Ornament] = "fashions",
        [RewardKind.TripleTriadCard] = "triad/cards",
    };

    public static string SiteUrl(string path, long id) => $"https://ffxivcollect.com/{path}/{id}";

    public static string DumpUrl(string path, int page) => $"{Base}{path}?page={page}";

    private readonly Dictionary<RewardKind, List<CollectEntry>> dumps = [];

    public async Task<IReadOnlyList<CollectEntry>?> DumpAsync(RewardKind kind, CancellationToken ct)
    {
        if (dumps.TryGetValue(kind, out var cached))
        {
            return cached;
        }

        if (!Paths.TryGetValue(kind, out var path))
        {
            return null;
        }

        var list = new List<CollectEntry>();
        var page = 1;
        while (true)
        {
            var fetched = await http.GetAsync(DumpUrl(path, page), ct);
            if (!fetched.Ok)
            {
                log.WriteLine($"collect: {path} page {page} unavailable (status {fetched.Status})");
                return page == 1 ? null : list;
            }

            JsonObject? root;
            try
            {
                root = JsonNode.Parse(fetched.Body)?.AsObject();
            }
            catch (System.Text.Json.JsonException)
            {
                log.WriteLine($"collect: {path} page {page} was not JSON");
                return page == 1 ? null : list;
            }

            var results = root?["results"] as JsonArray;
            if (results is null)
            {
                return page == 1 ? null : list;
            }

            foreach (var r in results.OfType<JsonObject>())
            {
                var sources = new List<CollectSourceRef>();
                foreach (var s in (r["sources"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    sources.Add(new CollectSourceRef(
                        s["type"]?.GetValue<string>() ?? string.Empty,
                        s["text"]?.GetValue<string>() ?? string.Empty,
                        s["related_type"]?.GetValue<string>(),
                        s["related_id"] is JsonValue rv && rv.TryGetValue<long>(out var rid) ? rid : null));
                }

                var id = r["id"] is JsonValue iv && iv.TryGetValue<long>(out var i) ? i : 0;
                var itemId = r["item_id"] is JsonValue itv && itv.TryGetValue<uint>(out var it) ? it : 0u;
                list.Add(new CollectEntry(path, id, r["name"]?.GetValue<string>() ?? string.Empty, itemId, sources, SiteUrl(path, id)));
            }

            var count = root?["count"] is JsonValue cv && cv.TryGetValue<int>(out var c) ? c : list.Count;
            if (list.Count >= count || results.Count == 0)
            {
                break;
            }

            page++;
        }

        dumps[kind] = list;
        log.WriteLine($"collect: {path} {list.Count} entries");
        return list;
    }

    /// <summary>Finds the Collect entry for a catalog reward: item id first, then canonical name.</summary>
    public static CollectEntry? Find(IReadOnlyList<CollectEntry> dump, UniqueRewardEntry entry)
    {
        if (entry.ItemId != 0)
        {
            var byItem = dump.FirstOrDefault(e => e.ItemId == entry.ItemId);
            if (byItem is not null)
            {
                return byItem;
            }
        }

        var name = Names.Canon(entry.RewardName);
        var byName = dump.Where(e => Names.Canon(e.Name) == name).ToList();
        return byName.Count == 1 ? byName[0] : byName.FirstOrDefault();
    }
}
