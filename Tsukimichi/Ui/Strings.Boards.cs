using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for 1.21.0 P6 and P7 (feature plan v7; spec-1.21): the Triple Triad card, its route stops and Unlocks
/// line, and Nearby's Here · Everywhere view (the zones board).
/// </summary>
static partial class Strings
{
    public static string TriadHeading => Loc.Get("TriadHeading");

    public static string TriadTooltip => Loc.Get("TriadTooltip");

    public static string TriadToUnlockOne => Loc.Get("TriadToUnlockOne");

    /// <summary>{0} = opponents behind a quest, masked ones included.</summary>
    public static string TriadToUnlockFormat => Loc.Get("TriadToUnlockFormat");

    public static string TriadDoneNone => Loc.Get("TriadDoneNone");

    public static string TriadDoneOne => Loc.Get("TriadDoneOne");

    /// <summary>{0} = opponents beaten with every card owned; they leave the list.</summary>
    public static string TriadDoneFormat => Loc.Get("TriadDoneFormat");

    public static string TriadChipPlaysYou => Loc.Get("TriadChipPlaysYou");

    /// <summary>A chip. {0} = opponents that play this character and still have something for it.</summary>
    public static string TriadChipPlaysYouFormat => Loc.Get("TriadChipPlaysYouFormat");

    /// <summary>A chip. {0} = opponents behind a quest.</summary>
    public static string TriadChipLockedFormat => Loc.Get("TriadChipLockedFormat");

    /// <summary>A chip. {0} = opponents with cards this character does not have.</summary>
    public static string TriadChipCardsLeftFormat => Loc.Get("TriadChipCardsLeftFormat");

    public static string TriadChipPlaysYouTip => Loc.Get("TriadChipPlaysYouTip");

    public static string TriadChipLockedTip => Loc.Get("TriadChipLockedTip");

    public static string TriadChipCardsLeftTip => Loc.Get("TriadChipCardsLeftTip");

    public static string TriadPlaysYouGroup => Loc.Get("TriadPlaysYouGroup");

    public static string TriadPlaysYouSub => Loc.Get("TriadPlaysYouSub");

    public static string TriadLockedGroup => Loc.Get("TriadLockedGroup");

    public static string TriadLockedSub => Loc.Get("TriadLockedSub");

    public static string TriadNotRead => Loc.Get("TriadNotRead");

    public static string TriadCardsOne => Loc.Get("TriadCardsOne");

    /// <summary>{0} = the opponent's cards this character does not have.</summary>
    public static string TriadCardsFormat => Loc.Get("TriadCardsFormat");

    public static string TriadNotBeatenAllCards => Loc.Get("TriadNotBeatenAllCards");

    public static string TriadBeatenCardsOne => Loc.Get("TriadBeatenCardsOne");

    /// <summary>{0} = the opponent's cards this character does not have.</summary>
    public static string TriadBeatenCardsFormat => Loc.Get("TriadBeatenCardsFormat");

    public static string TriadAfter => Loc.Get("TriadAfter");

    /// <summary>After the quest an opponent waits on. {0} = the quest's state ("Ready").</summary>
    public static string TriadStateFormat => Loc.Get("TriadStateFormat");

    /// <summary>After the quest an opponent waits on. {0} = its state ("Blocked"), {1} = what blocks it.</summary>
    public static string TriadStateReasonFormat => Loc.Get("TriadStateReasonFormat");

    public static string TriadWaitsForStory => Loc.Get("TriadWaitsForStory");

    public static string TriadOpponentAhead => Loc.Get("TriadOpponentAhead");

    public static string TriadZoneAhead => Loc.Get("TriadZoneAhead");

    /// <summary>{0} = masked opponents folded away, {1} = the character's first name.</summary>
    public static string TriadMoreMaskedFormat => Loc.Get("TriadMoreMaskedFormat");

    public static string TriadRouteOne => Loc.Get("TriadRouteOne");

    /// <summary>{0} = the opponents behind a quest the card names.</summary>
    public static string TriadRouteFormat => Loc.Get("TriadRouteFormat");

    public static string TriadRouteLabelOne => Loc.Get("TriadRouteLabelOne");

    /// <summary>The Route window's title after "Route to". {0} = how many.</summary>
    public static string TriadRouteLabelFormat => Loc.Get("TriadRouteLabelFormat");

    public static string TriadRouteTip => Loc.Get("TriadRouteTip");

    public static string TriadPinQuests => Loc.Get("TriadPinQuests");

    public static string TriadPinTip => Loc.Get("TriadPinTip");

    public static string TriadPinnedTip => Loc.Get("TriadPinnedTip");

    public static string TriadMenuShowQuest => Loc.Get("TriadMenuShowQuest");

    /// <summary>{0} = the aetheryte's name.</summary>
    public static string TriadTeleportFormat => Loc.Get("TriadTeleportFormat");

    public static string TriadNoAetheryte => Loc.Get("TriadNoAetheryte");

    public static string TriadUnlockPrefix => Loc.Get("TriadUnlockPrefix");

    /// <summary>After the opponent's name in the Unlocks line. {0} = where it stands.</summary>
    public static string TriadUnlockPlaceFormat => Loc.Get("TriadUnlockPlaceFormat");

    public static string TriadUnlockMasked => Loc.Get("TriadUnlockMasked");

    public static string TriadUnlockTip => Loc.Get("TriadUnlockTip");

    /// <summary>A Triple Triad opponent as a stop on a route. {0} = the opponent's name.</summary>
    public static string RouteTriadStopFormat => Loc.Get("RouteTriadStopFormat");

    public static string RouteTriadMark => Loc.Get("RouteTriadMark");

    public static string RouteTriadTooltip => Loc.Get("RouteTriadTooltip");

