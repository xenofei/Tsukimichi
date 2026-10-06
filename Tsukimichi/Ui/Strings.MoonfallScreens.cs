using Tsukimichi.Core.Moonfall;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Moonfall's screens (feature plan v9, 1.23.0): the title, the map, level select, the companions, Quick Play, the
/// challenges, the duel, the pause menu, Options and the tally's ways on. English only until localization reopens.
/// </summary>
static partial class Strings
{
    /// <summary>Moonfall title: the logotype, in capitals.</summary>
    public static string MoonfallLogotype => Loc.Get("MoonfallLogotype");

    /// <summary>Moonfall title: the line under the logotype.</summary>
    public static string MoonfallSubtitle => Loc.Get("MoonfallSubtitle");

    /// <summary>Moonfall title: the Adventure entry (opens the map).</summary>
    public static string MoonfallScreenAdventure => Loc.Get("MoonfallScreenAdventure");

    /// <summary>Moonfall title and screen: Quick Play.</summary>
    public static string MoonfallScreenQuickPlay => Loc.Get("MoonfallScreenQuickPlay");

    /// <summary>Moonfall title and screen: the challenges.</summary>
    public static string MoonfallScreenChallenges => Loc.Get("MoonfallScreenChallenges");

    /// <summary>Moonfall title and screen: the duel against a companion.</summary>
    public static string MoonfallScreenDuel => Loc.Get("MoonfallScreenDuel");

    /// <summary>Moonfall title and screen: the eleven companions.</summary>
    public static string MoonfallScreenCompanions => Loc.Get("MoonfallScreenCompanions");

    /// <summary>Moonfall title and screen: Moonfall&#x27;s settings.</summary>
    public static string MoonfallScreenOptions => Loc.Get("MoonfallScreenOptions");

    /// <summary>Moonfall title: the Continue card&#x27;s button when no next level is built yet (opens the map).</summary>
    public static string MoonfallScreenMap => Loc.Get("MoonfallScreenMap");

    /// <summary>Moonfall title, under Adventure: {0} campaign name, {1} the stage the player is on, {2} stages in it.</summary>
    public static string MoonfallTitleAdventureSubFormat => Loc.Get("MoonfallTitleAdventureSubFormat");

    /// <summary>Moonfall title, under Quick Play.</summary>
    public static string MoonfallTitleQuickSub => Loc.Get("MoonfallTitleQuickSub");

    /// <summary>Moonfall title, under Challenges: {0} challenges won, {1} challenges in all.</summary>
    public static string MoonfallTitleChallengesSubFormat => Loc.Get("MoonfallTitleChallengesSubFormat");

    /// <summary>Moonfall: why the challenges are locked (short).</summary>
    public static string MoonfallChallengesSealed => Loc.Get("MoonfallChallengesSealed");

    /// <summary>Moonfall: the locked Challenges entry&#x27;s tooltip.</summary>
    public static string MoonfallChallengesSealedTooltip => Loc.Get("MoonfallChallengesSealedTooltip");

    /// <summary>Moonfall title, under Duel: {0} the companion offered first.</summary>
    public static string MoonfallTitleDuelSubFormat => Loc.Get("MoonfallTitleDuelSubFormat");

    /// <summary>Moonfall title, under Duel, when the story has introduced no companion yet.</summary>
    public static string MoonfallTitleDuelNone => Loc.Get("MoonfallTitleDuelNone");

    /// <summary>Moonfall: why Quick Play is locked.</summary>
    public static string MoonfallQuickPlayLocked => Loc.Get("MoonfallQuickPlayLocked");

    /// <summary>Moonfall title: the Continue card&#x27;s caption, in capitals.</summary>
    public static string MoonfallContinueCaps => Loc.Get("MoonfallContinueCaps");

    /// <summary>Moonfall title: the Continue button; {0} the level&#x27;s code (3-3).</summary>
    public static string MoonfallContinueFormat => Loc.Get("MoonfallContinueFormat");

    /// <summary>Moonfall title at 640: under Continue; {0} the level&#x27;s name, {1} the companion.</summary>
    public static string MoonfallContinueSmallFormat => Loc.Get("MoonfallContinueSmallFormat");

    /// <summary>Moonfall: a level never won.</summary>
    public static string MoonfallNotYetWon => Loc.Get("MoonfallNotYetWon");

