using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the 1.9.0 collector extras (feature plan v5): the story recap ("Previously…"), the achievements that
/// need several quests, the New Game+ replay badge, the free-trial view and the Moonlit tab's Added in filter. English
/// only until the localization freeze lifts.
/// </summary>
static partial class Strings
{
    // ---- Story recap ----
    public static string RecapWindowTitle => Loc.Get("RecapWindowTitle");

    public static string RecapMsqHeading => Loc.Get("RecapMsqHeading");

    /// <summary>{0} = how many quests the page reads.</summary>
    public static string RecapMsqIntroFormat => Loc.Get("RecapMsqIntroFormat");

    public static string RecapMsqIntroOne => Loc.Get("RecapMsqIntroOne");

    /// <summary>{0} = the chain's name.</summary>
    public static string RecapChainHeadingFormat => Loc.Get("RecapChainHeadingFormat");

    /// <summary>{0} = quests done in the chain, {1} = quests in the chain.</summary>
    public static string RecapChainIntroFormat => Loc.Get("RecapChainIntroFormat");

    public static string RecapNoCharacter => Loc.Get("RecapNoCharacter");

    public static string RecapNoReader => Loc.Get("RecapNoReader");

    public static string RecapEmptyHeading => Loc.Get("RecapEmptyHeading");

    public static string RecapEmptyMsq => Loc.Get("RecapEmptyMsq");

    public static string RecapEmptyChain => Loc.Get("RecapEmptyChain");

    public static string RecapCopy => Loc.Get("RecapCopy");

    public static string RecapCopyTooltip => Loc.Get("RecapCopyTooltip");

    /// <summary>{0} = quests read so far, {1} = quests to read.</summary>
    public static string RecapReadingFormat => Loc.Get("RecapReadingFormat");

    public static string RecapNeutral => Loc.Get("RecapNeutral");

    public static string RecapReadMsq => Loc.Get("RecapReadMsq");

    public static string RecapReadMsqTooltip => Loc.Get("RecapReadMsqTooltip");

    public static string RecapReadChain => Loc.Get("RecapReadChain");

    public static string RecapReadChainTooltip => Loc.Get("RecapReadChainTooltip");

    /// <summary>{0} = the quest's name.</summary>
    public static string RecapNoChainFormat => Loc.Get("RecapNoChainFormat");

    public static string RecapNotStarted => Loc.Get("RecapNotStarted");

    // ---- Detail pane: Story card ----
    public static string CollectorCard => Loc.Get("CollectorCard");

    public static string ReplayableInNewGamePlus => Loc.Get("ReplayableInNewGamePlus");

    public static string ReplayableInNewGamePlusTooltip => Loc.Get("ReplayableInNewGamePlusTooltip");

    public static string OnceOnly => Loc.Get("OnceOnly");

    public static string OnceOnlyTooltip => Loc.Get("OnceOnlyTooltip");

    // ---- Achievements that need several quests ----
    public static string LaddersHeading => Loc.Get("LaddersHeading");

    public static string LaddersHeadingTooltip => Loc.Get("LaddersHeadingTooltip");

    public static string LaddersColumnAchievement => Loc.Get("LaddersColumnAchievement");

    /// <summary>{0} = the achievement, {1} = quests done, {2} = quests it needs, {3} = earned or not.</summary>
    public static string LadderLineFormat => Loc.Get("LadderLineFormat");

    /// <summary>{0} = the achievement, {1} = quests it needs.</summary>
    public static string LadderNeedsFormat => Loc.Get("LadderNeedsFormat");

    public static string LadderEarned => Loc.Get("LadderEarned");

    public static string LadderNotEarned => Loc.Get("LadderNotEarned");

    public static string LadderStillToDo => Loc.Get("LadderStillToDo");

    public static string LadderTooltip => Loc.Get("LadderTooltip");

    public static string LadderTooltipFromGame => Loc.Get("LadderTooltipFromGame");

    // ---- Once-only story filter ----
    public static string OnceOnlyStoryFilter => Loc.Get("OnceOnlyStoryFilter");

    public static string OnceOnlyStoryFilterTooltip => Loc.Get("OnceOnlyStoryFilterTooltip");

    public static string OnceOnlyStoryChip => Loc.Get("OnceOnlyStoryChip");

    // ---- Free-trial view ----
    public static string TrialBeyondHeading => Loc.Get("TrialBeyondHeading");

    /// <summary>{0} = quests beyond the trial in the table.</summary>
    public static string TrialBeyondCaptionFormat => Loc.Get("TrialBeyondCaptionFormat");

    public static string TrialBeyondCaptionOne => Loc.Get("TrialBeyondCaptionOne");

    /// <summary>{0} = quests of the node beyond the trial.</summary>
    public static string TrialBeyondCountFormat => Loc.Get("TrialBeyondCountFormat");

    /// <summary>{0} = unlock quests in the expansions after the trial's.</summary>
    public static string PlanBeyondTrialFormat => Loc.Get("PlanBeyondTrialFormat");

    // ---- Settings › Display › Free trial and story recap ----
    public static string CollectorSettingsHeading => Loc.Get("CollectorSettingsHeading");

    public static string TrialSetting => Loc.Get("TrialSetting");

    public static string TrialSettingHint => Loc.Get("TrialSettingHint");

    public static string TrialDetected => Loc.Get("TrialDetected");

    public static string RecapLengthSetting => Loc.Get("RecapLengthSetting");

    public static string RecapLengthHint => Loc.Get("RecapLengthHint");

    /// <summary>ImGui printf format of the slider: %d = quests.</summary>
    public static string RecapLengthFormat => Loc.Get("RecapLengthFormat");

    // ---- Moonlit: Added in ----
    public static string MoonlitAddedInTooltip => Loc.Get("MoonlitAddedInTooltip");

    /// <summary>{0} = series ("7.5"), {1} = rewards whose quest it added.</summary>
    public static string MoonlitAddedInOptionFormat => Loc.Get("MoonlitAddedInOptionFormat");
}
