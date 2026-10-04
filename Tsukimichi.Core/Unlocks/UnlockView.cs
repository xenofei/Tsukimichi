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
/// <item>A row that belongs to Rewards (<see cref="UnlockEntry.InRewards"/>, <see cref="RewardSplit"/>) is never shown:
/// <see cref="QuestUnlocks.For"/> leaves it out, and so does <see cref="Visible"/> for a list that still holds it.</item>
/// <item>The wider shield (plan v7, 1.20.0 N6): a zone, duty or other row whose name sits past the character's story
/// point (<see cref="SpoilerMask.IsNameMasked"/>) prints its placeholder ("Area ahead (Lv 61)"), with no place in its
/// caption, no note and, past the generic map and aetheryte markers, no icon of its own (<see cref="Shielded"/>).</item>
/// </list>
/// </summary>
public static class UnlockView
{
    /// <summary>
    /// The rows to show: none when <paramref name="masked"/>, else those at or below <paramref name="reach"/>
    /// (<see cref="byte.MaxValue"/> outside Sprout mode) that the quest's Rewards do not already show, in their order,
    /// each row whose name <paramref name="spoilers"/> masks in its <see cref="Shielded"/> form. The list itself when
    /// nothing is left out or changed.
    /// </summary>
    public static IReadOnlyList<UnlockEntry> Visible(IReadOnlyList<UnlockEntry> entries, bool masked, byte reach = byte.MaxValue, SpoilerMask? spoilers = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (masked)
        {
            return [];
        }

        var shield = spoilers is { MasksNames: true } ? spoilers : null;
        var all = true;
        foreach (var entry in entries)
        {
            if (!Shows(entry, reach) || (shield is not null && IsShielded(entry, shield)))
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
            if (Shows(entry, reach))
            {
                shown.Add(shield is null ? entry : Shielded(entry, shield));
            }
        }

        return shown;
    }

    /// <summary>Whether the wider shield masks the row's name (a next quest's is the quest shield's to mask).</summary>
    public static bool IsShielded(UnlockEntry entry, SpoilerMask spoilers)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(spoilers);
        return SpoilerNames.KindOf(entry.Target) is { } kind && spoilers.IsNameMasked(kind, entry.Name);
    }

    /// <summary>
    /// The row as the wider shield shows it: its placeholder for a name past the story point, its caption without a
    /// place the shield masks (an aetheryte's zone, a zone's region), no note, and no icon of its own beyond the generic
    /// map and aetheryte markers (a mount's or a duty's art would name it). The row itself when nothing is masked; its
    /// target and reward are kept, so a click still knows what it is.
    /// </summary>
    public static UnlockEntry Shielded(UnlockEntry entry, SpoilerMask spoilers)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(spoilers);
        var nameMasked = IsShielded(entry, spoilers);
        var detailMasked = DetailIsPlace(entry) && spoilers.IsNameMasked(SpoilerKind.Area, entry.Detail);
        if (!nameMasked && !detailMasked)
        {
            return entry;
        }

        var generic = entry.Group is UnlockGroup.Area or UnlockGroup.Aetheryte || entry.Target == UnlockTarget.Flying;
        return entry with
        {
            Name = nameMasked ? NameOf(entry, spoilers) : entry.Name,
            Detail = detailMasked ? string.Empty : entry.Detail,
            Note = nameMasked ? null : entry.Note,
            Icon = nameMasked && !generic ? 0 : entry.Icon,
        };
    }

    /// <summary>The row's caption ("Area · Hingashi") without a place the wider shield masks ("Area").</summary>
    public static string CaptionOf(UnlockEntry entry, SpoilerMask spoilers)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(spoilers);
        return DetailIsPlace(entry) && spoilers.IsNameMasked(SpoilerKind.Area, entry.Detail) ? UnlockTargets.Name(entry.Target) : entry.Caption;
    }

    /// <summary>A zone's caption names its region, an aetheryte's its zone.</summary>
    private static bool DetailIsPlace(UnlockEntry entry) =>
        entry.Detail.Length > 0 && entry.Group is UnlockGroup.Area or UnlockGroup.Aetheryte;

    /// <summary>A row's name through the wider shield; a next quest's as it is (<see cref="NameOf(UnlockEntry, QuestCatalog, SpoilerMask)"/> shields it).</summary>
    private static string NameOf(UnlockEntry entry, SpoilerMask spoilers) =>
        SpoilerNames.KindOf(entry.Target) is { } kind ? spoilers.Name(kind, entry.Name) : entry.Name;

    /// <summary>
    /// Whether a surface draws the row: not one the quest's Rewards already show, and within Sprout mode's
    /// <paramref name="reach"/> (<see cref="byte.MaxValue"/>: every expansion).
    /// </summary>
    public static bool Shows(UnlockEntry entry, byte reach = byte.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return !entry.InRewards && InReach(entry, reach);
    }

    /// <summary>Whether Sprout mode's <paramref name="reach"/> shows the row (<see cref="byte.MaxValue"/>: every row).</summary>
    public static bool InReach(UnlockEntry entry, byte reach)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Expansion <= reach;
    }

    /// <summary>
    /// A row's name as printed: a next quest's through the quest shield, any other row's through the wider shield
    /// ("Area ahead (Lv 61)" for a zone past the story point, plan v7 1.20.0 N6).
    /// </summary>
    public static string NameOf(UnlockEntry entry, QuestCatalog catalog, SpoilerMask spoilers)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(spoilers);
        return entry.Target == UnlockTarget.NextQuest && catalog.GetByRowId(entry.TargetId) is { } quest
            ? spoilers.DisplayName(quest)
            : NameOf(entry, spoilers);
    }
}