    /// <summary>Moonfall: a level&#x27;s best (or &#x27;not yet won&#x27;) and its Ace score; {0} best text, {1} the Ace score.</summary>
    public static string MoonfallBestAceFormat => Loc.Get("MoonfallBestAceFormat");

    /// <summary>Moonfall level select tile: {0} the level&#x27;s best score.</summary>
    public static string MoonfallTileBestFormat => Loc.Get("MoonfallTileBestFormat");

    /// <summary>Moonfall level select tile: {0} the level&#x27;s Ace score.</summary>
    public static string MoonfallAceFormat => Loc.Get("MoonfallAceFormat");

    /// <summary>Moonfall: the companion of a level; {0} their name.</summary>
    public static string MoonfallWithFormat => Loc.Get("MoonfallWithFormat");

    /// <summary>Moonfall: a level on the stage where the player picks the companion.</summary>
    public static string MoonfallYourPick => Loc.Get("MoonfallYourPick");

    /// <summary>Moonfall title: the Continue card when every built level is won.</summary>
    public static string MoonfallRoadGoesOn => Loc.Get("MoonfallRoadGoesOn");

    /// <summary>Moonfall title: the Continue card&#x27;s line when every built level is won.</summary>
    public static string MoonfallRoadGoesOnLine => Loc.Get("MoonfallRoadGoesOnLine");

    /// <summary>Moonfall: Alphinaud and Alisaie in a line (&#x27;with the twins&#x27;).</summary>
    public static string MoonfallTheTwins => Loc.Get("MoonfallTheTwins");

    /// <summary>Moonfall: the Back button (Esc does the same).</summary>
    public static string MoonfallBack => Loc.Get("MoonfallBack");

    /// <summary>Moonfall: the button back to the Adventure map (level select, the tally).</summary>
    public static string MoonfallMap => Loc.Get("MoonfallMap");

    /// <summary>Moonfall map: the locked campaign tab&#x27;s tooltip.</summary>
    public static string MoonfallFarShoreSealed => Loc.Get("MoonfallFarShoreSealed");

    /// <summary>Moonfall map: stages reached, of all; {0} reached, {1} in all.</summary>
    public static string MoonfallStagesCountFormat => Loc.Get("MoonfallStagesCountFormat");

    /// <summary>Moonfall map: the selected stage&#x27;s number, in capitals.</summary>
    public static string MoonfallStageCapsFormat => Loc.Get("MoonfallStageCapsFormat");

    /// <summary>Moonfall: a companion and their power; {0} name, {1} power.</summary>
    public static string MoonfallCarrierFormat => Loc.Get("MoonfallCarrierFormat");

    /// <summary>Moonfall: a companion the story has not introduced, with their power; {0} the power.</summary>
    public static string MoonfallNotMetPowerFormat => Loc.Get("MoonfallNotMetPowerFormat");

    /// <summary>Moonfall map: the free-choice stage&#x27;s carrier line.</summary>
    public static string MoonfallYourPickLine => Loc.Get("MoonfallYourPickLine");

    /// <summary>Moonfall: a stage or level not reached yet (a locked button).</summary>
    public static string MoonfallNotReached => Loc.Get("MoonfallNotReached");

    /// <summary>Moonfall map: the Play button of a stage whose levels are all won (opens level select).</summary>
    public static string MoonfallChooseLevel => Loc.Get("MoonfallChooseLevel");

    /// <summary>Moonfall: a level not built yet.</summary>
    public static string MoonfallLevelComing => Loc.Get("MoonfallLevelComing");

    /// <summary>Moonfall level select: a level not built yet.</summary>
    public static string MoonfallLevelComingLine => Loc.Get("MoonfallLevelComingLine");

    /// <summary>Moonfall map: the next level to play in the stage&#x27;s list.</summary>
    public static string MoonfallNext => Loc.Get("MoonfallNext");

    /// <summary>Moonfall: the Play button; {0} the level&#x27;s code (3-3).</summary>
    public static string MoonfallPlayFormat => Loc.Get("MoonfallPlayFormat");

    /// <summary>Moonfall: the Play button.</summary>
    public static string MoonfallPlay => Loc.Get("MoonfallPlay");

    /// <summary>Moonfall map: the locked Play button&#x27;s tooltip.</summary>
    public static string MoonfallStageSealedTooltip => Loc.Get("MoonfallStageSealedTooltip");

