namespace Tsukimichi.Config;

/// <summary>
/// 1.7.0 "In the game" settings (feature plan v5): chat actions, the "Opened:" line, Chat 2, nameplate marks and quest
/// toasts. Drawn under Settings › Notices › Chat actions and Settings › Integrations › Chat 2 and nameplates (<c>Ui/ConfigWindow.InGame.cs</c>).
/// </summary>
public sealed partial class Configuration
{
    /// <summary>
    /// "[Open] [Pin] [Route]" after the quest link on Tsukimichi's own chat lines (notices, /tsuki search, zone, which
    /// and why). On by default.
    /// </summary>
    public bool ChatLinkActions { get; set; } = true;

    /// <summary>
    /// "Opened: 2 unlock quests, 8 side quests (3 with a story) · [Show]" after a quest completion opened something
    /// (R9 F1). On by default; separate from <see cref="ChatNoticeNewlyAvailable"/>.
    /// </summary>
    public bool ChatNoticeOpened { get; set; } = true;

    /// <summary>"Open in Tsukimichi" in Chat 2's Integrations submenu on quest and item links (C6 #2). On by default; does nothing without Chat 2.</summary>
    public bool ChatTwoIntegration { get; set; } = true;

    /// <summary>"☾ Pinned", "☾ Moonlit reward" or "☾ Ready on WHM" under quest givers' names (R8 D). Off by default.</summary>
    public bool NamePlateMarks { get; set; }

    /// <summary>The game's quest toast when a completion gives a Moonlit reward or unlocks a duty (R8 I). Off by default.</summary>
    public bool QuestToasts { get; set; }
}
