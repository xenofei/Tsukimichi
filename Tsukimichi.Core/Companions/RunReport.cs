using System.Globalization;
using System.Text;

namespace Tsukimichi.Core.Companions;

/// <summary>
/// What a "Why it stopped" report says (plan v7, 1.18.0, A2), gathered by the plugin when Copy report is pressed. It
/// holds no field for the character's name, world, Free Company or chat: those never reach the report, and
/// <see cref="RunReport.Build"/> also scrubs <see cref="RunReport"/>'s private words out of every field that holds free
/// text from the game or another plugin.
/// </summary>
public sealed record RunReportFacts
{
    /// <summary>"1.18.0".</summary>
    public string PluginVersion { get; init; } = string.Empty;

    /// <summary>The Dalamud API level ("15").</summary>
    public string ApiLevel { get; init; } = string.Empty;

    /// <summary>The game's version ("2026.09.15").</summary>
    public string GameVersion { get; init; } = string.Empty;

    /// <summary>"Questionable", "AutoDuty", "Travel", "Artisan".</summary>
    public string HandOff { get; init; } = string.Empty;

    /// <summary>The hand-off plugin's version; empty when unknown (or for Tsukimichi's own travel).</summary>
    public string HandOffVersion { get; init; } = string.Empty;

    /// <summary>Who started it, in a few words: "started by Tsukimichi, \"this quest only\"".</summary>
    public string StartedBy { get; init; } = string.Empty;

    /// <summary>The stop: "error · stopped on its own".</summary>
    public string Stopped { get; init; } = string.Empty;

    /// <summary>The quest's name as the spoiler shield shows it; empty for none.</summary>
    public string QuestName { get; init; } = string.Empty;

    /// <summary>The quest's id as the game counts it (the row id less 65536); 0 for none.</summary>
    public uint QuestId { get; init; }

    /// <summary>Questionable's sequence; null when unknown.</summary>
    public byte? Sequence { get; init; }

    /// <summary>Questionable's step within the sequence, 0-based; null when unknown.</summary>
    public int? Step { get; init; }

    /// <summary>The job's abbreviation ("PLD"); empty when unknown.</summary>
    public string Job { get; init; } = string.Empty;

    /// <summary>The job's level; 0 when unknown.</summary>
    public int Level { get; init; }

    /// <summary>The zone's name; empty when unknown.</summary>
    public string Zone { get; init; } = string.Empty;

    /// <summary>The TerritoryType row; 0 when unknown.</summary>
    public uint TerritoryId { get; init; }

    /// <summary>The map coordinates ("18.2, 24.7"); empty when unknown.</summary>
    public string MapCoordinates { get; init; } = string.Empty;

    /// <summary>The travel plugins and the movement setting: "vnavmesh 0.4.3 · Lifestream 2.5.1 · movement Standard".</summary>
    public string Travel { get; init; } = string.Empty;

    /// <summary>How often a hand-off stopped at this step on this computer, this one included; 0 to leave the line out.</summary>
    public int StopsHere { get; init; }
}

/// <summary>
/// The text Copy report puts on the clipboard (spec-1.18 A2, "Copy report"): plain lines ready for a GitHub issue, in
/// English whatever the client's language (the reader is the plugin's author), each line left out when its facts are
/// unknown. It never includes the character's name, world, Free Company or chat lines: the facts have no field for
/// them, and every free-text field is scrubbed of the <c>privateWords</c> the plugin passes (the character's name, its
/// world, its Free Company tag) and of line breaks, so a quest or zone name, or another plugin's version string, cannot
/// carry them in. Pure, so the rules are tested.
/// </summary>
public static class RunReport
{
    /// <summary>What a private word is replaced with.</summary>
    public const string Hidden = "[hidden]";

    /// <summary>A private word shorter than this is not scrubbed (a one-letter Free Company tag would eat every word).</summary>
    public const int MinPrivateLength = 2;

    private const string Separator = " · ";

    /// <summary>The report for <paramref name="facts"/>, with <paramref name="privateWords"/> scrubbed out.</summary>
    public static string Build(RunReportFacts facts, IReadOnlyList<string> privateWords)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(privateWords);
        string Clean(string text) => Scrub(text, privateWords);
        var c = CultureInfo.InvariantCulture;
        var report = new StringBuilder(512);

        Line(report, Join(
            Labelled("Tsukimichi ", Clean(facts.PluginVersion)),
            Labelled("Dalamud API ", Clean(facts.ApiLevel)),
            Labelled("game ", Clean(facts.GameVersion))));

        var handOff = Clean(facts.HandOff);
        if (handOff.Length > 0)
        {
            var version = Clean(facts.HandOffVersion);
            var started = Clean(facts.StartedBy);
            Line(report, "Hand-off: " + handOff + (version.Length > 0 ? " " + version : string.Empty) + (started.Length > 0 ? " (" + started + ")" : string.Empty));
        }

        Line(report, Labelled("Stopped: ", Clean(facts.Stopped)));