    /// <summary>Moonfall map at 640: the stage panel&#x27;s tooltip.</summary>
    public static string MoonfallSeeLevelsTooltip => Loc.Get("MoonfallSeeLevelsTooltip");

    /// <summary>Moonfall map legend: a stage won.</summary>
    public static string MoonfallLegendWon => Loc.Get("MoonfallLegendWon");

    /// <summary>Moonfall map legend: the stage the player is on.</summary>
    public static string MoonfallLegendHere => Loc.Get("MoonfallLegendHere");

    /// <summary>Moonfall map legend: a stage not reached.</summary>
    public static string MoonfallLegendNotReached => Loc.Get("MoonfallLegendNotReached");

    /// <summary>Moonfall map legend: a companion the story has not introduced.</summary>
    public static string MoonfallLegendNotMet => Loc.Get("MoonfallLegendNotMet");

    /// <summary>Moonfall map legend: the stage where the player picks the companion.</summary>
    public static string MoonfallLegendPick => Loc.Get("MoonfallLegendPick");

    /// <summary>Moonfall map tooltip: a stage&#x27;s state.</summary>
    public static string MoonfallStageWon => Loc.Get("MoonfallStageWon");

    /// <summary>Moonfall map tooltip: a stage&#x27;s state.</summary>
    public static string MoonfallStageHere => Loc.Get("MoonfallStageHere");

    /// <summary>Moonfall map tooltip: a stage&#x27;s state.</summary>
    public static string MoonfallStageOpen => Loc.Get("MoonfallStageOpen");

    /// <summary>Moonfall map tooltip: a stage&#x27;s state.</summary>
    public static string MoonfallStageNotReached => Loc.Get("MoonfallStageNotReached");

    /// <summary>Moonfall map tooltip: {0} stage number, {1} stage name, {2} its state, {3} its power.</summary>
    public static string MoonfallStopTipFormat => Loc.Get("MoonfallStopTipFormat");

    /// <summary>Moonfall map tooltip: why a stop shows the card back.</summary>
    public static string MoonfallStopNotMetLine => Loc.Get("MoonfallStopNotMetLine");

    /// <summary>Moonfall map tooltip: why a stop is locked.</summary>
    public static string MoonfallStopSealedLine => Loc.Get("MoonfallStopSealedLine");

    /// <summary>Moonfall level select: {0} the campaign in capitals, {1} the stage number.</summary>
    public static string MoonfallLevelsHeaderFormat => Loc.Get("MoonfallLevelsHeaderFormat");

    /// <summary>Moonfall level select: {0} the companion, {1} their power.</summary>
    public static string MoonfallLevelsLineFormat => Loc.Get("MoonfallLevelsLineFormat");

    /// <summary>Moonfall level select: {0} the power (the companion is hidden by the spoiler shield).</summary>
    public static string MoonfallLevelsNotMetLineFormat => Loc.Get("MoonfallLevelsNotMetLineFormat");

    /// <summary>Moonfall level select: the free-choice stage&#x27;s line.</summary>
    public static string MoonfallLevelsPickLine => Loc.Get("MoonfallLevelsPickLine");

    /// <summary>Moonfall level select: a sealed level; {0} the level before it.</summary>
    public static string MoonfallOpensAfterFormat => Loc.Get("MoonfallOpensAfterFormat");

    /// <summary>Moonfall level select: the line on how to win.</summary>
    public static string MoonfallClearTheOranges => Loc.Get("MoonfallClearTheOranges");

    /// <summary>Moonfall level select: a level without an Ace score.</summary>
    public static string MoonfallNoAce => Loc.Get("MoonfallNoAce");

    /// <summary>Moonfall: no companion picked.</summary>
    public static string MoonfallNoCompanion => Loc.Get("MoonfallNoCompanion");

    /// <summary>Moonfall level select at 640: the Ace score&#x27;s caption.</summary>
    public static string MoonfallAceCaps => Loc.Get("MoonfallAceCaps");

    /// <summary>Moonfall level select: the Ace score&#x27;s caption.</summary>
    public static string MoonfallAceScoreCaps => Loc.Get("MoonfallAceScoreCaps");

    /// <summary>Moonfall: a companion the story has not introduced (the card back).</summary>
    public static string MoonfallNotYetMet => Loc.Get("MoonfallNotYetMet");

