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

    /// <summary>The allied society board on the Characters dashboard. On by default.</summary>
    public bool ShowAlliedSocietyBoard { get; set; } = true;
}
