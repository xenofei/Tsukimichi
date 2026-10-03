using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Unlocks;

/// <summary>
/// What of a quest's unlocks a surface may show (feature plan v6 K2, unlocks spec §3.3): the spoiler shield's rules
/// in one place, so the detail pane, the tooltips, the panels and the chat lines agree.
/// <list type="bullet">
/// <item>A quest the shield masks shows none of its unlocks: no names, no icons, no group captions.</item>
/// <item>A next quest prints its name through the shield (<see cref="SpoilerMask.DisplayName(QuestRecord)"/>): "Main
/// scenario quest (Lv 83)".</item>
/// <item>Sprout mode leaves out rows past the character's reach (<see cref="SpoilerMask.ReachExpansion"/>): a warp on
/// a side quest that names a later expansion's city.</item>
/// </list>
/// </summary>
public static class UnlockView
{
    /// <summary>
    /// The rows to show: none when <paramref name="masked"/>, else those at or below <paramref name="reach"/>
    /// (<see cref="byte.MaxValue"/> outside Sprout mode), in their order. The list itself when nothing is left out.
    /// </summary>
    public static IReadOnlyList<UnlockEntry> Visible(IReadOnlyList<UnlockEntry> entries, bool masked, byte reach = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (masked)
        {
            return [];
        }

        if (reach == byte.MaxValue)
        {
            return entries;
        }

        var all = true;
        foreach (var entry in entries)
        {
            if (entry.Expansion > reach)
            {
                all = false;
                break;
            }
        }

        if (all)
        {
            return entries;
        }

        var shown = new List<UnlockEntry>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Expansion <= reach)
            {
                shown.Add(entry);
            }
        }

        return shown;
    }

    /// <summary>A row's name as printed: a next quest's through the shield, any other row's as it is.</summary>
    public static string NameOf(UnlockEntry entry, QuestCatalog catalog, SpoilerMask spoilers)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(spoilers);
        return entry.Target == UnlockTarget.NextQuest && catalog.GetByRowId(entry.TargetId) is { } quest
            ? spoilers.DisplayName(quest)
            : entry.Name;
    }

    /// <summary>
    /// The one-line summary a surface prints for a quest ("Kugane (area) · The Sirensong Sea (dungeon)"): empty when
    /// the shield masks the quest or it opens nothing but next quests.
    /// </summary>
    public static string Summary(QuestUnlocksSource source, QuestRecord quest, SpoilerMask spoilers)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(spoilers);
        return spoilers.IsMasked(quest) ? string.Empty : source.Summary(quest.RowId);
    }
}