    /// <summary>Moonfall companions grid at 640: a companion not yet met.</summary>
    public static string MoonfallNotMetShort => Loc.Get("MoonfallNotMetShort");

    /// <summary>Moonfall companions: the line under the title.</summary>
    public static string MoonfallCompanionsLine => Loc.Get("MoonfallCompanionsLine");

    /// <summary>Moonfall companions: the footnote on the three faces.</summary>
    public static string MoonfallCompanionsFootnote => Loc.Get("MoonfallCompanionsFootnote");

    /// <summary>Moonfall companions: a companion met but not reached; {0} the power, {1} their stage.</summary>
    public static string MoonfallPowerStageFormat => Loc.Get("MoonfallPowerStageFormat");

    /// <summary>Moonfall companions at 640: {0} the power (short), {1} their stage.</summary>
    public static string MoonfallPowerStageShortFormat => Loc.Get("MoonfallPowerStageShortFormat");

    /// <summary>Moonfall companions: the moogle, met but not reached; {0} the power.</summary>
    public static string MoonfallPowerFarShoreFormat => Loc.Get("MoonfallPowerFarShoreFormat");

    /// <summary>Moonfall companions at 640: the moogle, met but not reached; {0} the power (short); FS is The Far Shore.</summary>
    public static string MoonfallPowerFarShoreShortFormat => Loc.Get("MoonfallPowerFarShoreShortFormat");

    /// <summary>Moonfall companions at 640: Moon-Viewing Draw, short.</summary>
    public static string MoonfallPowerShortDraw => Loc.Get("MoonfallPowerShortDraw");

    /// <summary>Moonfall companions at 640: Lunar Burst, short.</summary>
    public static string MoonfallPowerShortBurst => Loc.Get("MoonfallPowerShortBurst");

    /// <summary>Moonfall companions at 640: Brass Wings, short.</summary>
    public static string MoonfallPowerShortWings => Loc.Get("MoonfallPowerShortWings");

    /// <summary>Moonfall companions: the detail of a companion not yet met.</summary>
    public static string MoonfallNotMetRole => Loc.Get("MoonfallNotMetRole");

    /// <summary>Moonfall companions: {0} the stage number, {1} its name.</summary>
    public static string MoonfallJoinsFormat => Loc.Get("MoonfallJoinsFormat");

    /// <summary>Moonfall companions: the moogle; {0} the stage number, {1} its name.</summary>
    public static string MoonfallJoinsFarShoreFormat => Loc.Get("MoonfallJoinsFarShoreFormat");

    /// <summary>Moonfall companions: every companion is shown as met in A Realm Reborn.</summary>
    public static string MoonfallSpoilersLine => Loc.Get("MoonfallSpoilersLine");

    /// <summary>Moonfall companions: the caption under the power&#x27;s picture.</summary>
    public static string MoonfallPowerInPlay => Loc.Get("MoonfallPowerInPlay");

    /// <summary>Moonfall companions: the levels of their stage won.</summary>
    public static string MoonfallWonTogether => Loc.Get("MoonfallWonTogether");

    /// <summary>Moonfall companions: opens Quick Play with them; {0} the companion.</summary>
    public static string MoonfallPlayWithFormat => Loc.Get("MoonfallPlayWithFormat");

    /// <summary>Moonfall companions: the locked Play with button.</summary>
    public static string MoonfallPlayWithNotMet => Loc.Get("MoonfallPlayWithNotMet");

    /// <summary>Moonfall companions: the locked Play with button; {0} their stage.</summary>
    public static string MoonfallPlayWithStageFormat => Loc.Get("MoonfallPlayWithStageFormat");

    /// <summary>Moonfall companions: the moogle&#x27;s locked Play with button.</summary>
    public static string MoonfallPlayWithFarShore => Loc.Get("MoonfallPlayWithFarShore");

    /// <summary>Moonfall pause menu: its title.</summary>
    public static string MoonfallPausedTitle => Loc.Get("MoonfallPausedTitle");

    /// <summary>Moonfall pause menu: {0} the level&#x27;s code, {1} its name, {2} balls left, {3} oranges left.</summary>
    public static string MoonfallPauseLineFormat => Loc.Get("MoonfallPauseLineFormat");

    /// <summary>Moonfall pause menu: what holds the pause; {0} the reason (Paused · in combat).</summary>
    public static string MoonfallPauseHeldFormat => Loc.Get("MoonfallPauseHeldFormat");

