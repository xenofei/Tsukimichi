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
    /// The Journal table's Opens column (feature plan v6 K4): up to three icons of what each quest opens. Off by default;
    /// the tooltip and the detail pane's Unlocks section say it either way.
    /// </summary>
    public bool JournalShowOpensColumn { get; set; }

    /// <summary>The allied society board on the Characters dashboard. On by default.</summary>
    public bool ShowAlliedSocietyBoard { get; set; } = true;
}
