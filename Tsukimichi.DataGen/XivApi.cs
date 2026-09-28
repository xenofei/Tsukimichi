using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace Tsukimichi.DataGen;

/// <summary>
/// Minimal xivapi v2 client for the verifier's cross-checks. Rows are fetched in batches
/// (<c>/api/sheet/{sheet}?rows=a,b,c&amp;fields=...</c>) and cached per (sheet, fields) so the same row is never requested twice.
/// Every failure is recorded in <see cref="Errors"/> and surfaces as a missing row; the verifier treats that as a failed check.
/// </summary>
internal sealed class XivApi : IDisposable
{
    private const string BaseUrl = "https://v2.xivapi.com/api/";
    private const int BatchSize = 50;

    private readonly HttpClient http;
    private readonly Dictionary<(string Sheet, string Fields), Dictionary<uint, JsonObject>> cache = new();

    public List<string> Errors { get; } = new();
    public int Requests { get; private set; }
    public string? Version { get; private set; }

    public XivApi(string userAgent)
    {
        http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>Fetches the given rows' <c>fields</c> objects. Rows the API does not return are absent from the result.</summary>
    public IReadOnlyDictionary<uint, JsonObject> Rows(string sheet, IEnumerable<uint> ids, string fields)
    {
        var key = (sheet, fields);
        if (!cache.TryGetValue(key, out var sheetCache))
            cache[key] = sheetCache = new Dictionary<uint, JsonObject>();

        var missing = ids.Distinct().Where(id => !sheetCache.ContainsKey(id)).OrderBy(id => id).ToList();
        foreach (var chunk in missing.Chunk(BatchSize))
        {
            var url = $"sheet/{sheet}?rows={string.Join(",", chunk)}&fields={Uri.EscapeDataString(fields)}";
            var root = Get(url);
            if (root?["rows"] is not JsonArray rows)
                continue;
            foreach (var row in rows)
            {
                if (row is JsonObject obj && obj["row_id"] is JsonValue idNode && idNode.TryGetValue<uint>(out var id) && obj["fields"] is JsonObject f)
                    sheetCache[id] = f;
            }
        }

        var result = new Dictionary<uint, JsonObject>();
        foreach (var id in ids.Distinct())
        {
            if (sheetCache.TryGetValue(id, out var f))
                result[id] = f;
        }
        return result;
    }

    public JsonObject? Row(string sheet, uint id, string fields)
        => Rows(sheet, [id], fields).GetValueOrDefault(id);

    private JsonObject? Get(string url)
    {
        Requests++;
        try
        {
            using var response = http.GetAsync(url).GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                Errors.Add($"GET {url}: HTTP {(int)response.StatusCode} {Truncate(body)}");
                return null;
            }
            var node = JsonNode.Parse(body) as JsonObject;
            if (node is null)
            {
                Errors.Add($"GET {url}: response is not a JSON object");
                return null;
            }
            Version ??= node["version"]?.GetValue<string>();
            return node;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            Errors.Add($"GET {url}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static string Truncate(string s) => s.Length <= 120 ? s : s[..120] + "...";

    // Reading helpers for the shapes xivapi v2 returns.

    /// <summary>A string field; empty when absent.</summary>
    public static string Str(JsonObject? fields, string name)
        => fields?[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;

    /// <summary>A row reference field: either a bare number or an object with <c>value</c>.</summary>
    public static uint Ref(JsonNode? node)
    {
        switch (node)
        {
            case JsonValue v when v.TryGetValue<uint>(out var n):
                return n;
            case JsonObject o when o["value"] is JsonValue vv && vv.TryGetValue<uint>(out var m):
                return m;
            default:
                return 0;
        }
    }

    /// <summary>The nested <c>fields</c> object of a row reference field.</summary>
    public static JsonObject? Nested(JsonObject? fields, string name)
        => fields?[name] is JsonObject o ? o["fields"] as JsonObject : null;

    public void Dispose() => http.Dispose();
}
