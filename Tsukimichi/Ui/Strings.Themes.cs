using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>Strings for Settings › Themes (1.16, plan v7 T9; docs/design/v7/ui/spec-1.16.md §B) and its Undo toasts.</summary>
public static partial class Strings
{
    public static string SettingsPageThemes => Loc.Get("SettingsPageThemes");

    public static string SettingsIntroThemes => Loc.Get("SettingsIntroThemes");

    public static string ThemesHeadingTheme => Loc.Get("ThemesHeadingTheme");

    public static string ThemesHeadingPreview => Loc.Get("ThemesHeadingPreview");

    public static string ThemesHeadingColours => Loc.Get("ThemesHeadingColours");

    public static string ThemesCards => Loc.Get("ThemesCards");

    public static string ThemesCardsHint => Loc.Get("ThemesCardsHint");

    public static string ThemeNameMedallion => Loc.Get("ThemeNameMedallion");

    public static string ThemeNameClassic => Loc.Get("ThemeNameClassic");

    public static string ThemeNameAetherCrystal => Loc.Get("ThemeNameAetherCrystal");

    public static string ThemeNameIshgardGlass => Loc.Get("ThemeNameIshgardGlass");

    public static string ThemeNameOrrery => Loc.Get("ThemeNameOrrery");

    public static string ThemeNameSumi => Loc.Get("ThemeNameSumi");

    public static string ThemesLegacyTag => Loc.Get("ThemesLegacyTag");

    /// <summary>{0} = the frame kit (Brass, Silver, Came), {1} = the palette (Night, Ishgard Snow).</summary>
    public static string ThemesCardSubtitleFormat => Loc.Get("ThemesCardSubtitleFormat");

    /// <summary>{0} = the palette.</summary>
    public static string ThemesCardSubtitleClassicFormat => Loc.Get("ThemesCardSubtitleClassicFormat");

    public static string FrameKitBrass => Loc.Get("FrameKitBrass");

    public static string FrameKitSilver => Loc.Get("FrameKitSilver");

    public static string FrameKitCame => Loc.Get("FrameKitCame");

    public static string FrameKitAstrolabe => Loc.Get("FrameKitAstrolabe");

    public static string FrameKitKirikane => Loc.Get("FrameKitKirikane");

    public static string PaletteNameNight => Loc.Get("PaletteNameNight");

    public static string PaletteNameDawn => Loc.Get("PaletteNameDawn");

    public static string PaletteNameIshgardSnow => Loc.Get("PaletteNameIshgardSnow");

    public static string PaletteNameKuganeLacquer => Loc.Get("PaletteNameKuganeLacquer");

    public static string PaletteNameFollowDalamud => Loc.Get("PaletteNameFollowDalamud");

    public static string ThemesInUse => Loc.Get("ThemesInUse");

    public static string ThemesSampleQuest => Loc.Get("ThemesSampleQuest");

    public static string ThemesHighContrastNote => Loc.Get("ThemesHighContrastNote");

    /// <summary>{0} = the hovered theme.</summary>
    public static string ThemesPreviewingFormat => Loc.Get("ThemesPreviewingFormat");

    /// <summary>{0} = the theme in use.</summary>
    public static string ThemesPreviewInUseFormat => Loc.Get("ThemesPreviewInUseFormat");

    /// <summary>{0} = the theme in use.</summary>
    public static string ThemesPreviewFormat => Loc.Get("ThemesPreviewFormat");

    /// <summary>{0} = the theme.</summary>
    public static string ThemesCustomisedFormat => Loc.Get("ThemesCustomisedFormat");

    public static string ThemesSampleReady => Loc.Get("ThemesSampleReady");

    public static string ThemesSampleReadyDetail => Loc.Get("ThemesSampleReadyDetail");

    public static string ThemesSampleJournal => Loc.Get("ThemesSampleJournal");

    public static string ThemesSampleJournalDetail => Loc.Get("ThemesSampleJournalDetail");

    public static string ThemesSampleCompleted => Loc.Get("ThemesSampleCompleted");

    public static string ThemesSampleCompletedDetail => Loc.Get("ThemesSampleCompletedDetail");

    public static string ThemesSampleBlocked => Loc.Get("ThemesSampleBlocked");

    public static string ThemesSampleBlockedDetail => Loc.Get("ThemesSampleBlockedDetail");

    public static string ThemesSampleLocked => Loc.Get("ThemesSampleLocked");

    public static string ThemesSampleLockedDetail => Loc.Get("ThemesSampleLockedDetail");

    public static string ThemesSampleUnknown => Loc.Get("ThemesSampleUnknown");

    public static string ThemesSampleUnknownDetail => Loc.Get("ThemesSampleUnknownDetail");

    public static string ThemesSampleHeroLine => Loc.Get("ThemesSampleHeroLine");

    public static string ThemesPalette => Loc.Get("ThemesPalette");

    public static string ThemesPaletteHint => Loc.Get("ThemesPaletteHint");

    public static string ThemesHighContrast => Loc.Get("ThemesHighContrast");

    public static string ThemesHighContrastHint => Loc.Get("ThemesHighContrastHint");

    public static string ThemesFrames => Loc.Get("ThemesFrames");

    public static string ThemesFramesHint => Loc.Get("ThemesFramesHint");

    public static string ThemesFramesFixedReason => Loc.Get("ThemesFramesFixedReason");

    public static string ThemesFramesFromTheme => Loc.Get("ThemesFramesFromTheme");

    public static string ThemesFramesHardFormat => Loc.Get("ThemesFramesHardFormat");

