using System.Text;
using Tsukimichi.Core.Model;

namespace Tsukimichi.DataGen;

/// <summary>Markdown review reports: docs/data/unique-report.md and docs/data/catalog-stats.md.</summary>
internal static class Reports
{
    private const int ExamplesPerKind = 10;

    // No timestamps anywhere in the reports: a regeneration that changes nothing must produce no diff. The date of
    // the last regeneration lives in docs/data/DATA-VERSION.md (tools/regen.ps1).
    public static string UniqueReport(GameSheets g, UniqueRewardGenerator gen, IReadOnlyCollection<UniqueRewardEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Unique reward report");
        sb.AppendLine();
        sb.AppendLine($"Generated from game version `{g.GameVersion}` by Tsukimichi.DataGen (see docs/data/DATA-VERSION.md for when).");
        sb.AppendLine();
        var questCount = entries.Select(e => e.QuestRowId).Distinct().Count();
        var flagged = entries.Count(e => e.OtherSources.Count > 0);
        sb.AppendLine($"- Entries: **{entries.Count}** across **{questCount}** quests.");
        sb.AppendLine($"- Entries whose reward is also obtainable elsewhere (`otherSources` non-empty): **{flagged}**. They keep confidence Static in V1; the UI shows the source and marks Online Store re-sells.");
        foreach (var grp in entries.SelectMany(e => e.OtherSources).GroupBy(s => s).OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal))
            sb.AppendLine($"  - `{grp.Key}`: {grp.Count()}");
        sb.AppendLine($"- Plain item rewards refused by the exclusivity rule ({(gen.StrictItemExclusivity ? "strict" : "legacy, --keep-nonexclusive-items")}): **{gen.Dropped.Count}** (listed at the end).");
        sb.AppendLine();

        sb.AppendLine("## Counts per kind and confidence");
        sb.AppendLine();
        var confidences = Enum.GetValues<Confidence>();
        sb.Append("| Kind |");
        foreach (var c in confidences) sb.Append($" {c} |");
        sb.AppendLine(" Total |");
        sb.Append("|---|");
        foreach (var _ in confidences) sb.Append("---:|");
        sb.AppendLine("---:|");
        foreach (var kind in Enum.GetValues<RewardKind>())
        {
            var ofKind = entries.Where(e => e.Kind == kind).ToList();
            if (ofKind.Count == 0)
                continue;
            sb.Append($"| {kind} |");
            foreach (var c in confidences) sb.Append($" {ofKind.Count(e => e.Confidence == c)} |");
            sb.AppendLine($" {ofKind.Count} |");
        }
        sb.Append("| **Total** |");
        foreach (var c in confidences) sb.Append($" {entries.Count(e => e.Confidence == c)} |");
        sb.AppendLine($" {entries.Count} |");
        sb.AppendLine();

        sb.AppendLine("## Examples per kind");
        sb.AppendLine();
        foreach (var group in entries.GroupBy(e => e.Kind).OrderBy(k => k.Key))
        {
            sb.AppendLine($"### {group.Key} ({group.Count()})");
            sb.AppendLine();
            sb.AppendLine("| Quest | Reward | Reward id | Item id | Confidence | Source |");
            sb.AppendLine("|---|---|---:|---:|---|---|");
            foreach (var e in group.OrderBy(e => e.QuestRowId).ThenBy(e => e.RewardId).Take(ExamplesPerKind))
                sb.AppendLine($"| {e.QuestRowId} {Md(QuestName(g, e.QuestRowId))} | {Md(e.RewardName)} | {e.RewardId} | {e.ItemId} | {e.Confidence} | `{e.Source}` |");
            sb.AppendLine();
        }

