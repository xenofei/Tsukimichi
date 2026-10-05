using Tsukimichi.Core.Moonfall;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Moonfall's in-play chrome and reward moments (spec-rich2.md §1, §3, §4): the plates' labels, the banners, the tally's
/// callouts, the companions' names (the real FFXIV cast, decision 22) and the Peg marks option. English only.
/// </summary>
static partial class Strings
{
    /// <summary>The companion who carries <paramref name="power"/>: the real FFXIV character (spec-rich2.md §3).</summary>
    public static string MoonfallCompanionName(MoonfallPower power) => power switch
    {
        MoonfallPower.SuperGuide => Loc.Get("MoonfallCompanion.SuperGuide"),
        MoonfallPower.Multiball => Loc.Get("MoonfallCompanion.Multiball"),
        MoonfallPower.Wings => Loc.Get("MoonfallCompanion.Wings"),
        MoonfallPower.Burst => Loc.Get("MoonfallCompanion.Burst"),
        MoonfallPower.Flippers => Loc.Get("MoonfallCompanion.Flippers"),
        MoonfallPower.Gate => Loc.Get("MoonfallCompanion.Gate"),
        MoonfallPower.Bloom => Loc.Get("MoonfallCompanion.Bloom"),
        MoonfallPower.Draw => Loc.Get("MoonfallCompanion.Draw"),
        MoonfallPower.Fireball => Loc.Get("MoonfallCompanion.Fireball"),
        MoonfallPower.Path => Loc.Get("MoonfallCompanion.Path"),
        MoonfallPower.Bolt => Loc.Get("MoonfallCompanion.Bolt"),
        _ => Loc.Get("MoonfallPower.None"),
    };

    public static string MoonfallHudScore => Loc.Get("MoonfallHudScore");
    public static string MoonfallHudBalls => Loc.Get("MoonfallHudBalls");
    public static string MoonfallHudOranges => Loc.Get("MoonfallHudOranges");
    public static string MoonfallBannerFullMoon => Loc.Get("MoonfallBannerFullMoon");
    public static string MoonfallBannerPerfectMoon => Loc.Get("MoonfallBannerPerfectMoon");
    public static string MoonfallBannerFullMoonSub => Loc.Get("MoonfallBannerFullMoonSub");
    public static string MoonfallBannerLevelClear => Loc.Get("MoonfallBannerLevelClear");
    public static string MoonfallBannerOutOfBalls => Loc.Get("MoonfallBannerOutOfBalls");
    public static string MoonfallAced => Loc.Get("MoonfallAced");
    public static string MoonfallNewBest => Loc.Get("MoonfallNewBest");

    /// <summary>"ace score {0}".</summary>
    public static string MoonfallAceScoreFormat => Loc.Get("MoonfallAceScoreFormat");

    /// <summary>"best {0}".</summary>
    public static string MoonfallBestFormat => Loc.Get("MoonfallBestFormat");

    /// <summary>"{0} · {1} · balls left: {2}".</summary>
    public static string MoonfallTallyStageFormat => Loc.Get("MoonfallTallyStageFormat");

    public static string MoonfallCampaignName(MoonfallCampaignKind kind) =>
        kind == MoonfallCampaignKind.Expansion ? Loc.Get("MoonfallCampaignExpansion") : Loc.Get("MoonfallCampaignBase");

    /// <summary>"+{0}".</summary>
    public static string MoonfallStyleValueFormat => Loc.Get("MoonfallStyleValueFormat");

    public static string MoonfallOptions => Loc.Get("MoonfallOptions");
    public static string MoonfallPegMarks => Loc.Get("MoonfallPegMarks");
    public static string MoonfallPegMarksTooltip => Loc.Get("MoonfallPegMarksTooltip");
    public static string MoonfallPegMarksHint => Loc.Get("MoonfallPegMarksHint");
    public static string MoonfallTurnOn => Loc.Get("MoonfallTurnOn");
    public static string MoonfallNoThanks => Loc.Get("MoonfallNoThanks");

    public static string MoonfallTallyBallsLabel => Loc.Get("MoonfallTallyBallsLabel");

    public static string MoonfallTallyBallsCountFormat => Loc.Get("MoonfallTallyBallsCountFormat");
}
