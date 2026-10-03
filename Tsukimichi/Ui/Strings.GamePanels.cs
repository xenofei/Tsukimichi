using System.Collections.Generic;
using System.Globalization;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the panels beside game windows (1.7.0): "Worth it?" beside the quest offer, "What this opened"
/// beside the quest completion, the Journal companion, and their settings in Settings › Integrations. Constant names
/// carry the <c>GamePanel</c> prefix so this part of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    /// <summary>ImGui id of the "Worth it?" panel window (never shown).</summary>
    public const string GamePanelOfferWindowId = "##tsukimichi-offer-panel";

    /// <summary>ImGui id of the "What this opened" panel window (never shown).</summary>
    public const string GamePanelResultWindowId = "##tsukimichi-result-panel";

    /// <summary>ImGui id of the Journal companion window (never shown).</summary>
    public const string GamePanelJournalWindowId = "##tsukimichi-journal-panel";

    public static string GamePanelOfferCaption => Loc.Get("GamePanelOfferCaption");

    public static string GamePanelResultCaption => Loc.Get("GamePanelResultCaption");

    public static string GamePanelJournalCaption => Loc.Get("GamePanelJournalCaption");

    public static string GamePanelMasked => Loc.Get("GamePanelMasked");

    public static string GamePanelOwned => Loc.Get("GamePanelOwned");

    public static string GamePanelNotOwned => Loc.Get("GamePanelNotOwned");

    public static string GamePanelOwnedUnknown => Loc.Get("GamePanelOwnedUnknown");

    public static string GamePanelMoonlitHeading => Loc.Get("GamePanelMoonlitHeading");

    public static string GamePanelUnlocksHeading => Loc.Get("GamePanelUnlocksHeading");

    public static string GamePanelChainNext => Loc.Get("GamePanelChainNext");

    /// <summary>{0} the patch the quest was added in.</summary>
    public static string GamePanelAddedInFormat => Loc.Get("GamePanelAddedInFormat");

    public static string GamePanelRepeatable => Loc.Get("GamePanelRepeatable");

    public static string GamePanelSocietyDaily => Loc.Get("GamePanelSocietyDaily");

    public static string GamePanelSeasonal => Loc.Get("GamePanelSeasonal");

    public static string GamePanelOpen => Loc.Get("GamePanelOpen");

    public static string GamePanelOpenHint => Loc.Get("GamePanelOpenHint");

    public static string GamePanelPin => Loc.Get("GamePanelPin");

    public static string GamePanelUnpin => Loc.Get("GamePanelUnpin");

    public static string GamePanelPinHint => Loc.Get("GamePanelPinHint");

    public static string GamePanelPinUnavailable => Loc.Get("GamePanelPinUnavailable");

    public static string GamePanelRoute => Loc.Get("GamePanelRoute");

    public static string GamePanelRouteHint => Loc.Get("GamePanelRouteHint");

    /// <summary>{0} an opened quest's name, when only another class or job can take it.</summary>
    public static string GamePanelOtherJobFormat => Loc.Get("GamePanelOtherJobFormat");

    public static string GamePanelOpenedNone => Loc.Get("GamePanelOpenedNone");

    /// <summary>{0} how many more quests the completion opens.</summary>
    public static string GamePanelMoreFormat => Loc.Get("GamePanelMoreFormat");

    /// <summary>Settings › Integrations block heading over the three panel toggles.</summary>
    public static string GamePanelSettingsHeading => Loc.Get("GamePanelSettingsHeading");

    public static string GamePanelSettingOffer => Loc.Get("GamePanelSettingOffer");

    public static string GamePanelSettingOfferHint => Loc.Get("GamePanelSettingOfferHint");

    public static string GamePanelSettingResult => Loc.Get("GamePanelSettingResult");

    public static string GamePanelSettingResultHint => Loc.Get("GamePanelSettingResultHint");

    public static string GamePanelSettingJournal => Loc.Get("GamePanelSettingJournal");

    public static string GamePanelSettingJournalHint => Loc.Get("GamePanelSettingJournalHint");

    /// <summary>"Opens 1 main scenario quest, 2 unlock quests, 8 other quests": the kinds with none are left out.</summary>
    public static string GamePanelOpenedSummary(int mainScenario, int feature, int other)
    {
        var parts = new List<string>(3);
        AddCount(parts, mainScenario, "GamePanelOpenedMsqOne", "GamePanelOpenedMsqFormat");
        AddCount(parts, feature, "GamePanelOpenedFeatureOne", "GamePanelOpenedFeatureFormat");
        AddCount(parts, other, "GamePanelOpenedSideOne", "GamePanelOpenedSideFormat");
        return parts.Count == 0
            ? GamePanelOpenedNone
            : string.Format(CultureInfo.CurrentCulture, Loc.Get("GamePanelOpenedFormat"), string.Join(Loc.Get("GamePanelOpenedJoin"), parts));
    }

    private static void AddCount(List<string> parts, int count, string oneKey, string manyKey)
    {
        if (count == 1)
        {
            parts.Add(Loc.Get(oneKey));
        }
        else if (count > 1)
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, Loc.Get(manyKey), count));
        }
    }
}