    /// <summary>Moonfall pause menu: restart (held to confirm).</summary>
    public static string MoonfallRestartLevel => Loc.Get("MoonfallRestartLevel");

    /// <summary>Moonfall pause menu: under Restart.</summary>
    public static string MoonfallHoldToRestart => Loc.Get("MoonfallHoldToRestart");

    /// <summary>Moonfall pause menu at 640: under Restart.</summary>
    public static string MoonfallHoldToRestartShort => Loc.Get("MoonfallHoldToRestartShort");

    /// <summary>Moonfall pause menu: leave the level (held to confirm), Adventure.</summary>
    public static string MoonfallLeaveToMap => Loc.Get("MoonfallLeaveToMap");

    /// <summary>Moonfall pause menu: leave the level (held to confirm), Quick Play.</summary>
    public static string MoonfallLeaveToQuickPlay => Loc.Get("MoonfallLeaveToQuickPlay");

    /// <summary>Moonfall pause menu: leave the challenge (held to confirm).</summary>
    public static string MoonfallLeaveChallenge => Loc.Get("MoonfallLeaveChallenge");

    /// <summary>Moonfall pause menu: leave the duel (held to confirm).</summary>
    public static string MoonfallLeaveDuel => Loc.Get("MoonfallLeaveDuel");

    /// <summary>Moonfall pause menu: under Leave.</summary>
    public static string MoonfallHoldToLeave => Loc.Get("MoonfallHoldToLeave");

    /// <summary>Moonfall pause menu at 640: under Leave.</summary>
    public static string MoonfallHoldToLeaveShort => Loc.Get("MoonfallHoldToLeaveShort");

    /// <summary>Moonfall pause menu: its note.</summary>
    public static string MoonfallPausesItself => Loc.Get("MoonfallPausesItself");

    /// <summary>Moonfall board: the crest&#x27;s moonstone pauses.</summary>
    public static string MoonfallPauseTooltip => Loc.Get("MoonfallPauseTooltip");

    /// <summary>Moonfall quick setting: Tsukimichi&#x27;s Reduce motion.</summary>
    public static string MoonfallReduceMotion => Loc.Get("MoonfallReduceMotion");

    /// <summary>Moonfall options: under Reduce motion.</summary>
    public static string MoonfallReduceMotionNote => Loc.Get("MoonfallReduceMotionNote");

    /// <summary>Moonfall quick setting: Moonfall&#x27;s decoration level.</summary>
    public static string MoonfallDecoration => Loc.Get("MoonfallDecoration");

    /// <summary>Moonfall decoration: everything (moondust, fireflies, stars, mist, glints).</summary>
    public static string MoonfallDecorationFull => Loc.Get("MoonfallDecorationFull");

    /// <summary>Moonfall decoration: the halos, the beams and the lantern only.</summary>
    public static string MoonfallDecorationSimple => Loc.Get("MoonfallDecorationSimple");

    /// <summary>Moonfall decoration: still, flat.</summary>
    public static string MoonfallDecorationOff => Loc.Get("MoonfallDecorationOff");

    /// <summary>Moonfall options: under Decoration.</summary>
    public static string MoonfallDecorationNote => Loc.Get("MoonfallDecorationNote");

    /// <summary>Moonfall quick setting: no audio output.</summary>
    public static string MoonfallSoundNone => Loc.Get("MoonfallSoundNone");

    /// <summary>Moonfall options: under Sound.</summary>
    public static string MoonfallSoundNote => Loc.Get("MoonfallSoundNote");

    /// <summary>Moonfall switch: on.</summary>
    public static string MoonfallOn => Loc.Get("MoonfallOn");

    /// <summary>Moonfall switch: off.</summary>
    public static string MoonfallOff => Loc.Get("MoonfallOff");

    /// <summary>Moonfall options: the line under the title.</summary>
    public static string MoonfallOptionsLine => Loc.Get("MoonfallOptionsLine");

    /// <summary>Moonfall title at 640: the Peg marks hint.</summary>
    public static string MoonfallPegMarksHintShort => Loc.Get("MoonfallPegMarksHintShort");

    /// <summary>Moonfall tally: play the level again.</summary>
    public static string MoonfallReplay => Loc.Get("MoonfallReplay");

    /// <summary>Moonfall tally: the next level; {0} its code (3-3).</summary>
    public static string MoonfallNextCodeFormat => Loc.Get("MoonfallNextCodeFormat");

