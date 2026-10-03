using Tsukimichi.Core.Portraits;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for 1.15.0 "Faces and icons" (feature plan v7 F2, F5): the giver portrait's source line, the Giver
/// portraits setting and the Journal's Giver column. English only until localization reopens.
/// </summary>
static partial class Strings
{
    /// <summary>The hover tooltip's source line under the giver's name: where the face comes from.</summary>
    public static string PortraitSource(PortraitSource source) => source switch
    {
        Core.Portraits.PortraitSource.TrustBust or Core.Portraits.PortraitSource.TrustStrip => Loc.Get("Portrait.SourceDutySupport"),
        Core.Portraits.PortraitSource.TripleTriadCard => Loc.Get("Portrait.SourceTripleTriad"),
        Core.Portraits.PortraitSource.BattleTalk => Loc.Get("Portrait.SourceBattleTalk"),
        Core.Portraits.PortraitSource.Delivery => Loc.Get("Portrait.SourceDelivery"),
        _ => string.Empty,
    };

    public static string SettingsGiverPortraits => Loc.Get("Settings.GiverPortraits");

    public static string SettingsGiverPortraitsHint => Loc.Get("Settings.GiverPortraitsHint");

    public static string SettingsGiverPortraitsOff => Loc.Get("Settings.GiverPortraitsOff");

    public static string SettingsGiverPortraitsGameArt => Loc.Get("Settings.GiverPortraitsGameArt");

    /// <summary>The Journal table's Giver column header.</summary>
    public static string ColumnGiver => Loc.Get("ColumnGiver");

    public static string ColumnGiverTooltip => Loc.Get("ColumnGiverTooltip");

    /// <summary>The Journal's Giver cell for a quest the spoiler shield masks, in place of the giver's name.</summary>
    public static string GiverHidden => Loc.Get("Portrait.GiverHidden");
}