    public static string RouteTriadFlagTooltip => Loc.Get("RouteTriadFlagTooltip");

    public static string BoardRowMoreTooltip => Loc.Get("BoardRowMoreTooltip");

    public static string NearbyHere => Loc.Get("NearbyHere");

    public static string NearbyEverywhere => Loc.Get("NearbyEverywhere");

    public static string NearbyHereTip => Loc.Get("NearbyHereTip");

    public static string NearbyEverywhereTip => Loc.Get("NearbyEverywhereTip");

    public static string NearbyChipReady => Loc.Get("NearbyChipReady");

    public static string NearbyChipBlues => Loc.Get("NearbyChipBlues");

    public static string NearbyChipSideStories => Loc.Get("NearbyChipSideStories");

    public static string NearbyChipRewards => Loc.Get("NearbyChipRewards");

    public static string NearbyChipReadyTip => Loc.Get("NearbyChipReadyTip");

    public static string NearbyChipBluesTip => Loc.Get("NearbyChipBluesTip");

    public static string NearbyChipSideStoriesTip => Loc.Get("NearbyChipSideStoriesTip");

    public static string NearbyChipRewardsTip => Loc.Get("NearbyChipRewardsTip");

    /// <summary>{0} = the sort ("Level fit").</summary>
    public static string NearbySortFormat => Loc.Get("NearbySortFormat");

    public static string NearbySortLevelFit => Loc.Get("NearbySortLevelFit");

    public static string NearbySortReadyFirst => Loc.Get("NearbySortReadyFirst");

    public static string NearbySortStoryOrder => Loc.Get("NearbySortStoryOrder");

    public static string NearbySortTip => Loc.Get("NearbySortTip");

    public static string NearbyHereKindsEmpty => Loc.Get("NearbyHereKindsEmpty");

    /// <summary>An expansion's heading on the zones board. {0} = expansion, {1}-{2} = its level span.</summary>
    public static string ZoneEyebrowFormat => Loc.Get("ZoneEyebrowFormat");

    /// <summary>After the heading of the expansion that fits the job. {0} = job abbreviation, {1} = its level.</summary>
    public static string ZoneFitsFormat => Loc.Get("ZoneFitsFormat");

    /// <summary>A zone's level span.</summary>
    public static string ZoneLevelSpanFormat => Loc.Get("ZoneLevelSpanFormat");

    /// <summary>A zone whose quests share one level.</summary>
    public static string ZoneLevelOneFormat => Loc.Get("ZoneLevelOneFormat");

    /// <summary>A chip: the zone has quests added since patch {0}.</summary>
    public static string ZoneNewSinceFormat => Loc.Get("ZoneNewSinceFormat");

    /// <summary>{0} = quests startable now in the zone.</summary>
    public static string ZoneReadyFormat => Loc.Get("ZoneReadyFormat");

    public static string ZoneBluesOne => Loc.Get("ZoneBluesOne");

    /// <summary>{0} = unlock quests left in the zone.</summary>
    public static string ZoneBluesFormat => Loc.Get("ZoneBluesFormat");

    public static string ZoneSideStoriesOne => Loc.Get("ZoneSideStoriesOne");

    /// <summary>{0} = side story quests left in the zone.</summary>
    public static string ZoneSideStoriesFormat => Loc.Get("ZoneSideStoriesFormat");

    public static string ZoneRewardsOne => Loc.Get("ZoneRewardsOne");

    /// <summary>{0} = quests left in the zone with a Moonlit reward.</summary>
    public static string ZoneRewardsFormat => Loc.Get("ZoneRewardsFormat");

    public static string ZoneSeparator => Loc.Get("ZoneSeparator");

    /// <summary>A folded expansion. {0} = Ready quests, {1} = zones with something left.</summary>
    public static string ZoneFoldedReadyFormat => Loc.Get("ZoneFoldedReadyFormat");

    /// <summary>A folded expansion when Ready is off. {0} = zones.</summary>
    public static string ZoneFoldedLeftFormat => Loc.Get("ZoneFoldedLeftFormat");

    /// <summary>An expansion past the story point. {0} = its zones, {1} = the character's first name.</summary>
    public static string ZoneMaskedExpansionFormat => Loc.Get("ZoneMaskedExpansionFormat");

    public static string ZoneAhead => Loc.Get("ZoneAhead");

    public static string ZoneWaitsForStory => Loc.Get("ZoneWaitsForStory");

    /// <summary>{0} = masked zones folded away.</summary>
    public static string ZoneMoreAheadFormat => Loc.Get("ZoneMoreAheadFormat");

    public static string ZoneNothingLeftOne => Loc.Get("ZoneNothingLeftOne");

    /// <summary>{0} = zones with nothing left of the chips that are on; a click lists them.</summary>
    public static string ZoneNothingLeftFormat => Loc.Get("ZoneNothingLeftFormat");

    public static string ZoneNothingLeftLine => Loc.Get("ZoneNothingLeftLine");

    public static string ZoneMenuNearestReady => Loc.Get("ZoneMenuNearestReady");

    public static string ZoneMenuNearestReadyTip => Loc.Get("ZoneMenuNearestReadyTip");

    public static string ZoneMenuShowInJournal => Loc.Get("ZoneMenuShowInJournal");

    public static string ZoneMenuShowInJournalTip => Loc.Get("ZoneMenuShowInJournalTip");

    public static string ZoneMenuSendQuestionable => Loc.Get("ZoneMenuSendQuestionable");

    /// <summary>{0} = the zone's aetheryte.</summary>
    public static string ZoneTeleportFormat => Loc.Get("ZoneTeleportFormat");

    public static string ZoneNoAetheryte => Loc.Get("ZoneNoAetheryte");

    public static string ZoneEmpty => Loc.Get("ZoneEmpty");
}
