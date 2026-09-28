using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tsukimichi.Verify.Net;

/// <summary>One fetch as the sources see it: a cached or live body with its HTTP status. Status 0 means no response (offline miss, robots refusal, host gave up).</summary>
internal sealed record Fetched(string Url, int Status, string Body, bool FromCache, string? ETag)
{
    public bool Ok => Status is >= 200 and < 300;
    public bool NotFound => Status == 404;
    public bool NoResponse => Status == 0;
}

/// <summary>Raised when a host keeps failing (ten consecutive errors) so the run stops rather than hammering it.</summary>
internal sealed class HostBlockedException(string host, string detail) : Exception($"{host}: giving up after repeated failures ({detail})")
{
    public string Host { get; } = host;
}

/// <summary>
/// The only HTTP client in the tool. Serial requests, one every <see cref="RateSeconds"/> per host, an identifying
/// User-Agent, robots.txt fetched once per host and honoured, exponential backoff on 429/5xx/transport errors, and a
/// permanent on-disk cache keyed by URL under the game-version cache root (2xx and 404 are cached; everything else is
/// retried next run). <c>--offline</c> turns every cache miss into a status-0 response.
/// </summary>
internal sealed class PoliteHttp : IDisposable
{
    private const string UserAgentToken = "Tsukimichi.Verify";
    private const int MaxAttempts = 6;
    private const int ConsecutiveFailureLimit = 10;

    private readonly HttpClient http;
    private readonly string cacheRoot;
    private readonly bool offline;
    private readonly TextWriter log;
    private readonly Dictionary<string, DateTime> lastRequest = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Robots> robots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> consecutiveFailures = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> blocked = new(StringComparer.OrdinalIgnoreCase);