    /// <summary>Moonfall tally: the row for the Ace bonus.</summary>
    public static string MoonfallTallyAceBonus => Loc.Get("MoonfallTallyAceBonus");

    /// <summary>Moonfall tally: a duel won, in capitals.</summary>
    public static string MoonfallBannerDuelWon => Loc.Get("MoonfallBannerDuelWon");

    /// <summary>Moonfall tally: a duel lost, in capitals.</summary>
    public static string MoonfallBannerDuelLost => Loc.Get("MoonfallBannerDuelLost");

    /// <summary>Moonfall tally: a duel drawn, in capitals.</summary>
    public static string MoonfallBannerDuelDrawn => Loc.Get("MoonfallBannerDuelDrawn");

    /// <summary>Moonfall tally: a challenge met, in capitals.</summary>
    public static string MoonfallBannerChallengeMet => Loc.Get("MoonfallBannerChallengeMet");

    /// <summary>Moonfall tally: a challenge failed, in capitals.</summary>
    public static string MoonfallBannerChallengeFailed => Loc.Get("MoonfallBannerChallengeFailed");

    /// <summary>Moonfall tally: a duel; {0} the opponent, {1} the difficulty.</summary>
    public static string MoonfallDuelAgainstFormat => Loc.Get("MoonfallDuelAgainstFormat");

    /// <summary>Moonfall tally: the duel again.</summary>
    public static string MoonfallRematch => Loc.Get("MoonfallRematch");

    /// <summary>Moonfall duel: the player&#x27;s side.</summary>
    public static string MoonfallDuelYou => Loc.Get("MoonfallDuelYou");

    /// <summary>Moonfall duel HUD: the player&#x27;s side, in capitals.</summary>
    public static string MoonfallDuelYouCaps => Loc.Get("MoonfallDuelYouCaps");

    /// <summary>Moonfall tally: a score challenge; {0} its name, {1} the run&#x27;s total, {2} its target.</summary>
    public static string MoonfallRunScoreFormat => Loc.Get("MoonfallRunScoreFormat");

    /// <summary>Moonfall tally: a challenge; {0} its name, {1} the level played, {2} levels in the run.</summary>
    public static string MoonfallRunLevelFormat => Loc.Get("MoonfallRunLevelFormat");

    /// <summary>Moonfall tally: the challenge&#x27;s next level; {0} its number, {1} levels in the run.</summary>
    public static string MoonfallRunNextFormat => Loc.Get("MoonfallRunNextFormat");

    /// <summary>Moonfall Quick Play: the line under the title.</summary>
    public static string MoonfallQuickPlayLine => Loc.Get("MoonfallQuickPlayLine");

    /// <summary>Moonfall: the companion picker&#x27;s caption.</summary>
    public static string MoonfallCompanionCaps => Loc.Get("MoonfallCompanionCaps");

    /// <summary>Moonfall companion picker: no companion (inside a ring).</summary>
    public static string MoonfallNoPowerShort => Loc.Get("MoonfallNoPowerShort");

    /// <summary>Moonfall companion picker: the &#x27;none&#x27; ring&#x27;s tooltip.</summary>
    public static string MoonfallNoPowerTip => Loc.Get("MoonfallNoPowerTip");

    /// <summary>Moonfall companion picker: a card back&#x27;s tooltip; {0} the power.</summary>
    public static string MoonfallPickerNotMetFormat => Loc.Get("MoonfallPickerNotMetFormat");

    /// <summary>Moonfall companion picker: a companion not reached; {0} name, {1} stage.</summary>
    public static string MoonfallPickerNotReachedFormat => Loc.Get("MoonfallPickerNotReachedFormat");

    /// <summary>Moonfall Quick Play: {0} campaign, {1} level code, {2} stage name.</summary>
    public static string MoonfallQuickLevelLineFormat => Loc.Get("MoonfallQuickLevelLineFormat");

    /// <summary>Moonfall lists: page {0} of {1}.</summary>
    public static string MoonfallPageFormat => Loc.Get("MoonfallPageFormat");

    /// <summary>Moonfall challenges: the line under the title.</summary>
    public static string MoonfallChallengesLine => Loc.Get("MoonfallChallengesLine");

