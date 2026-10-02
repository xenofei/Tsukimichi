using System.Globalization;
using System.Text;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Plan;

namespace Tsukimichi.Core.Unique;

/// <summary>One missing reward for "Copy missing": what the Moonlit row shows, in the order the rows are listed.</summary>
/// <param name="Section">The heading the line goes under (the reward's kind, or its expansion when the pane groups by expansion).</param>
/// <param name="Reward">The reward's name as the row shows it.</param>
/// <param name="Quest">The quest's name through the spoiler shield.</param>
/// <param name="Note">The availability when it is worth saying ("Event running (ends Oct 5)"); empty for "Get now".</param>
public sealed record MoonlitMissingLine(string Section, string Reward, string Quest, string Note);

/// <summary>
/// "Copy missing" (feature plan v5, Moonlit): the rewards the Moonlit tab lists as not obtained, as Markdown that reads
/// well pasted into Discord: plain bullets (Discord shows no task boxes), one heading per section, names escaped:
/// <code>
/// # Moonlit: missing rewards (3)
///
/// ## Mounts (2)
/// - Magitek Armor — The Steps of Faith
/// - Company Chocobo — My Little Chocobo · Gone for good
/// </code>
/// Quest names come in already passed through the spoiler shield and nothing names the character. Longer than one
/// Discord message, the caller copies it in parts (<see cref="Text.MessageSplitter"/>).
/// </summary>
public static class MoonlitMarkdown
{
    public static string Title => CoreText.T("Core.Moonlit.MissingTitle", "Moonlit: missing rewards");

    /// <summary>The Markdown for <paramref name="lines"/>; sections in the order they first appear.</summary>
    public static string Write(IReadOnlyList<MoonlitMissingLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var sections = new List<string>();
        var bySection = new Dictionary<string, List<MoonlitMissingLine>>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            if (!bySection.TryGetValue(line.Section, out var list))
            {
                list = [];
                bySection[line.Section] = list;
                sections.Add(line.Section);
            }

            list.Add(line);
        }

        var sb = new StringBuilder();
        sb.Append("# ").Append(PlanChecklist.Escape(Title)).Append(" (").Append(lines.Count.ToString(CultureInfo.InvariantCulture)).Append(")\n");
        foreach (var section in sections)
        {
            var list = bySection[section];
            sb.Append('\n');
            if (section.Length > 0)
            {
                sb.Append("## ").Append(PlanChecklist.Escape(section)).Append(" (").Append(list.Count.ToString(CultureInfo.InvariantCulture)).Append(")\n");
            }

            foreach (var line in list)
            {
                sb.Append("- ").Append(PlanChecklist.Escape(line.Reward));
                if (line.Quest.Length > 0)
                {
                    sb.Append(" — ").Append(PlanChecklist.Escape(line.Quest));
                }

                if (line.Note.Length > 0)
                {
                    sb.Append(" · ").Append(PlanChecklist.Escape(line.Note));
                }

                sb.Append('\n');
            }
        }

        sb.Append('\n').Append(PlanChecklist.Footer).Append('\n');
        return sb.ToString();
    }
}
