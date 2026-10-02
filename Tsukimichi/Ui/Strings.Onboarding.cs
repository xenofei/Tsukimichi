using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for onboarding and play-time reach (feature plan v5, 1.7.0): the rail's Overlay and Nearby buttons, the
/// first-pin prompt, the context bar, the "Set up your road" card and the command line's "did you mean".
/// </summary>
public static partial class Strings
{
    // Rail foot (Overlay and Nearby beside Help and Settings)
    public static string RailOverlayOffTooltip => Loc.Get("RailOverlayOffTooltip");
    public static string RailOverlayOnTooltip => Loc.Get("RailOverlayOnTooltip");
    public static string RailNearbyTooltip => Loc.Get("RailNearbyTooltip");

    // The first pin while the overlay is off
    public static string PinOverlayPrompt => Loc.Get("PinOverlayPrompt");
    public static string PinOverlayTurnOn => Loc.Get("PinOverlayTurnOn");
    public static string PinOverlayTurnOnTooltip => Loc.Get("PinOverlayTurnOnTooltip");
    public static string PinOverlayNotNow => Loc.Get("PinOverlayNotNow");

    // Context bar: a stored character or several filters carried over from last time
    /// <summary>{0} = character name.</summary>
    public static string ContextViewingFormat => Loc.Get("ContextViewingFormat");

    /// <summary>"3 filters on"; {0} = count.</summary>
    public static string ContextFilters(int count) => Loc.Plural(count, Loc.Get("ContextFilterOne"), Loc.Get("ContextFiltersFormat"));

    public static string ContextFollowMe => Loc.Get("ContextFollowMe");
    public static string ContextFollowMeTooltip => Loc.Get("ContextFollowMeTooltip");
    public static string ContextClear => Loc.Get("ContextClear");
    public static string ContextClearTooltip => Loc.Get("ContextClearTooltip");
    public static string ContextDismissTooltip => Loc.Get("ContextDismissTooltip");
    public const string ContextSeparator = " · ";

    // Empty states with a reset (Moonlit, My blues)
    public static string MoonlitEmptyBody => Loc.Get("MoonlitEmptyBody");
    public static string PlanEmptyFilteredBody => Loc.Get("PlanEmptyFilteredBody");
    public static string PlanEmptyAllDoneHeading => Loc.Get("PlanEmptyAllDoneHeading");

    // Command line
    /// <summary>{0} = subcommand.</summary>
    public static string CommandDidYouMeanFormat => Loc.Get("CommandDidYouMeanFormat");
    public static string RouteCommandNoSelection => Loc.Get("RouteCommandNoSelection");

    /// <summary>The "Set up your road" card (decision 7: the overlay stays off until the player turns it on).</summary>
    public static class Setup
    {
        public static string Title => Loc.Get("Setup.Title");
        public static string Lede => Loc.Get("Setup.Lede");
        public static string OverlayLabel => Loc.Get("Setup.OverlayLabel");
        public static string OverlayValue => Loc.Get("Setup.OverlayValue");
        public static string NoticeLabel => Loc.Get("Setup.NoticeLabel");
        public static string NoticeValue => Loc.Get("Setup.NoticeValue");
        public static string ServerBarLabel => Loc.Get("Setup.ServerBarLabel");
        public static string ServerBarValue => Loc.Get("Setup.ServerBarValue");
        public static string ItemHintsLabel => Loc.Get("Setup.ItemHintsLabel");
        public static string ItemHintsValue => Loc.Get("Setup.ItemHintsValue");
        public static string DutyFinderLabel => Loc.Get("Setup.DutyFinderLabel");
        public static string DutyFinderValue => Loc.Get("Setup.DutyFinderValue");
        public static string Companions => Loc.Get("Setup.Companions");
        public static string CompanionsButton => Loc.Get("Setup.CompanionsButton");
        public static string CompanionsTooltip => Loc.Get("Setup.CompanionsTooltip");
        public static string Recommended => Loc.Get("Setup.Recommended");
        public static string RecommendedTooltip => Loc.Get("Setup.RecommendedTooltip");
        public static string Done => Loc.Get("Setup.Done");
        public static string DoneTooltip => Loc.Get("Setup.DoneTooltip");
    }
}