    public PoliteHttp(string cacheRoot, string userAgent, double rateSeconds, bool offline, TextWriter log)
    {
        this.cacheRoot = cacheRoot;
        this.offline = offline;
        this.log = log;
        RateSeconds = rateSeconds;
        UserAgent = userAgent;
        Directory.CreateDirectory(cacheRoot);

        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxConnectionsPerServer = 1,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/json;q=0.9,*/*;q=0.8");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.8");
    }

    public double RateSeconds { get; }
    public string UserAgent { get; }
    public int LiveRequests { get; private set; }
    public int CacheHits { get; private set; }
    public int RobotsRefusals { get; private set; }
    public IReadOnlyCollection<string> BlockedHosts => blocked;

    /// <summary>Fetches a URL through the cache. Never throws for HTTP errors; throws <see cref="HostBlockedException"/> when a host is given up on.</summary>
    public async Task<Fetched> GetAsync(string url, CancellationToken ct = default)
    {
        var uri = new Uri(url);
        var (bodyPath, metaPath) = CachePaths(uri);
        if (File.Exists(metaPath))
        {
            var meta = JsonNode.Parse(await File.ReadAllTextAsync(metaPath, ct))?.AsObject();
            if (meta is not null && meta["status"] is JsonValue sv && sv.TryGetValue<int>(out var cachedStatus) && (cachedStatus is >= 200 and < 300 || cachedStatus == 404))
            {
                CacheHits++;
                var body = File.Exists(bodyPath) ? await File.ReadAllTextAsync(bodyPath, ct) : string.Empty;
                return new Fetched(url, cachedStatus, body, true, meta["etag"]?.GetValue<string>());
            }
        }

        if (offline)
        {
            return new Fetched(url, 0, string.Empty, false, null);
        }

        if (blocked.Contains(uri.Host))
        {
            return new Fetched(url, 0, string.Empty, false, null);
        }

        if (!await AllowedByRobotsAsync(uri, ct))
        {
            RobotsRefusals++;
            log.WriteLine($"robots: {uri.Host} disallows {uri.PathAndQuery}; skipped");
            return new Fetched(url, 0, string.Empty, false, null);
        }

        var attempt = 0;
        while (true)
        {
            await ThrottleAsync(uri.Host, ct);
            attempt++;
            int status;
            string? etag = null;
            string text = string.Empty;
            string failure;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
                status = (int)response.StatusCode;
                etag = response.Headers.ETag?.Tag;
                text = await response.Content.ReadAsStringAsync(ct);
                LiveRequests++;
                if (status is >= 200 and < 300 || status == 404)
                {
                    consecutiveFailures[uri.Host] = 0;
                    await WriteCacheAsync(bodyPath, metaPath, url, status, etag, text, ct);
                    return new Fetched(url, status, text, false, etag);
                }

                failure = $"HTTP {status}";
                if (status is 429 or >= 500)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta;
                    await BackoffAsync(uri.Host, attempt, retryAfter, failure, ct);
                }
                else
                {
                    // 403/401/other client errors: not retried, not cached.
                    log.WriteLine($"http: {failure} for {url}");
                    consecutiveFailures[uri.Host] = consecutiveFailures.GetValueOrDefault(uri.Host) + 1;
                    CheckBlocked(uri.Host, failure);
                    return new Fetched(url, status, text, false, etag);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
                await BackoffAsync(uri.Host, attempt, null, failure, ct);
            }

            consecutiveFailures[uri.Host] = consecutiveFailures.GetValueOrDefault(uri.Host) + 1;
            CheckBlocked(uri.Host, failure);
            if (attempt >= MaxAttempts)
            {
                log.WriteLine($"http: giving up on {url} after {attempt} attempts ({failure})");
                return new Fetched(url, 0, string.Empty, false, null);
            }
        }
    }

    /// <summary>Writes the manifest: one entry per cached fetch (url, status, fetchedUtc, etag, sha256), no content. Sorted by URL.</summary>
    public void WriteManifest(string path, string gameVersion)
    {
        var entries = new List<JsonObject>();
        foreach (var metaPath in Directory.EnumerateFiles(cacheRoot, "*.meta.json", SearchOption.AllDirectories))
        {
            try
            {
                if (JsonNode.Parse(File.ReadAllText(metaPath)) is JsonObject meta)
                {
                    entries.Add(meta);
                }
            }
            catch (JsonException)
            {
                // A half-written sidecar from an interrupted run; the next fetch overwrites it.
            }
        }

        entries.Sort((a, b) => string.CompareOrdinal(a["url"]?.GetValue<string>(), b["url"]?.GetValue<string>()));
        var root = new JsonObject
        {
            ["gameVersion"] = gameVersion,
            ["userAgent"] = UserAgent,
            ["entries"] = new JsonArray(entries.Select(e => (JsonNode)e).ToArray()),
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) + "\n");
    }

    public void Dispose() => http.Dispose();

    private (string Body, string Meta) CachePaths(Uri uri)
    {
        var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)))[..32];
        var dir = Path.Combine(cacheRoot, uri.Host);
        Directory.CreateDirectory(dir);
        return (Path.Combine(dir, key + ".body"), Path.Combine(dir, key + ".meta.json"));
    }

    private static async Task WriteCacheAsync(string bodyPath, string metaPath, string url, int status, string? etag, string text, CancellationToken ct)
    {
        await File.WriteAllTextAsync(bodyPath, text, ct);
        var meta = new JsonObject
        {
            ["url"] = url,
            ["status"] = status,
            ["fetchedUtc"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["etag"] = etag,
            ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))),
            ["bytes"] = text.Length,
        };
        await File.WriteAllTextAsync(metaPath, meta.ToJsonString(), ct);
    }

    private async Task ThrottleAsync(string host, CancellationToken ct)
    {
        if (lastRequest.TryGetValue(host, out var last))
        {
            var wait = last.AddSeconds(RateSeconds) - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, ct);
            }
        }

        lastRequest[host] = DateTime.UtcNow;
    }

    private async Task BackoffAsync(string host, int attempt, TimeSpan? retryAfter, string failure, CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(Math.Min(120, RateSeconds * Math.Pow(2, attempt)));
        if (retryAfter is { } ra && ra > delay)
        {
            delay = ra;
        }

        log.WriteLine($"http: {host} {failure}; attempt {attempt}/{MaxAttempts}, backing off {delay.TotalSeconds:F0} s");
        await Task.Delay(delay, ct);
    }

    private void CheckBlocked(string host, string failure)
    {
        if (consecutiveFailures.GetValueOrDefault(host) >= ConsecutiveFailureLimit)
        {
            blocked.Add(host);
            throw new HostBlockedException(host, failure);
        }
    }

    private async Task<bool> AllowedByRobotsAsync(Uri uri, CancellationToken ct)
    {
        if (!robots.TryGetValue(uri.Host, out var rules))
        {
            rules = Robots.AllowAll;
            var robotsUrl = $"{uri.Scheme}://{uri.Host}/robots.txt";
            var (bodyPath, metaPath) = CachePaths(new Uri(robotsUrl));
            string? text = null;
            if (File.Exists(metaPath) && File.Exists(bodyPath))
            {
                var meta = JsonNode.Parse(await File.ReadAllTextAsync(metaPath, ct))?.AsObject();
                if (meta?["status"] is JsonValue sv && sv.TryGetValue<int>(out var s))
                {
                    text = s == 200 ? await File.ReadAllTextAsync(bodyPath, ct) : string.Empty;
                }
            }

            if (text is null)
            {
                await ThrottleAsync(uri.Host, ct);
                try
                {
                    using var response = await http.GetAsync(robotsUrl, ct);
                    LiveRequests++;
                    var status = (int)response.StatusCode;
                    text = status == 200 ? await response.Content.ReadAsStringAsync(ct) : string.Empty;
                    await WriteCacheAsync(bodyPath, metaPath, robotsUrl, status == 200 ? 200 : 404, response.Headers.ETag?.Tag, text, ct);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    log.WriteLine($"robots: {uri.Host} unreadable ({ex.Message}); treating as allow-all");
                    text = string.Empty;
                }
            }

            if (text.Length > 0 && !text.TrimStart().StartsWith('<'))
            {
                rules = Robots.Parse(text, UserAgentToken);
            }

            robots[uri.Host] = rules;
            log.WriteLine($"robots: {uri.Host} {(rules.RuleCount == 0 ? "no rules for us" : rules.RuleCount + " rules")}");
        }

        return rules.IsAllowed(uri.PathAndQuery);
    }

    /// <summary>Elapsed-time helper for the run log.</summary>
    public static string Elapsed(Stopwatch clock) => clock.Elapsed.TotalHours >= 1
        ? $"{clock.Elapsed.TotalHours:F1} h"
        : clock.Elapsed.TotalMinutes >= 1 ? $"{clock.Elapsed.TotalMinutes:F1} min" : $"{clock.Elapsed.TotalSeconds:F1} s";
}
