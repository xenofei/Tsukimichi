using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for Moonfall (feature plan v9, 1.23.0): the main window's button, the game window's bar, its pauses, the
/// shot and level tallies. English only until localization reopens. "Full Moon" is Moonfall's own name for the
/// original's end-of-level fever.
/// </summary>
static partial class Strings
{
    public static string MoonfallTitle => Loc.Get("MoonfallTitle");
    public static string MoonfallButton => Loc.Get("MoonfallButton");
    public static string MoonfallButtonTooltip => Loc.Get("MoonfallButtonTooltip");

    // ---- The bar ----

    /// <summary>{0} = the level's number in its campaign, {1} = its name.</summary>
    public static string MoonfallLevelFormat => Loc.Get("MoonfallLevelFormat");

    /// <summary>{0} = balls left in the launcher.</summary>
    public static string MoonfallBallsFormat => Loc.Get("MoonfallBallsFormat");

    /// <summary>{0} = orange pegs left.</summary>
    public static string MoonfallOrangesFormat => Loc.Get("MoonfallOrangesFormat");

    /// <summary>{0} = the multiplier.</summary>
    public static string MoonfallMultiplierFormat => Loc.Get("MoonfallMultiplierFormat");

    public static string MoonfallMultiplierTooltip => Loc.Get("MoonfallMultiplierTooltip");
    public static string MoonfallPause => Loc.Get("MoonfallPause");
    public static string MoonfallResume => Loc.Get("MoonfallResume");
    public static string MoonfallRestart => Loc.Get("MoonfallRestart");
    public static string MoonfallRestartQuestion => Loc.Get("MoonfallRestartQuestion");
    public static string MoonfallLeaveQuestion => Loc.Get("MoonfallLeaveQuestion");
    public static string MoonfallKeepPlaying => Loc.Get("MoonfallKeepPlaying");
    public static string MoonfallLeave => Loc.Get("MoonfallLeave");

    // ---- Pauses ----

    public static string MoonfallPausedCombat => Loc.Get("MoonfallPausedCombat");
    public static string MoonfallPausedDuty => Loc.Get("MoonfallPausedDuty");
    public static string MoonfallPausedCutscene => Loc.Get("MoonfallPausedCutscene");
    public static string MoonfallPausedFocus => Loc.Get("MoonfallPausedFocus");
    public static string MoonfallPaused => Loc.Get("MoonfallPaused");
    public static string MoonfallClickToResume => Loc.Get("MoonfallClickToResume");
    public static string MoonfallClickToPlay => Loc.Get("MoonfallClickToPlay");
    public static string MoonfallWaitsForIt => Loc.Get("MoonfallWaitsForIt");

    // ---- Shots and Full Moon ----

    /// <summary>{0} = the shot's peg values, {1} = its pegs cleared so far.</summary>
    public static string MoonfallShotFormat => Loc.Get("MoonfallShotFormat");

    public static string MoonfallFreeBall => Loc.Get("MoonfallFreeBall");
    public static string MoonfallFullMoon => Loc.Get("MoonfallFullMoon");
    public static string MoonfallPerfectMoon => Loc.Get("MoonfallPerfectMoon");

    // ---- The tally ----

    public static string MoonfallLevelClear => Loc.Get("MoonfallLevelClear");
    public static string MoonfallTallyLevel => Loc.Get("MoonfallTallyLevel");
    public static string MoonfallTallyFullMoon => Loc.Get("MoonfallTallyFullMoon");

    /// <summary>{0} = balls left.</summary>
    public static string MoonfallTallyBallsFormat => Loc.Get("MoonfallTallyBallsFormat");

    public static string MoonfallTallyTotal => Loc.Get("MoonfallTallyTotal");
    public static string MoonfallNextLevel => Loc.Get("MoonfallNextLevel");
    public static string MoonfallPlayAgain => Loc.Get("MoonfallPlayAgain");
    public static string MoonfallLastLevel => Loc.Get("MoonfallLastLevel");
    public static string MoonfallOutOfBalls => Loc.Get("MoonfallOutOfBalls");

    /// <summary>{0} = orange pegs left.</summary>
    public static string MoonfallOrangesLeftFormat => Loc.Get("MoonfallOrangesLeftFormat");

    public static string MoonfallTryAgain => Loc.Get("MoonfallTryAgain");
    public static string MoonfallNoLevels => Loc.Get("MoonfallNoLevels");
}
