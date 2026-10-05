using System.Globalization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Unique;

/// <summary>The Moonlit table column a header click sorts on; <see cref="Default"/> is the usual order (the catalog's).</summary>
public enum MoonlitSortColumn
{
    Default,
    Obtained,
    Reward,
    Kind,
    Quest,
    State,
    Confidence,
    Availability,
}

/// <summary>The Moonlit table's sort: a column and its direction. <see cref="Default"/> keeps the usual order whatever the direction.</summary>
public readonly record struct MoonlitSortSpec(MoonlitSortColumn Column, bool Descending)
{
    public static readonly MoonlitSortSpec Default = new(MoonlitSortColumn.Default, false);

    /// <summary>Whether this is the usual order (the third header click, or none yet).</summary>
    public bool IsDefault => Column == MoonlitSortColumn.Default;
}

/// <summary>
/// What a Moonlit row sorts by, as the row shows it. <paramref name="Reward"/> and <paramref name="Quest"/> are the
/// names printed in the row: a name the spoiler shield hides is its placeholder here, so the order never tells what the
/// real name is. <paramref name="QuestMaskedLevel"/> is the level a masked quest's placeholder prints ("Main scenario
/// quest (Lv 90)"), or -1 for a quest whose name shows: two masked quests order by that level as a number, as the quest
/// table's Name column does (<see cref="Query.SpoilerMask.CompareDisplayNames"/>).
/// </summary>
/// <param name="Obtained">Whether the character has the reward; null when it cannot be read.</param>
/// <param name="Reward">The reward's printed name.</param>
/// <param name="Kind">The reward's kind.</param>
/// <param name="Quest">The printed name of the quest the row shows.</param>
/// <param name="QuestMaskedLevel">The masked quest's placeholder level; -1 when the quest's name shows.</param>
/// <param name="State">The quest's state for the viewed character (<see cref="QuestState.Unknown"/> without an evaluation).</param>
/// <param name="Confidence">The confidence the row shows (a row a verdict hides shows <see cref="Confidence.UserOverride"/>).</param>
/// <param name="Availability">Whether, and until when, the reward can still be had.</param>
public readonly record struct MoonlitSortKey(
    bool? Obtained,
    string Reward,
    RewardKind Kind,
    string Quest,
    int QuestMaskedLevel,
    QuestState State,
    Confidence Confidence,
    RewardAvailabilityInfo Availability);

/// <summary>
/// The Moonlit table's column sort (owner request, 1.22.x): a header click sorts by that column, a second reverses it,
/// a third goes back to the usual order (ImGui's tristate). Every column sorts by what it means, never by its glyph or
/// label text: Obtained puts owned rewards first, then the unreadable, then the missing; Reward and Quest by the printed
/// name, culture-aware and case-insensitive; Kind in <see cref="RewardKind"/> order (the kinds list's order); State in
/// <see cref="QuestState"/> order (the quest table's State sort); Confidence by its ordinal; Availability by its kind,
/// then the soonest announced end. Ties keep the usual order in either direction, so the result is stable.
/// </summary>
public static class MoonlitSort
{
    /// <summary>The Moonlit table's columns in declaration order: what an ImGui sort spec's column index names.</summary>
    private static readonly MoonlitSortColumn[] TableColumns =
    [
        MoonlitSortColumn.Obtained,
        MoonlitSortColumn.Reward,
        MoonlitSortColumn.Kind,
        MoonlitSortColumn.Quest,
        MoonlitSortColumn.State,
        MoonlitSortColumn.Confidence,
        MoonlitSortColumn.Availability,
    ];

    /// <summary>The column at a table column index (declaration order, whatever the player's reorder); Default when out of range.</summary>
    public static MoonlitSortColumn ColumnAt(int tableColumn) =>
        tableColumn >= 0 && tableColumn < TableColumns.Length ? TableColumns[tableColumn] : MoonlitSortColumn.Default;

    /// <summary>The table column index of a sort column; -1 for <see cref="MoonlitSortColumn.Default"/>.</summary>
    public static int TableColumnOf(MoonlitSortColumn column) => Array.IndexOf(TableColumns, column);

