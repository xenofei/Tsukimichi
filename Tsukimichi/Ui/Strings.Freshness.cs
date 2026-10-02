using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for patch-day honesty (feature plan v5, 1.5.0 "Trust"): the main window's "Game updated" strip, the
/// "New since data" value of the Added in filter, and the chat line when a capture is held back as implausible.
/// </summary>
static partial class Strings
{
    /// <summary>The strip: {0} = quests newer than the data, one form (<see cref="Loc.Plural"/>).</summary>
    public static string FreshnessStrip(int count) => Loc.Plural(count, Loc.Get("FreshnessStripOneFormat"), Loc.Get("FreshnessStripFormat"));

    public static string FreshnessShowNew => Loc.Get("FreshnessShowNew");
    public static string FreshnessShowNewTooltip => Loc.Get("FreshnessShowNewTooltip");
    public static string FreshnessDismiss => Loc.Get("FreshnessDismiss");
    public static string FreshnessDismissTooltip => Loc.Get("FreshnessDismissTooltip");

    /// <summary>The Added in filter's "New since data" value, its chip and combo entry.</summary>
    public static string AddedInNewSinceData => Loc.Get("AddedInNewSinceData");

    /// <summary>The combo entry: {0} = quests newer than the data.</summary>
    public static string AddedInNewSinceDataOptionFormat => Loc.Get("AddedInNewSinceDataOptionFormat");

    /// <summary>The once-per-session chat line when an implausible capture was not saved (<c>Core.Runtime.CapturePlausibility</c>).</summary>
    public static string PlausibilitySkippedNotice => Loc.Get("PlausibilitySkippedNotice");

    /// <summary>The chat line when a held-back capture kept reading the same for a few minutes and is saved after all (<c>Core.Runtime.HeldBackCaptures</c>).</summary>
    public static string PlausibilityAcceptedNotice => Loc.Get("PlausibilityAcceptedNotice");
}