    public static string ThemesFramesHardColourFormat => Loc.Get("ThemesFramesHardColourFormat");

    public static string ThemesFramesCloseFormat => Loc.Get("ThemesFramesCloseFormat");

    public static string ThemesFramesCloseColourFormat => Loc.Get("ThemesFramesCloseColourFormat");

    public static string ThemesFramesReadyFormat => Loc.Get("ThemesFramesReadyFormat");

    public static string ThemesFramesCompletedFormat => Loc.Get("ThemesFramesCompletedFormat");

    public static string ThemesFramesMoreFormat => Loc.Get("ThemesFramesMoreFormat");

    public static string ThemesReset => Loc.Get("ThemesReset");

    public static string ThemesResetHint => Loc.Get("ThemesResetHint");

    public static string ThemesResetDefaultReason => Loc.Get("ThemesResetDefaultReason");

    public static string ThemesResetButton => Loc.Get("ThemesResetButton");

    /// <summary>{0} = the theme.</summary>
    public static string UndoToastThemeFormat => Loc.Get("UndoToastThemeFormat");

    /// <summary>{0} = the palette.</summary>
    public static string UndoToastPaletteFormat => Loc.Get("UndoToastPaletteFormat");

    /// <summary>{0} = the kit, or From theme.</summary>
    public static string UndoToastFramesFormat => Loc.Get("UndoToastFramesFormat");

    public static string UndoToastHighContrastOn => Loc.Get("UndoToastHighContrastOn");

    public static string UndoToastHighContrastOff => Loc.Get("UndoToastHighContrastOff");

    public static string UndoToastAppearanceReset => Loc.Get("UndoToastAppearanceReset");

    // ---- Share codes (1.17, plan v7 T12; spec-1.17 §C) ----
    public static string ThemesHeadingShare => Loc.Get("ThemesHeadingShare");

    public static string ThemesShareCode => Loc.Get("ThemesShareCode");

    public static string ThemesShareCodeHint => Loc.Get("ThemesShareCodeHint");

    public static string ThemesShareCopy => Loc.Get("ThemesShareCopy");

    public static string ThemesShareCopied => Loc.Get("ThemesShareCopied");

    /// <summary>{0} = the share code; the quiet note after Copy.</summary>
    public static string ThemesShareCopiedFormat => Loc.Get("ThemesShareCopiedFormat");

    public static string ThemesSharePaste => Loc.Get("ThemesSharePaste");

    public static string ThemesSharePasteHint => Loc.Get("ThemesSharePasteHint");

    public static string ThemesSharePastePlaceholder => Loc.Get("ThemesSharePastePlaceholder");

    public static string ThemesShareWouldChange => Loc.Get("ThemesShareWouldChange");

    public static string ThemesShareSameLook => Loc.Get("ThemesShareSameLook");

    /// <summary>{0} = Theme, Palette or Frames; {1} = what it is now; {2} = what the code makes it.</summary>
    public static string ThemesShareChangeFormat => Loc.Get("ThemesShareChangeFormat");

    public static string ThemesShareTheme => Loc.Get("ThemesShareTheme");

    public static string ThemesSharePalette => Loc.Get("ThemesSharePalette");

    public static string ThemesShareFrames => Loc.Get("ThemesShareFrames");

    /// <summary>{0} = a state (Ready); {1} = the set its moon comes from.</summary>
    public static string ThemesSharePickFormat => Loc.Get("ThemesSharePickFormat");

    /// <summary>{0} = a state (Ready) whose moon follows the theme again.</summary>
    public static string ThemesSharePickFromThemeFormat => Loc.Get("ThemesSharePickFromThemeFormat");

    /// <summary>{0} = what was left out (Ready: a set this version doesn't have).</summary>
    public static string ThemesShareLeftOutOneFormat => Loc.Get("ThemesShareLeftOutOneFormat");

    /// <summary>{0} = how many; {1} = what was left out, separated by semicolons.</summary>
    public static string ThemesShareLeftOutManyFormat => Loc.Get("ThemesShareLeftOutManyFormat");

    /// <summary>{0} = Theme, Palette, Frames or a state; {1} = what the code names.</summary>
    public static string ThemesShareLeftOutItemFormat => Loc.Get("ThemesShareLeftOutItemFormat");

    public static string ThemesShareListSeparator => Loc.Get("ThemesShareListSeparator");

    public static string ThemesShareUnknownTheme => Loc.Get("ThemesShareUnknownTheme");

    public static string ThemesShareUnknownPalette => Loc.Get("ThemesShareUnknownPalette");

    public static string ThemesShareUnknownFrames => Loc.Get("ThemesShareUnknownFrames");

    public static string ThemesShareUnknownSet => Loc.Get("ThemesShareUnknownSet");

    /// <summary>{0} = a theme, palette, frames or set this version lists but does not offer yet.</summary>
    public static string ThemesShareNotOfferedFormat => Loc.Get("ThemesShareNotOfferedFormat");

    /// <summary>{0} = a set that cannot be mixed state by state (Classic).</summary>
    public static string ThemesShareWholeThemeFormat => Loc.Get("ThemesShareWholeThemeFormat");

    public static string ThemesShareCancel => Loc.Get("ThemesShareCancel");

    public static string ThemesShareApply => Loc.Get("ThemesShareApply");

    public static string ThemesShareApplyRest => Loc.Get("ThemesShareApplyRest");

    public static string ThemesShareMistyped => Loc.Get("ThemesShareMistyped");

    public static string ThemesShareNewer => Loc.Get("ThemesShareNewer");

    public static string UndoToastLookApplied => Loc.Get("UndoToastLookApplied");
}
