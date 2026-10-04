using System.Text.Json;
using System.Text.Json.Nodes;
using Tsukimichi.Verify.Verify;

namespace Tsukimichi.Verify.Output;

/// <summary>
/// docs/data/verification-allowlist.json: entries <c>{ rowId, fact, source?, verdict, reason, until }</c> that excuse a
/// gate-failing row until the named release. <c>rowId</c> may be a quest row id or <c>"*"</c>; <c>fact</c> may name a
/// reward kind (<c>reward:Mount</c>) for reward rows, narrowed by the optional <c>rewardId</c>; a <c>prereqs</c> entry with
/// source <c>questionable</c> excuses a Questionable link the catalog does not imply (<c>Tsukimichi.Verify questionable</c>),
/// narrowed to one required quest by the optional <c>prereqId</c>; <c>fix</c> (optional) names where a confirmed catalogWrong is
/// corrected (a mapper rule, a curated file, or data) and feeds the "Discrepancies to fix" section of the report. An
/// entry has expired when <c>until</c> is a version at or below the current plugin version. A <see cref="Settled"/> entry
/// (1.22.0, verdict <c>settled</c>) records a decision made on the evidence that no release will change (a reward kept
/// quest-only by decision, a Questionable link the catalog rightly leaves out): it needs <c>evidence</c> and a reason,
/// carries no <c>until</c>, never expires, and excuses its row whatever verdict the row reads.
/// </summary>
internal sealed class Allowlist
{
    public sealed record Entry(string RowId, string Fact, string? Source, string Verdict, string Reason, string Until, string? Evidence, string? Fix, string? RewardId = null, string? PrereqId = null);

    /// <summary>The <c>source</c> of an entry that excuses a Questionable prerequisite link.</summary>
    public const string QuestionableSource = "questionable";

    /// <summary>The verdict of a settled entry, which never expires (1.22.0).</summary>
    public const string Settled = "settled";

    /// <summary>"settled", or "until 1.23.0", for the report.</summary>
    public static string Label(Entry e) => e.Verdict == Settled ? Settled : "until " + e.Until;

    public IReadOnlyList<Entry> Entries { get; }

    private Allowlist(IReadOnlyList<Entry> entries) => Entries = entries;

    public static Allowlist Load(string path)
    {
        if (!File.Exists(path))
        {
            return new Allowlist([]);
        }

        var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? new JsonObject();
        var list = new List<Entry>();
        if (root["entries"] is JsonArray entries)
        {
            foreach (var node in entries.OfType<JsonObject>())
            {
                list.Add(new Entry(
                    node["rowId"]?.ToString() ?? "*",
                    node["fact"]?.GetValue<string>() ?? "*",
                    node["source"]?.GetValue<string>(),
                    node["verdict"]?.GetValue<string>() ?? "*",
                    node["reason"]?.GetValue<string>() ?? string.Empty,
                    node["until"]?.GetValue<string>() ?? string.Empty,
                    node["evidence"]?.GetValue<string>(),
                    node["fix"]?.GetValue<string>(),
                    node["rewardId"]?.ToString(),
                    node["prereqId"]?.ToString()));
            }
        }

        return new Allowlist(list);
    }

    public Entry? Covering(QuestRow row, Version? current)
        => Entries.FirstOrDefault(e => Applies(e, row.RowId, row.Fact, row.Source, row.Verdict, current));

    public Entry? Covering(RewardRow row, Version? current)
        => Entries.FirstOrDefault(e => Applies(e, row.QuestRowId, "reward:" + row.Kind, row.Source, row.Verdict, current) && (e.RewardId is null || e.RewardId == row.RewardId.ToString()));

    /// <summary>The entry excusing Questionable's link from <paramref name="questRowId"/> to <paramref name="requiredRowId"/>, if any is still live.</summary>
    public Entry? CoveringLink(uint questRowId, uint requiredRowId, Version? current)
        => Entries.FirstOrDefault(e => !Expired(e, current)
                                       && e.RowId == questRowId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                       && e.Fact == Facts.Prereqs
                                       && e.Source == QuestionableSource
                                       && (e.PrereqId is null || e.PrereqId == requiredRowId.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    public static bool Expired(Entry e, Version? current)
    {
        if (e.Verdict == Settled || current is null || string.IsNullOrEmpty(e.Until))
        {
            return false;
        }

        return Version.TryParse(Pad(e.Until), out var until) && current >= until;
    }

    private static bool Applies(Entry e, uint rowId, string fact, string source, Verdict verdict, Version? current)
    {
        if (Expired(e, current))
        {
            return false;
        }

        if (e.RowId != "*" && e.RowId != rowId.ToString())
        {
            return false;
        }

        if (e.Fact != "*" && !string.Equals(e.Fact, fact, StringComparison.Ordinal))
        {
            return false;
        }

        if (e.Source is not null && !string.Equals(e.Source, source, StringComparison.Ordinal))
        {
            return false;
        }

        return e.Verdict is "*" or Settled || string.Equals(e.Verdict, Verdicts.Name(verdict), StringComparison.Ordinal);
    }

    private static string Pad(string v) => v.Count(c => c == '.') == 0 ? v + ".0" : v;

    public static void WriteEmpty(string path)
    {
        var root = new JsonObject
        {
            ["$schema_note"] = "entries: { rowId (quest row id or \"*\"), fact (fact name, or reward:<Kind> for reward rows), source (optional), verdict, reason, evidence (URL), fix (optional: mapper rule, curated file or data change that corrects a catalogWrong), until (release in which the fix lands; the entry expires once the plugin version reaches it; none for a settled entry), rewardId (reward rows only), prereqId (source questionable only: the required quest); verdict settled marks a decision that never expires and needs evidence }",
            ["entries"] = new JsonArray(),
        };
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }
}
