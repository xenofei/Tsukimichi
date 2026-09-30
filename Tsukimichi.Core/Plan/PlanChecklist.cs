using System.Globalization;
using System.Text;

namespace Tsukimichi.Core.Plan;

/// <summary>
/// "Copy as checklist" (P3): the plan as Markdown that reads well pasted into Discord or a document. One heading per
/// expansion, one task line per quest:
/// <code>
/// ## A Realm Reborn (2)
/// - [ ] Hallo Halatali (Lv 20, Western Thanalan) — unlocks: Dungeon: Halatali
/// - [ ] Legacy of Allag (Lv 50, Mor Dhona) — leads to: Alliance raid
/// </code>
/// Names are the ones the plan carries (spoiler-masked), and nothing names the character, its world or its progress
/// beyond the list itself, so the text can be shared as is. Markdown characters in names are escaped.
/// </summary>
public static class PlanChecklist
{
    /// <summary>Names printed per kind before "+N more".</summary>
    public const int MaxNamesPerKind = 3;

    public const string Title = "# Clear my blues";
    public const string Footer = "_Made with Tsukimichi (Dalamud plugin)_";

    /// <param name="plan">The plan as shown (already filtered).</param>
    /// <param name="zoneName">The zone's name for the line ("Western Thanalan"); empty prints the level alone.</param>
    public static string Write(UnlockPlan plan, Func<PlanZone, string> zoneName)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(zoneName);

        var sb = new StringBuilder();
        sb.Append(Title).Append(" (").Append(plan.Count.ToString(CultureInfo.InvariantCulture)).Append(')').Append('\n');
        foreach (var expansion in plan.Expansions)
        {
            sb.Append('\n').Append("## ").Append(Escape(expansion.Name)).Append(" (")
                .Append(expansion.Count.ToString(CultureInfo.InvariantCulture)).Append(')').Append('\n');
            foreach (var zone in expansion.Zones)
            {
                var zoneText = zoneName(zone) ?? string.Empty;
                foreach (var entry in zone.Entries)
                {
                    AppendLine(sb, entry, zoneText);
                }
            }
        }

        sb.Append('\n').Append(Footer).Append('\n');
        return sb.ToString();
    }

    /// <summary>"- [ ] Name (Lv N, Zone) — unlocks: Dungeon: The Tam-Tara Deepcroft", without the line break.</summary>
    public static string Line(PlanEntry entry, string zone)
    {
        var sb = new StringBuilder();
        AppendLine(sb, entry, zone ?? string.Empty);
        return sb.ToString(0, sb.Length - 1);
    }

    /// <summary>
    /// What the entry unlocks as the checklist prints it: "unlocks: Dungeon: Halatali; System: Retainers" for named
    /// unlocks (at most <see cref="MaxNamesPerKind"/> names per kind, then "+N more"), "leads to: Alliance raid" for an
    /// inherited kind, empty when nothing is known.
    /// </summary>
    public static string UnlocksText(PlanEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var parts = new List<string>();
        var inherited = new List<string>();
        foreach (var kind in UnlockKinds.All)
        {
            var names = new List<string>();
            var any = false;
            var isInherited = false;
            foreach (var unlock in entry.Unlocks)
            {
                if (unlock.Kind != kind)
                {
                    continue;
                }

                any = true;
                isInherited |= unlock.Inherited;
                if (!unlock.Inherited && unlock.Name.Length > 0)
                {
                    names.Add(unlock.Name);
                }
            }

            if (!any)
            {
                continue;
            }

            if (isInherited && names.Count == 0)
            {
                inherited.Add(UnlockKinds.Name(kind));
                continue;
            }

            if (names.Count == 0)
            {
                if (kind != UnlockKind.Other)
                {
                    parts.Add(UnlockKinds.Name(kind));
                }

                continue;
            }

            var shown = names.Count <= MaxNamesPerKind ? names : names.GetRange(0, MaxNamesPerKind);
            var text = UnlockKinds.Name(kind) + ": " + string.Join(", ", shown);
            if (names.Count > shown.Count)
            {
                text += string.Format(CultureInfo.InvariantCulture, " +{0} more", names.Count - shown.Count);
            }

            parts.Add(text);
        }

        if (parts.Count > 0)
        {
            return "unlocks: " + string.Join("; ", parts);
        }

        return inherited.Count > 0 ? "leads to: " + string.Join("; ", inherited) : string.Empty;
    }

    private static void AppendLine(StringBuilder sb, PlanEntry entry, string zone)
    {
        sb.Append("- [ ] ").Append(Escape(entry.Name)).Append(" (Lv ")
            .Append(entry.Quest.DisplayLevel.ToString(CultureInfo.InvariantCulture));
        if (zone.Length > 0)
        {
            sb.Append(", ").Append(Escape(zone));
        }

        sb.Append(')');
        var unlocks = UnlocksText(entry);
        if (unlocks.Length > 0)
        {
            sb.Append(" — ").Append(Escape(unlocks));
        }

        sb.Append('\n');
    }

    /// <summary>Backslash before the characters Discord and CommonMark read as formatting.</summary>
    internal static string Escape(string text)
    {
        if (text.AsSpan().IndexOfAny("\\*_~`|[]<>") < 0)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length + 4);
        foreach (var c in text)
        {
            if (c is '\\' or '*' or '_' or '~' or '`' or '|' or '[' or ']' or '<' or '>')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}