        sb.AppendLine("## Quests with rewards but no unique classification");
        sb.AppendLine();
        var classified = entries.Select(e => e.QuestRowId).ToHashSet();
        var withSignals = gen.RewardSignals.Keys.Where(q => !string.IsNullOrEmpty(QuestName(g, q))).ToList();
        var unclassifiedAll = withSignals.Where(q => !classified.Contains(q)).OrderBy(q => q).ToList();
        var interesting = unclassifiedAll
            .Where(q => gen.RewardSignals[q].Any(IsWorthReview))
            .ToList();
        sb.AppendLine($"Named quests that hand out at least one reward signal (item, emote, action, unlock or other) but produced no entry: **{unclassifiedAll.Count}** of {withSignals.Count}.");
        sb.AppendLine("Most of them only give tradable gear or consumables. The list below is restricted to the ones worth a second look: at least one reward item is untradable, or carries an ItemAction and is not sold on the market board, yet no rule claimed it.");
        sb.AppendLine();
        sb.AppendLine($"Quests to review: **{interesting.Count}**.");
        sb.AppendLine();
        sb.AppendLine("| Quest | Reward signals |");
        sb.AppendLine("|---|---|");
        foreach (var q in interesting)
        {
            var signals = gen.RewardSignals[q].Where(IsWorthReview);
            sb.AppendLine($"| {q} {Md(QuestName(g, q))} | {Md(string.Join("; ", signals))} |");
        }
        sb.AppendLine();

        var actionTypes = interesting
            .SelectMany(q => gen.RewardSignals[q])
            .Where(IsWorthReview)
            .Select(ExtractItemAction)
            .Where(t => t != 0)
            .GroupBy(t => t)
            .OrderByDescending(gr => gr.Count())
            .ToList();
        if (actionTypes.Count > 0)
        {
            sb.AppendLine("Unhandled ItemAction types among those rewards (candidates for a new rule):");
            sb.AppendLine();
            sb.AppendLine("| ItemAction type | Reward items |");
            sb.AppendLine("|---:|---:|");
            foreach (var t in actionTypes)
                sb.AppendLine($"| {t.Key} | {t.Count()} |");
            sb.AppendLine();
        }

        sb.AppendLine("## Item rewards refused by the exclusivity rule");
        sb.AppendLine();
        sb.AppendLine("Untradable, non-marketable item rewards that are nevertheless not quest-only collectibles: consumable-like ItemUICategory (potions, food, materials, currencies, tickets, vouchers, coffers), or also sold, crafted or gathered elsewhere. The Calamity Salvager's quest-reward reacquisition menus (\"Purchase Quest Rewards\", \"... Arms & Gear\", \"... Arms & Tools\") do not count; they only re-sell what the character already earned. Items whose ItemAction is an unlock (framer's kits, field notes) are kept whatever their category. Collectible kinds (mount, minion, emote, ...) are never affected. Pass `--keep-nonexclusive-items` to ship them anyway.");
        sb.AppendLine();
        sb.AppendLine("| Reason | Items dropped |");
        sb.AppendLine("|---|---:|");
        foreach (var grp in gen.Dropped.GroupBy(d => d.Reason).OrderByDescending(x => x.Count()).ThenBy(x => x.Key))
            sb.AppendLine($"| {Md(grp.Key)} | {grp.Count()} |");
        sb.AppendLine();
        sb.AppendLine("| Quest | Item | Reason |");
        sb.AppendLine("|---|---|---|");
        foreach (var d in gen.Dropped.OrderBy(d => d.QuestRowId).ThenBy(d => d.ItemId))
            sb.AppendLine($"| {d.QuestRowId} {Md(QuestName(g, d.QuestRowId))} | {d.ItemId} {Md(d.Name)} | {Md(d.Reason)} |");
        sb.AppendLine();