    /// <summary>Moonfall challenges: a challenge whose levels are not built yet.</summary>
    public static string MoonfallChallengeUnavailable => Loc.Get("MoonfallChallengeUnavailable");

    /// <summary>Moonfall challenges: the detail of a challenge whose levels are not built yet.</summary>
    public static string MoonfallChallengeUnavailableLine => Loc.Get("MoonfallChallengeUnavailableLine");

    /// <summary>Moonfall challenges: {0} what it asks, {1} balls, {2} oranges.</summary>
    public static string MoonfallChallengeRulesFormat => Loc.Get("MoonfallChallengeRulesFormat");

    /// <summary>Moonfall challenges: {0} the levels&#x27; codes.</summary>
    public static string MoonfallChallengeLevelsFormat => Loc.Get("MoonfallChallengeLevelsFormat");

    /// <summary>Moonfall challenges: a challenge met; {0} its best total.</summary>
    public static string MoonfallChallengeDoneFormat => Loc.Get("MoonfallChallengeDoneFormat");

    /// <summary>Moonfall challenges: a challenge never played.</summary>
    public static string MoonfallChallengeNotTried => Loc.Get("MoonfallChallengeNotTried");

    /// <summary>Moonfall challenges: a score challenge.</summary>
    public static string MoonfallChallengeKindScore => Loc.Get("MoonfallChallengeKindScore");

    /// <summary>Moonfall challenges: a win challenge.</summary>
    public static string MoonfallChallengeKindWin => Loc.Get("MoonfallChallengeKindWin");

    /// <summary>Moonfall challenges: a clear-all challenge.</summary>
    public static string MoonfallChallengeKindClear => Loc.Get("MoonfallChallengeKindClear");

    /// <summary>Moonfall challenges: a duel challenge.</summary>
    public static string MoonfallChallengeKindDuel => Loc.Get("MoonfallChallengeKindDuel");

    /// <summary>Moonfall challenges: the locked Play button.</summary>
    public static string MoonfallSealed => Loc.Get("MoonfallSealed");

    /// <summary>Moonfall duel: the line under the title.</summary>
    public static string MoonfallDuelLine => Loc.Get("MoonfallDuelLine");

    /// <summary>Moonfall duel: why no duel can be set up.</summary>
    public static string MoonfallDuelNone => Loc.Get("MoonfallDuelNone");

    /// <summary>Moonfall duel: the opponent picker&#x27;s caption.</summary>
    public static string MoonfallOpponentCaps => Loc.Get("MoonfallOpponentCaps");

    /// <summary>Moonfall duel: the difficulty&#x27;s caption.</summary>
    public static string MoonfallDifficultyCaps => Loc.Get("MoonfallDifficultyCaps");

    /// <summary>Moonfall duel: the level&#x27;s caption.</summary>
    public static string MoonfallLevelCaps => Loc.Get("MoonfallLevelCaps");

    /// <summary>Moonfall duel: the player&#x27;s companion&#x27;s caption.</summary>
    public static string MoonfallYourCompanionCaps => Loc.Get("MoonfallYourCompanionCaps");

    /// <summary>Moonfall duel: the record at this difficulty; {0} difficulty, {1} wins, {2} losses, {3} draws.</summary>
    public static string MoonfallDuelRecordFormat => Loc.Get("MoonfallDuelRecordFormat");

    /// <summary>Moonfall duel: the start button; {0} the opponent.</summary>
    public static string MoonfallDuelPlayFormat => Loc.Get("MoonfallDuelPlayFormat");

    /// <summary>Moonfall duel HUD: the caption while it is the player&#x27;s turn.</summary>
    public static string MoonfallDuelYourShot => Loc.Get("MoonfallDuelYourShot");

    /// <summary>Moonfall duel HUD: the caption while the opponent weighs its shot; {0} the opponent.</summary>
    public static string MoonfallDuelThinkingFormat => Loc.Get("MoonfallDuelThinkingFormat");

    /// <summary>Moonfall duel HUD: the caption while the opponent&#x27;s ball flies; {0} the opponent.</summary>
    public static string MoonfallDuelTheirShotFormat => Loc.Get("MoonfallDuelTheirShotFormat");

    /// <summary>Moonfall map: the Play button of a stage whose levels are not built yet.</summary>
    public static string MoonfallLevelsComing => Loc.Get("MoonfallLevelsComing");