        var quest = Clean(facts.QuestName);
        if (quest.Length > 0 || facts.QuestId != 0)
        {
            var name = quest.Length > 0 && facts.QuestId != 0
                ? string.Format(c, "{0} (#{1})", quest, facts.QuestId)
                : quest.Length > 0 ? quest : string.Format(c, "#{0}", facts.QuestId);
            Line(report, "Quest: " + Join(
                name,
                facts.Step is { } step ? string.Format(c, "step {0}", step + 1) : string.Empty,
                facts.Sequence is { } sequence ? string.Format(c, "sequence {0}", sequence) : string.Empty));
        }

        var job = Clean(facts.Job);
        var zone = Clean(facts.Zone);
        var where = zone.Length > 0 && facts.TerritoryId != 0 ? string.Format(c, "{0} ({1})", zone, facts.TerritoryId) : zone.Length > 0 ? zone : facts.TerritoryId != 0 ? facts.TerritoryId.ToString(c) : string.Empty;
        var coords = Clean(facts.MapCoordinates);
        Line(report, Join(
            job.Length > 0 ? "Job: " + job + (facts.Level > 0 ? " " + facts.Level.ToString(c) : string.Empty) : string.Empty,
            where.Length > 0 ? "Zone: " + where : string.Empty,
            coords.Length > 0 ? "at " + coords : string.Empty));

        Line(report, Labelled("Travel: ", Clean(facts.Travel)));
        if (facts.StopsHere > 0)
        {
            Line(report, string.Format(c, "Stops at this step on this computer: {0}", facts.StopsHere));
        }

        return report.ToString().TrimEnd('\n');
    }

    /// <summary>
    /// <paramref name="text"/> on one line (control characters and line breaks become spaces, runs of spaces one),
    /// trimmed, with every private word of at least <see cref="MinPrivateLength"/> characters replaced by
    /// <see cref="Hidden"/>, ignoring case.
    /// </summary>
    public static string Scrub(string? text, IReadOnlyList<string> privateWords)
    {
        ArgumentNullException.ThrowIfNull(privateWords);
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var flat = new StringBuilder(text.Length);
        var space = false;
        foreach (var ch in text)
        {
            if (char.IsControl(ch) || char.IsWhiteSpace(ch))
            {
                space = flat.Length > 0;
                continue;
            }

            if (space)
            {
                flat.Append(' ');
                space = false;
            }

            flat.Append(ch);
        }

        var result = flat.ToString();
        foreach (var word in privateWords)
        {
            var trimmed = word?.Trim() ?? string.Empty;
            if (trimmed.Length >= MinPrivateLength)
            {
                result = result.Replace(trimmed, Hidden, StringComparison.OrdinalIgnoreCase);
            }
        }

        return result;
    }

    private static string Labelled(string label, string value) => value.Length > 0 ? label + value : string.Empty;

    private static string Join(params string[] parts)
    {
        var joined = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            if (joined.Length > 0)
            {
                joined.Append(Separator);
            }

            joined.Append(part);
        }

        return joined.ToString();
    }

    private static void Line(StringBuilder report, string line)
    {
        if (line.Length > 0)
        {
            report.Append(line).Append('\n');
        }
    }
}

/// <summary>
/// How often a hand-off stopped at a quest's step on this computer (spec-1.18 A2: "Stops at this step on this
/// computer"), kept in the settings under "rowId:sequence" keys and capped at <see cref="Max"/> steps: the step noted
/// first goes first. The settings keep a plain dictionary (so the saved file keeps its shape), whose order is the order
/// keys were added only while none was ever removed; so a step is never removed alone: the map is rebuilt in order
/// without the oldest, and its order stays the order the steps were first noted in, through saves and loads. Pure.
/// </summary>
public static class RunStopCounts
{
    /// <summary>The most steps kept.</summary>
    public const int Max = 200;

    /// <summary>The key of quest <paramref name="rowId"/> at <paramref name="sequence"/> ("65964:3"; "65964" without a sequence).</summary>
    public static string Key(uint rowId, byte? sequence) =>
        sequence is { } s ? string.Create(CultureInfo.InvariantCulture, $"{rowId}:{s}") : rowId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Counts one more stop at the step and returns the count now; 0 (and nothing kept) without a quest.</summary>
    public static int Note(Dictionary<string, int> counts, uint rowId, byte? sequence)
    {
        ArgumentNullException.ThrowIfNull(counts);
        if (rowId == 0)
        {
            return 0;
        }

        var key = Key(rowId, sequence);
        if (counts.TryGetValue(key, out var count))
        {
            count = Math.Max(0, count) + 1;
            counts[key] = count;
            return count;
        }

        if (counts.Count >= Max)
        {
            // Removing one key would leave a hole the next key fills, out of order (the newest would then go first):
            // keep the newest Max - 1 in their order and add them back to an emptied map, which fills it in order.
            var kept = counts.Skip(counts.Count - (Max - 1)).ToList();
            counts.Clear();
            foreach (var (k, v) in kept)
            {
                counts.Add(k, v);
            }
        }

        counts.Add(key, 1);
        return 1;
    }
}