        return sb.ToString();
    }

    public static string CatalogStats(GameSheets g)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Quest catalog statistics");
        sb.AppendLine();
        sb.AppendLine($"Generated from game version `{g.GameVersion}` by Tsukimichi.DataGen (see docs/data/DATA-VERSION.md for when).");
        sb.AppendLine();

        var all = g.Quests.Where(q => q.RowId != 0).ToList();
        var named = all.Where(q => UniqueRewardGenerator.Text(q.Name).Length > 0).ToList();
        var unlisted = named.Count(q => q.JournalGenre.RowId == 0);
        sb.AppendLine($"- Quest rows: **{all.Count}**");
        sb.AppendLine($"- Named quests: **{named.Count}**");
        sb.AppendLine($"- Named quests with no journal genre (Unlisted): **{unlisted}**");
        sb.AppendLine($"- Repeatable named quests: **{named.Count(q => q.IsRepeatable)}**");
        sb.AppendLine();

        // section -> category -> count, resolved through JournalGenre. Row 0 is a real section (the A Realm Reborn
        // through Endwalker main scenario); quests with no genre at all are counted under the Unlisted sentinel.
        const uint Unlisted = uint.MaxValue;
        var perCategory = new Dictionary<uint, int>();
        var perSection = new Dictionary<uint, int>();
        var sectionOfCategory = new Dictionary<uint, uint>();
        foreach (var q in named)
        {
            uint categoryId, sectionId;
            if (q.JournalGenre.RowId == 0)
            {
                categoryId = Unlisted;
                sectionId = Unlisted;
            }
            else
            {
                var category = q.JournalGenre.ValueNullable?.JournalCategory;
                categoryId = category?.RowId ?? Unlisted;
                sectionId = category?.ValueNullable?.JournalSection.RowId ?? Unlisted;
            }
            perCategory[categoryId] = perCategory.GetValueOrDefault(categoryId) + 1;
            perSection[sectionId] = perSection.GetValueOrDefault(sectionId) + 1;
            sectionOfCategory[categoryId] = sectionId;
        }

        sb.AppendLine("## Quests per journal section");
        sb.AppendLine();
        sb.AppendLine("| Section id | Section | Named quests |");
        sb.AppendLine("|---:|---|---:|");
        foreach (var (sectionId, count) in perSection.OrderBy(kv => kv.Key))
            sb.AppendLine($"| {IdText(sectionId)} | {Md(SectionName(g, sectionId))} | {count} |");
        sb.AppendLine();

        sb.AppendLine("## Quests per journal category");
        sb.AppendLine();
        sb.AppendLine("| Section | Category id | Category | Named quests |");
        sb.AppendLine("|---|---:|---|---:|");
        foreach (var (categoryId, count) in perCategory
                     .OrderBy(kv => sectionOfCategory[kv.Key])
                     .ThenBy(kv => kv.Key))
        {
            var sectionId = sectionOfCategory[categoryId];
            var categoryName = categoryId == Unlisted
                ? "Unlisted (no journal genre)"
                : UniqueRewardGenerator.Text(g.JournalCategories.GetRowOrDefault(categoryId)?.Name);
            sb.AppendLine($"| {Md(SectionName(g, sectionId))} | {IdText(categoryId)} | {Md(categoryName)} | {count} |");
        }
        sb.AppendLine();

        return sb.ToString();

        static string IdText(uint id) => id == Unlisted ? "-" : id.ToString();
    }

    private static string SectionName(GameSheets g, uint sectionId)
    {
        if (sectionId == uint.MaxValue)
            return "Unlisted";
        var name = UniqueRewardGenerator.Text(g.JournalSections.GetRowOrDefault(sectionId)?.Name);
        return name.Length == 0 ? $"Section {sectionId}" : name;
    }

    private static string QuestName(GameSheets g, uint questRowId)
        => UniqueRewardGenerator.Text(g.Quests.GetRowOrDefault(questRowId)?.Name);

    /// <summary>A reward signal worth a human look: untradable, or an ItemAction item that is not on the market board.</summary>
    private static bool IsWorthReview(string signal)
        => signal.Contains("untradable") || (signal.Contains("ItemAction=") && !signal.Contains("marketable"));

    private static uint ExtractItemAction(string signal)
    {
        const string marker = "ItemAction=";
        var idx = signal.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
            return 0;
        var start = idx + marker.Length;
        var end = start;
        while (end < signal.Length && char.IsDigit(signal[end])) end++;
        return uint.TryParse(signal.AsSpan(start, end - start), out var v) ? v : 0;
    }

    private static string Md(string s) => s.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
