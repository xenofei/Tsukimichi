using System.Globalization;
using System.Text;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Unlocks;

/// <summary>
/// The one-line forms of a quest's unlocks the other surfaces print (feature plan v6 K4): the table's tooltip ("Opens
/// Kugane (area) · The Sirensong Sea (dungeon) · +2"), the chat line and the overlay hint ("Kugane · The Sirensong
/// Sea"). Next quests are never part of them: Path and the counts line already say what opens next. A caller prints
/// nothing for a quest the spoiler shield masks.
/// </summary>
public static class UnlockText
{
    /// <summary>How many unlocks a one-line form names before "+N".</summary>
    public const int DefaultMax = 4;

    /// <summary>"Kugane (area) · The Sirensong Sea (dungeon) · Eastern Bow (emote) · +2"; empty when nothing but next quests.</summary>
    public static string Summary(IReadOnlyList<UnlockEntry> entries, int max = DefaultMax) => Join(entries, max, withKind: true);

    /// <summary>"Kugane · The Sirensong Sea · +1"; empty when nothing but next quests.</summary>
    public static string Names(IReadOnlyList<UnlockEntry> entries, int max = DefaultMax) => Join(entries, max, withKind: false);

    /// <summary>"Opens Kugane (area) · The Sirensong Sea (dungeon)": <see cref="Summary"/> after the word; empty when it is.</summary>
    public static string OpensLine(IReadOnlyList<UnlockEntry> entries, int max = DefaultMax) => Opens(Summary(entries, max));

    /// <summary>
    /// "Kugane · The Sirensong Sea": the names of the places and things the quest opens (areas, aetherytes, duties,
    /// features), leaving out actions, emotes and items, which a reward list already names; empty when none.
    /// </summary>
    public static string Places(IReadOnlyList<UnlockEntry> entries, int max = DefaultMax)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var places = new List<UnlockEntry>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Group <= UnlockGroup.Feature)
            {
                places.Add(entry);
            }
        }

        return Join(places, max, withKind: false);
    }

    /// <summary>"Opens Kugane · The Sirensong Sea": the word before already composed names; empty when they are.</summary>
    public static string Opens(string names) =>
        string.IsNullOrEmpty(names) ? string.Empty : string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Unlock.OpensFormat", "Opens {0}"), names);

    /// <summary>"Kugane (area)": a row's name with its kind word.</summary>
    public static string WithKind(UnlockEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Unlock.WithKindFormat", "{0} ({1})"), entry.Name, UnlockTargets.Name(entry.Target).ToLower(CultureInfo.CurrentCulture));
    }

    private static string Join(IReadOnlyList<UnlockEntry> entries, int max, bool withKind)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var builder = new StringBuilder();
        var shown = 0;
        var more = 0;
        foreach (var entry in entries)
        {
            if (entry.Target == UnlockTarget.NextQuest)
            {
                continue;
            }

            if (shown >= max)
            {
                more++;
                continue;
            }

            if (shown > 0)
            {
                builder.Append(Evaluation.BlockerText.Separator);
            }

            builder.Append(withKind ? WithKind(entry) : entry.Name);
            shown++;
        }

        if (more > 0)
        {
            builder.Append(Evaluation.BlockerText.Separator)
                .Append(string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Unlock.MoreFormat", "+{0}"), more));
        }

        return builder.ToString();
    }
}
