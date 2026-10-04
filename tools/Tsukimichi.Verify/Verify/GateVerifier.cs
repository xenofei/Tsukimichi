using System.Text.RegularExpressions;
using Tsukimichi.Core.Model;
using Tsukimichi.Verify.Game;
using Tsukimichi.Verify.Sources;

namespace Tsukimichi.Verify.Verify;

/// <summary>
/// The <c>gates</c> fact (feature plan v7 C3): what the wiki's quest pages say a quest waits for that no quest
/// prerequisite expresses, read from the infobox's free-text <c>requirements</c> field and from the page's System lines
/// about this quest ("In order to receive the quest …, you must have [*] equipped, in your Armoury Chest, or in your
/// inventory."), sorted into classes and compared with what the catalog models: a curated game gate
/// (<c>curated/game_gates.json</c>) or a sheet field. Only the class and a short normalised value are written ("floor
/// 50, the Palace of the Dead"), never the wiki's text. Classes the sheets carry as their own fields (class and job,
/// allied society and Grand Company ranks, custom delivery and carrier levels, duty clears, which the <c>duties</c> fact
/// compares) are left to them.
/// </summary>
internal static partial class GateVerifier
{
    public const string Fact = "gates";

    /// <summary>One class of gate: its name, the pattern that finds it, and how its value reads.</summary>
    private sealed record GateClass(string Name, Regex Pattern, Func<Match, string> Value, bool SystemLine = false);

    private static readonly GateClass[] Classes =
    [
        new("deepDungeonFloor", DeepDungeon(), m => $"{m.Groups["kind"].Value.ToLowerInvariant()} {m.Groups["n"].Value}, {m.Groups["dd"].Value}"),
        new("resistanceRank", ResistanceRank(), m => "rank " + m.Groups["n"].Value),
        new("mettle", Mettle(), m => m.Groups["n"].Value.Replace(",", string.Empty, StringComparison.Ordinal) + " mettle"),
        new("occultRecord", OccultRecord(), _ => "Occult Record entries"),
        new("knowledgeLevel", Knowledge(), _ => "knowledge level"),
        new("islandRank", IslandRank(), m => "rank " + m.Groups["n"].Value),
        new("variantNotes", VariantNotes(), _ => "variant dungeon notes"),
        new("blueMagic", BlueMagic(), m => m.Groups["spell"].Value.Trim()),
        new("achievement", Achievement(), _ => "an achievement"),
        new("beastsTamed", Beasts(), _ => "beasts tamed"),
        new("elementalLevel", ElementalLevel(), m => "elemental level " + m.Groups["n"].Value),
        new("chocoboCompanion", Chocobo(), _ => "My Little Chocobo"),
        new("toolHeld", ToolHeld(), _ => "a tool or weapon in your possession", SystemLine: true),
        new("dutyUnlocked", DutyUnlocked(), m => m.Groups["duty"].Value.Trim(), SystemLine: true),
    ];

    /// <summary>
    /// The gate rows of every cached quest page that names one, against <paramref name="game"/>'s catalog with its
    /// curated game gates. A page is matched to its quest by the infobox's <c>id-gt</c>; removed rows are left out.
    /// </summary>
    public static List<QuestRow> Run(GameCatalog game, IEnumerable<WikiPage> pages)
    {
        var rows = new List<QuestRow>();
        var seen = new HashSet<(uint, string)>();
        foreach (var page in pages)
        {
            if (page.Missing || page.QuestInfobox.Count == 0 || page.IdGt == 0 || game.Catalog.GetByRowId(page.IdGt) is not { IsRemoved: false } quest)
            {
                continue;
            }

            var requirements = Clean(page.QuestInfobox.GetValueOrDefault("requirements", string.Empty));
            var systemLines = page.Text.Split('\n').Where(line => AboutThisQuest(line, quest.Name)).Select(Clean).ToList();
            foreach (var gateClass in Classes)
            {
                var texts = gateClass.SystemLine ? systemLines : [requirements, .. systemLines];
                foreach (var text in texts)
                {
                    if (gateClass.Pattern.Match(text) is not { Success: true } m || !seen.Add((quest.RowId, gateClass.Name)))
                    {
                        continue;
                    }

                    rows.Add(Row(game, quest, gateClass.Name, gateClass.Value(m), page.Url));
                }
            }
        }

        Reconcile(game, rows);
        return rows;
    }

    /// <summary>
    /// qa-data-engineer §1.4 for the gates fact (1.21): an unresolved row becomes sourceWrong when the wiki names on the
    /// quest the very gate a quest that follows it carries (that quest's row matched with the same value, and its previous
    /// quests include this one) while this quest's own text and sheet state none: no System line "In order to … this
    /// quest", no accept condition that is no quest, no unlock link its row waits for. The game's text and its sheet then
    /// agree with the catalog, and the wiki has put the next quest's gate on this one (The Black Wolf's Ultimatum names
    /// the chocobo companion Operation Archon asks for).
    /// </summary>
    private static void Reconcile(GameCatalog game, List<QuestRow> rows)
    {
        var matched = rows.Where(r => r.Verdict == Verdict.Match).ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Verdict != Verdict.Unresolved || game.Catalog.GetByRowId(row.RowId) is not { } quest)
            {
                continue;
            }

            var next = matched.FirstOrDefault(m => m.SourceValue == row.SourceValue && game.Catalog.GetByRowId(m.RowId) is { } follower && follower.PreviousQuests.QuestIds.Contains(quest.RowId));
            if (next is null)
            {
                continue;
            }