    /// <summary>
    /// The sort the table's headers ask for: no spec (the third click, or a table never sorted) is the usual order, else
    /// the first spec's column and direction.
    /// </summary>
    public static MoonlitSortSpec FromHeader(int specsCount, int tableColumn, bool descending)
    {
        var column = specsCount > 0 ? ColumnAt(tableColumn) : MoonlitSortColumn.Default;
        return column == MoonlitSortColumn.Default ? MoonlitSortSpec.Default : new MoonlitSortSpec(column, descending);
    }

    /// <summary>
    /// Compares two rows on one column, ascending; 0 for a tie (the caller keeps the usual order then). Names compare
    /// under <paramref name="culture"/> (the current culture when null), ignoring case.
    /// </summary>
    public static int Compare(in MoonlitSortKey a, in MoonlitSortKey b, MoonlitSortColumn column, CultureInfo? culture = null) => column switch
    {
        MoonlitSortColumn.Obtained => ObtainedRank(a.Obtained).CompareTo(ObtainedRank(b.Obtained)),
        MoonlitSortColumn.Reward => CompareNames(a.Reward, b.Reward, culture),
        MoonlitSortColumn.Kind => ((int)a.Kind).CompareTo((int)b.Kind),
        MoonlitSortColumn.Quest => a.QuestMaskedLevel >= 0 && b.QuestMaskedLevel >= 0
            ? a.QuestMaskedLevel.CompareTo(b.QuestMaskedLevel)
            : CompareNames(a.Quest, b.Quest, culture),
        MoonlitSortColumn.State => ((int)a.State).CompareTo((int)b.State),
        MoonlitSortColumn.Confidence => ((int)a.Confidence).CompareTo((int)b.Confidence),
        MoonlitSortColumn.Availability => CompareAvailability(a.Availability, b.Availability),
        _ => 0,
    };

    /// <summary>
    /// Sorts <paramref name="items"/> (in the usual order) by <paramref name="sort"/>, in place and stable: ties keep
    /// the order they arrived in, in either direction. The usual order leaves the list as it is. <paramref name="keyOf"/>
    /// is read once per item.
    /// </summary>
    public static void Apply(List<int> items, Func<int, MoonlitSortKey> keyOf, MoonlitSortSpec sort, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(keyOf);
        if (sort.IsDefault || items.Count < 2)
        {
            return;
        }

        var keys = new MoonlitSortKey[items.Count];
        var order = new int[items.Count];
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = keyOf(items[i]);
            order[i] = i;
        }

        var compareCulture = culture ?? CultureInfo.CurrentCulture;
        Array.Sort(order, (x, y) =>
        {
            if (x == y)
            {
                return 0;
            }

            var c = Compare(keys[x], keys[y], sort.Column, compareCulture);
            if (c == 0)
            {
                return x.CompareTo(y);
            }

            return sort.Descending ? -c : c;
        });

        var sorted = new int[order.Length];
        for (var i = 0; i < order.Length; i++)
        {
            sorted[i] = items[order[i]];
        }

        for (var i = 0; i < sorted.Length; i++)
        {
            items[i] = sorted[i];
        }
    }

    /// <summary>Owned first, then the unreadable, then the missing: either direction leads with a definite answer.</summary>
    private static int ObtainedRank(bool? obtained) => obtained switch
    {
        true => 0,
        null => 1,
        false => 2,
    };

    private static int CompareNames(string? a, string? b, CultureInfo? culture) =>
        string.Compare(a ?? string.Empty, b ?? string.Empty, culture ?? CultureInfo.CurrentCulture, CompareOptions.IgnoreCase);

    /// <summary>By kind (Get now first, Gone for good last), then the soonest announced end; an end not announced after any that is.</summary>
    private static int CompareAvailability(RewardAvailabilityInfo a, RewardAvailabilityInfo b)
    {
        var byKind = ((int)a.Kind).CompareTo((int)b.Kind);
        if (byKind != 0)
        {
            return byKind;
        }

        return (a.EndsUtc, b.EndsUtc) switch
        {
            ({ } endA, { } endB) => endA.CompareTo(endB),
            ({ }, null) => -1,
            (null, { }) => 1,
            _ => 0,
        };
    }
}