    /// <summary>Moonfall pause menu at 640: {0} balls left, {1} oranges left.</summary>
    public static string MoonfallPauseShortFormat => Loc.Get("MoonfallPauseShortFormat");

    /// <summary>Moonfall HUD: the caption over the level&#x27;s Ace score on the right rail.</summary>
    public static string MoonfallAceShort => Loc.Get("MoonfallAceShort");

    /// <summary>Moonfall companions at 640: Sage&#x27;s Path, short.</summary>
    public static string MoonfallPowerShortPath => Loc.Get("MoonfallPowerShortPath");

    /// <summary>Moonfall companions at 640: Storm Post, short.</summary>
    public static string MoonfallPowerShortBolt => Loc.Get("MoonfallPowerShortBolt");

    /// <summary>Moonfall options: under the Decoration setting while Reduce motion is on (it overrides Decoration&#x27;s motion).</summary>
    public static string MoonfallDecorationHeldNote => Loc.Get("MoonfallDecorationHeldNote");

    /// <summary>Moonfall map at the small size: the stage panel&#x27;s button that opens the stage&#x27;s five levels.</summary>
    public static string MoonfallLevelsButton => Loc.Get("MoonfallLevelsButton");

    /// <summary>Moonfall level select: the selected level&#x27;s place. {0} is its code (3-2), {1} the stage&#x27;s name.</summary>
    public static string MoonfallStripLevelFormat => Loc.Get("MoonfallStripLevelFormat");

    /// <summary>Moonfall: what a level of a stage set past the player&#x27;s story is called in its place (the spoiler shield hides the place it depicts).</summary>
    public static string MoonfallVeiledLevel => Loc.Get("MoonfallVeiledLevel");

    /// <summary>Moonfall map: a stop&#x27;s state in its hover line, for a stage set past the player&#x27;s story (the spoiler shield).</summary>
    public static string MoonfallStageVeiledState => Loc.Get("MoonfallStageVeiledState");

    /// <summary>Moonfall map: why a stage set past the player&#x27;s story is closed, and how it opens (the spoiler shield&#x27;s reveal).</summary>
    public static string MoonfallStageVeiledLine => Loc.Get("MoonfallStageVeiledLine");

    /// <summary>Moonfall map and challenges: the closed Play button of a stage or challenge set past the player&#x27;s story.</summary>
    public static string MoonfallStageVeiledButton => Loc.Get("MoonfallStageVeiledButton");

    /// <summary>Moonfall map legend: the spoiler shield&#x27;s mark on a stop set past the player&#x27;s story.</summary>
    public static string MoonfallLegendStory => Loc.Get("MoonfallLegendStory");

    /// <summary>Moonfall challenges: a row&#x27;s line when the challenge runs through a stage set past the player&#x27;s story.</summary>
    public static string MoonfallChallengeVeiled => Loc.Get("MoonfallChallengeVeiled");

    /// <summary>Moonfall title: why Duel is locked.</summary>
    public static string MoonfallDuelLockedTooltip => Loc.Get("MoonfallDuelLockedTooltip");

    /// <summary>Moonfall title: the locked Duel pill&#x27;s second line.</summary>
    public static string MoonfallTitleDuelNoLevels => Loc.Get("MoonfallTitleDuelNoLevels");

    /// <summary>Moonfall duel HUD: the caption while the twins (Alphinaud and Alisaie) weigh their shot.</summary>
    public static string MoonfallDuelTwinsThinking => Loc.Get("MoonfallDuelTwinsThinking");

    /// <summary>Moonfall duel HUD: the caption while the twins&#x27; ball is in play.</summary>
    public static string MoonfallDuelTwinsShot => Loc.Get("MoonfallDuelTwinsShot");

    /// <summary>Moonfall map at the small size: the stage panel&#x27;s line for a stage set past the player&#x27;s story (the spoiler shield).</summary>
    public static string MoonfallStageVeiledShort => Loc.Get("MoonfallStageVeiledShort");

    /// <summary>A duel opponent's difficulty.</summary>
    public static string MoonfallDifficultyName(MoonfallAiDifficulty difficulty) => difficulty switch
    {
        MoonfallAiDifficulty.Novice => Loc.Get("MoonfallDifficulty.Novice"),
        MoonfallAiDifficulty.Master => Loc.Get("MoonfallDifficulty.Master"),
        _ => Loc.Get("MoonfallDifficulty.Adept"),
    };
}
