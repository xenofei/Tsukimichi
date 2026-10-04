using Tsukimichi.Core.Query;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for 1.21.0's storylines (feature plan v7 P5, N8, N10): the Side stories card and its new-chapter line, Loose
/// ends (card, Tonight line, overlay section, chat line, settings) and Who's in it (the Cast line, the hover line, the
/// quick view). Every constant is prefixed <c>Stories</c>, <c>LooseEnds</c>, <c>NewChapters</c>, <c>Cast</c> or
/// <c>PresetWithStoryCharacters</c> so the partial halves never collide.
/// </summary>
static partial class Strings
{
    // ---- Side stories (P5) ----
    public static string StoriesSection => Loc.Get("StoriesSection");
    public static string StoriesNone => Loc.Get("StoriesNone");
    public static string StoriesToGoOne => Loc.Get("StoriesToGoOne");
    public static string StoriesToGoFormat => Loc.Get("StoriesToGoFormat");
    public static string StoriesDoneOne => Loc.Get("StoriesDoneOne");
    public static string StoriesDoneFormat => Loc.Get("StoriesDoneFormat");
    public static string StoriesDoneHide => Loc.Get("StoriesDoneHide");
    public static string StoriesFinished => Loc.Get("StoriesFinished");
    public static string StoriesCaughtUp => Loc.Get("StoriesCaughtUp");
    public static string StoriesContinues => Loc.Get("StoriesContinues");
    public static string StoriesCaughtUpLine => Loc.Get("StoriesCaughtUpLine");
    public static string StoriesAhead => Loc.Get("StoriesAhead");
    public static string StoriesAheadExpansion => Loc.Get("StoriesAheadExpansion");
    public static string StoriesAheadStory => Loc.Get("StoriesAheadStory");
    public static string StoriesLeftNextFormat => Loc.Get("StoriesLeftNextFormat");
    public static string StoriesSideAheadFormat => Loc.Get("StoriesSideAheadFormat");
    public static string StoriesTeleport => Loc.Get("StoriesTeleport");
    public static string StoriesShowInJournal => Loc.Get("StoriesShowInJournal");
    public static string StoriesFlagGiver => Loc.Get("StoriesFlagGiver");

    // ---- New chapters in the What's new card (P5) ----
    public static string NewChaptersLabel => Loc.Get("NewChaptersLabel");
    public static string NewChaptersQuestOne => Loc.Get("NewChaptersQuestOne");
    public static string NewChaptersQuestsFormat => Loc.Get("NewChaptersQuestsFormat");
    public static string NewChaptersNameHidden => Loc.Get("NewChaptersNameHidden");
    public static string NewChaptersShow => Loc.Get("NewChaptersShow");
    public static string NewChaptersShowTooltip => Loc.Get("NewChaptersShowTooltip");
    public static string NewChaptersCloseTooltip => Loc.Get("NewChaptersCloseTooltip");

    // ---- Loose ends (N8) ----
    public static string LooseEndsSection => Loc.Get("LooseEndsSection");
    public static string LooseEndsTooltip => Loc.Get("LooseEndsTooltip");
    public static string LooseEndsNone => Loc.Get("LooseEndsNone");
    public static string LooseEndsCaptionFormat => Loc.Get("LooseEndsCaptionFormat");
    public static string LooseEndsFinale => Loc.Get("LooseEndsFinale");
    public static string LooseEndsFinaleTooltip => Loc.Get("LooseEndsFinaleTooltip");
    public static string LooseEndsNotForMeTooltip => Loc.Get("LooseEndsNotForMeTooltip");
    public static string LooseEndsLeftFormat => Loc.Get("LooseEndsLeftFormat");
    public static string LooseEndsLeftLevelFormat => Loc.Get("LooseEndsLeftLevelFormat");
    public static string LooseEndsJobAheadFormat => Loc.Get("LooseEndsJobAheadFormat");
    public static string LooseEndsFinaleConfig => Loc.Get("LooseEndsFinaleConfig");
    public static string LooseEndsFinaleConfigHint => Loc.Get("LooseEndsFinaleConfigHint");
    public static string LooseEndsChatFormat => Loc.Get("LooseEndsChatFormat");
    public static string LooseEndsTonightLabel => Loc.Get("LooseEndsTonightLabel");
    public static string TodoConfigShowLooseEnds => Loc.Get("TodoConfigShowLooseEnds");
    public static string TodoConfigShowLooseEndsHint => Loc.Get("TodoConfigShowLooseEndsHint");

    // ---- Who's in it (N10) ----
    public static string CastWith => Loc.Get("CastWith");
    public static string CastAnd => Loc.Get("CastAnd");
    public static string CastComma => Loc.Get("CastComma");
    public static string CastFamiliarFace => Loc.Get("CastFamiliarFace");
    public static string CastFamiliarFaces => Loc.Get("CastFamiliarFaces");
    public static string CastTooltip => Loc.Get("CastTooltip");
    public static string PresetWithStoryCharacters => FilterNames.Display(FilterNames.WithStoryCharacters);
    public static string PresetWithStoryCharactersCaption => Loc.Get("PresetWithStoryCharactersCaption");
    public static string PresetWithStoryCharactersTooltip => Loc.Get("PresetWithStoryCharactersTooltip");
}
