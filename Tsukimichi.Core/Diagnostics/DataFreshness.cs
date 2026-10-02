using System.Collections.Frozen;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Diagnostics;

/// <summary>How the client's game version stands against the game version Tsukimichi's shipped data was built from.</summary>
public enum FreshnessVerdict
{
    /// <summary>The same game version, or nothing to go on (a version missing and no quest new since the data).</summary>
    Current,

    /// <summary>
    /// The client is newer than the data: a game update came out after the release. The quests the data has never
    /// seen are <see cref="DataFreshnessReport.NewQuestIds"/>; a hotfix that added none has an empty set.
    /// </summary>
    NewerClient,

    /// <summary>The client is older than the data (a stale client or a test server). Settings › About says so; nothing else does.</summary>
    OlderClient,
}

/// <summary>What <see cref="DataFreshness.Evaluate"/> found.</summary>
/// <param name="Verdict">The client against the data.</param>
/// <param name="NewQuestIds">Catalog row ids <c>quest_patches.json</c> does not list: new since the data was built.</param>
/// <param name="DataVersion">The game version the data was built from, trimmed; empty when unknown.</param>
/// <param name="ClientVersion">The client's game version, trimmed; empty when unknown.</param>
public sealed record DataFreshnessReport(FreshnessVerdict Verdict, IReadOnlySet<uint> NewQuestIds, string DataVersion, string ClientVersion)
{
    /// <summary>Nothing known: current, no new quests.</summary>
    public static DataFreshnessReport None { get; } = new(FreshnessVerdict.Current, FrozenSet<uint>.Empty, string.Empty, string.Empty);

    /// <summary>How many quests are newer than the data.</summary>
    public int NewQuests => NewQuestIds.Count;

    /// <summary>
    /// Whether the main window shows the "Game updated" strip: the client is newer and has quests the data has never
    /// seen. An older client never does (only the About line speaks), nor a hotfix that added no quest.
    /// </summary>
    public bool ShowStrip => Verdict == FreshnessVerdict.NewerClient && NewQuestIds.Count > 0;

    /// <summary>Whether a quest is new since the data was built (the "New since data" filter).</summary>
    public bool IsNew(uint rowId) => NewQuestIds.Contains(rowId);
}

/// <summary>
/// Patch-day honesty (feature plan v5, 1.5.0 "Trust"; R10 proposal 1). Quests are read live from the game, but the
/// shipped data (<c>unique_quests.json</c>, the curated overlay, <c>quest_patches.json</c>) is built against one game
/// version and goes stale when the game updates. <c>quest_patches.json</c> lists every quest id the catalog held when
/// it was last written (<see cref="QuestPatches"/>), so an id it does not list is a quest new since then: its rewards,
/// patch and curated notes may be missing until a Tsukimichi update. Pure; the plugin evaluates once per catalog.
/// </summary>
public static class DataFreshness
{
    /// <summary>
    /// The verdict and the quests new since the data. <paramref name="catalogIds"/> are the catalog's row ids; the new
    /// ones are those <paramref name="questPatches"/> does not list (none when the file is empty or missing: without
    /// it nothing can be told apart). The versions compare as game versions (<see cref="Compare"/>); when either is
    /// missing or unreadable, quests the data has never seen are the evidence of a newer client.
    /// </summary>
    public static DataFreshnessReport Evaluate(IEnumerable<uint> catalogIds, QuestPatches questPatches, string? dataVersion, string? clientVersion)
    {
        ArgumentNullException.ThrowIfNull(catalogIds);
        ArgumentNullException.ThrowIfNull(questPatches);
        var data = dataVersion?.Trim() ?? string.Empty;
        var client = clientVersion?.Trim() ?? string.Empty;

        IReadOnlySet<uint> fresh = FrozenSet<uint>.Empty;
        if (questPatches.ByRowId.Count > 0)
        {
            var found = new HashSet<uint>();
            foreach (var id in catalogIds)
            {
                if (!questPatches.Lists(id))
                {
                    found.Add(id);
                }
            }

            if (found.Count > 0)
            {
                fresh = found.ToFrozenSet();
            }
        }

        var verdict = Compare(data, client) switch
        {
            { } known => known,
            null => fresh.Count > 0 ? FreshnessVerdict.NewerClient : FreshnessVerdict.Current,
        };
        return new DataFreshnessReport(verdict, fresh, data, client);
    }

    /// <summary>
    /// The client against the data as game versions (date, then build numbers: a hotfix is newer); null when either
    /// is missing or not a game version.
    /// </summary>
    public static FreshnessVerdict? Compare(string? dataVersion, string? clientVersion)
    {
        if (!GameVersion.TryParse(dataVersion, out var data) || !GameVersion.TryParse(clientVersion, out var client))
        {
            return null;
        }

        var order = client.CompareTo(data);
        return order == 0 ? FreshnessVerdict.Current : order > 0 ? FreshnessVerdict.NewerClient : FreshnessVerdict.OlderClient;
    }

    /// <summary>
    /// Whether the strip shows for <paramref name="report"/> given the client version the player last dismissed it on
    /// (<paramref name="dismissedFor"/>, empty for never): one dismissal per client version, so the next game update
    /// brings it back.
    /// </summary>
    public static bool StripVisible(DataFreshnessReport report, string? dismissedFor)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (!report.ShowStrip)
        {
            return false;
        }

        var dismissed = dismissedFor?.Trim() ?? string.Empty;
        if (dismissed.Length == 0)
        {
            return true;
        }

        // As versions where both parse (a trailing newline or a short form still matches), else as text.
        var key = DismissKey(report);
        return GameVersion.TryParse(dismissed, out var a) && GameVersion.TryParse(key, out var b)
            ? a.CompareTo(b) != 0
            : !string.Equals(dismissed, key, StringComparison.Ordinal);
    }

    /// <summary>What a dismissal stores: the client version, or "unknown" when it could not be read.</summary>
    public static string DismissKey(DataFreshnessReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return report.ClientVersion.Length > 0 ? report.ClientVersion : UnknownClient;
    }

    private const string UnknownClient = "unknown";
}
