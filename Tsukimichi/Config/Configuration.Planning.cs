using System;
using System.Collections.Generic;

namespace Tsukimichi.Config;

/// <summary>
/// 1.9.0 planning extras (feature plan v5, R6 E and G). Drawn under Settings › Display › Planning
/// (<c>Ui/ConfigWindow.Planning.cs</c>).
/// </summary>
public sealed partial class Configuration
{
    /// <summary>The Journal table's EXP column (each quest's base EXP). Off by default; the detail pane shows EXP either way.</summary>
    public bool JournalShowExpColumn { get; set; }

    /// <summary>
    /// The Journal table's Unlocks column (feature plan v6 K4): up to three icons of what each quest unlocks. On by default
    /// since 1.12.1, at the owner's request; the tooltip and the detail pane's Unlocks section say it either way.
    /// </summary>
    public bool JournalShowOpensColumn { get; set; } = true;

    /// <summary>
    /// Set once the 1.12.1 change has turned the Unlocks column on for a configuration saved while it was off by default
    /// (1.12.0). Null on a configuration from before; after that the player's own choice stands.
    /// </summary>
    public bool? UnlocksColumnDefaultApplied { get; set; }

    /// <summary>
    /// Set once the Journal table has hidden its Giver column (1.15, F5) the first time it drew it: the column is off by
    /// default and shown from the table's header menu, and ImGui's saved table settings from before 1.15 would otherwise
    /// show a column they never knew. After that the player's own choice, kept by ImGui, stands.
    /// </summary>
    public bool JournalGiverColumnDefaulted { get; set; }

    /// <summary>The allied society board on the Characters dashboard. On by default.</summary>
    public bool ShowAlliedSocietyBoard { get; set; } = true;

    /// <summary>
    /// The Journal table's column widths the player dragged (feature plan v6 U9), by column name
    /// (<see cref="Core.Ui.QuestColumn"/>: "Level", "Job", "Status", "Expansion", "Rewards", "Exp", "Opens"), in logical
    /// pixels of content, so a width keeps its look at every UI scale. A column not listed is sized automatically; the
    /// table header's "Reset column widths" empties it. Unknown names and unreadable widths are dropped on load.
    /// </summary>
    public Dictionary<string, float> JournalColumnWidths { get; set; } = [];

    /// <summary>
    /// <paramref name="widths"/> with only the columns the player can size and a readable width each
    /// (<see cref="Core.Ui.TableGeometry.SanitizePlayerWidth"/>); a new dictionary.
    /// </summary>
    internal static Dictionary<string, float> SanitizeColumnWidths(Dictionary<string, float>? widths)
    {
        var clean = new Dictionary<string, float>(StringComparer.Ordinal);
        if (widths is null)
        {
            return clean;
        }

        foreach (var (name, width) in widths)
        {
            if (Enum.TryParse<Core.Ui.QuestColumn>(name, ignoreCase: false, out var column)
                && column is not (Core.Ui.QuestColumn.Glyph or Core.Ui.QuestColumn.Name)
                && Enum.IsDefined(column)
                && Core.Ui.TableGeometry.SanitizePlayerWidth(width) is > 0f and var kept)
            {
                clean[column.ToString()] = kept;
            }
        }

        return clean;
    }
}