            var name = Bare(quest.Name);
            var textStatesGate = game.SystemLines(quest).Any(line => InOrderTo().IsMatch(line) && (line.Contains("this quest", StringComparison.Ordinal) || (name.Length > 0 && line.Contains(name, StringComparison.Ordinal))));
            var sheetStatesGate = quest.AcceptConditions.Any(id => game.Catalog.GetByRowId(id) is null) || game.RequiredUnlockLink(quest.RowId) != 0;
            if (textStatesGate || sheetStatesGate)
            {
                continue;
            }

            rows[i] = row with
            {
                Verdict = Verdict.SourceWrong,
                Reason = $"the wiki names the gate of the quest that follows, {next.Name} ({next.RowId}), which curated/game_gates.json carries; this quest's own text states no gate and its sheet names no accept condition or unlock link to wait for",
            };
        }
    }

    /// <summary>A quest name without the private-use icon glyphs some repeatable quests open with.</summary>
    private static string Bare(string name) => new string(name.Where(c => c is < '' or > '').ToArray()).Trim();

    /// <summary>A row: matched when the catalog models a gate for the quest, unresolved when it models none.</summary>
    private static QuestRow Row(GameCatalog game, QuestRecord quest, string gateClass, string value, string url)
    {
        var sourceValue = gateClass + ": " + value;
        if (game.Catalog.GameGateOf(quest.RowId) is { } gate)
        {
            return new QuestRow(quest.RowId, quest.Name, Fact, gate.Gate, SourceNames.Wiki, sourceValue, url, Verdict.Match, string.Empty, string.Empty);
        }

        return new QuestRow(quest.RowId, quest.Name, Fact, string.Empty, SourceNames.Wiki, sourceValue, url, Verdict.Unresolved,
            "the wiki states a gate no curated game gate (curated/game_gates.json) models; a gate needs two sources (the game's text, the sheets, the wiki), or the wiki names the quest's objective rather than what it waits for",
            string.Empty);
    }

    /// <summary>A System line about this quest (its own name, or "this quest"); never one about the next quest.</summary>
    private static bool AboutThisQuest(string line, string name)
    {
        if (!line.Contains("System", StringComparison.Ordinal) || !InOrderTo().IsMatch(line))
        {
            return false;
        }

        var bare = Bare(name);
        return line.Contains("this quest", StringComparison.Ordinal) || (bare.Length > 0 && line.Contains(bare, StringComparison.Ordinal));
    }

    /// <summary>Wiki markup to plain words: links to their text, <c>{{i|X}}</c> and <c>{{action icon|X}}</c> to X, line breaks to "; ".</summary>
    internal static string Clean(string text)
    {
        text = Template().Replace(text, m => m.Groups[1].Value);
        text = Link().Replace(text, m => m.Groups[2].Success && m.Groups[2].Value.Length > 0 ? m.Groups[2].Value : m.Groups[1].Value);
        text = Break().Replace(text, "; ");
        return text.Replace("'''", string.Empty, StringComparison.Ordinal).Replace("''", string.Empty, StringComparison.Ordinal).Replace("&#44;", ",", StringComparison.Ordinal).Replace("&comma;", ",", StringComparison.Ordinal).Trim();
    }

    [GeneratedRegex(@"\{\{\s*(?:i|action icon|item icon)\s*\|([^|}]+)[^}]*\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex Template();

    [GeneratedRegex(@"\[\[([^\]|]+)(?:\|([^\]]*))?\]\]")]
    private static partial Regex Link();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex Break();

    [GeneratedRegex(@"In order to (?:undertake|receive|accept|proceed with)")]
    private static partial Regex InOrderTo();

    [GeneratedRegex(@"(?<kind>floor|stone|level)\s+(?<n>\d+)\s+(?:of|in)\s+(?:the\s+)?(?<dd>Palace of the Dead|Heaven-on-High|Eureka Orthos|Pilgrim's Traverse)", RegexOptions.IgnoreCase)]
    private static partial Regex DeepDungeon();

    [GeneratedRegex(@"Resistance Rank\s*(?<n>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ResistanceRank();

    [GeneratedRegex(@"Mettle\D{0,20}(?<n>\d[\d,]*)", RegexOptions.IgnoreCase)]
    private static partial Regex Mettle();

    [GeneratedRegex(@"Occult Record")]
    private static partial Regex OccultRecord();

    [GeneratedRegex(@"knowledge level", RegexOptions.IgnoreCase)]
    private static partial Regex Knowledge();

    [GeneratedRegex(@"(?:Island Sanctuary\s*Rank|^Rank)\s*(?<n>\d+)")]
    private static partial Regex IslandRank();

    [GeneratedRegex(@"Note \d+ from")]
    private static partial Regex VariantNotes();

    [GeneratedRegex(@"^Learn\s+(?<spell>[^;]+)")]
    private static partial Regex BlueMagic();

    [GeneratedRegex(@"\bachievement\b", RegexOptions.IgnoreCase)]
    private static partial Regex Achievement();

    [GeneratedRegex(@"\btame at least\b", RegexOptions.IgnoreCase)]
    private static partial Regex Beasts();

    [GeneratedRegex(@"Elemental Level\s*(?<n>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ElementalLevel();

    [GeneratedRegex(@"^My Little Chocobo")]
    private static partial Regex Chocobo();

    [GeneratedRegex(@"in your Armoury Chest|tool for your current class", RegexOptions.IgnoreCase)]
    private static partial Regex ToolHeld();

    [GeneratedRegex(@"unlocking the raid\s*[""“]?(?<duty>[^""”.]+)")]
    private static partial Regex DutyUnlocked();
}
