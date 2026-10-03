using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>Strings for Settings › Themes › Mix moons by state (1.17, plan v7 T10; docs/design/v7/ui/spec-1.17.md §A).</summary>
public static partial class Strings
{
    public static string ThemesHeadingMix => Loc.Get("ThemesHeadingMix");

    public static string ThemesMix => Loc.Get("ThemesMix");

    public static string ThemesMixHint => Loc.Get("ThemesMixHint");

    public static string ThemesMixFromTheme => Loc.Get("ThemesMixFromTheme");

    public static string ThemesMixHighContrast => Loc.Get("ThemesMixHighContrast");

    public static string ThemesMixClassic => Loc.Get("ThemesMixClassic");

    /// <summary>A row's note, and a list option's: {0} = the other state.</summary>
    public static string ThemesMixNoteCloseFormat => Loc.Get("ThemesMixNoteCloseFormat");

    /// <summary>A row's note, and a list option's: {0} = the other state.</summary>
    public static string ThemesMixNoteHardFormat => Loc.Get("ThemesMixNoteHardFormat");

    /// <summary>Ready's row note: {0} = the loudest other state.</summary>
    public static string ThemesMixNoteQuieterLargeFormat => Loc.Get("ThemesMixNoteQuieterLargeFormat");

    /// <summary>Ready's row note: {0} = the loudest other state.</summary>
    public static string ThemesMixNoteQuieterRowFormat => Loc.Get("ThemesMixNoteQuieterRowFormat");

    public static string ThemesMixNoteLoudLarge => Loc.Get("ThemesMixNoteLoudLarge");

    public static string ThemesMixNoteLoudRow => Loc.Get("ThemesMixNoteLoudRow");

    public static string ThemesMixOptionLeadLarge => Loc.Get("ThemesMixOptionLeadLarge");

    public static string ThemesMixOptionLeadRow => Loc.Get("ThemesMixOptionLeadRow");

    public static string ThemesMixOptionLoudLarge => Loc.Get("ThemesMixOptionLoudLarge");

    public static string ThemesMixOptionLoudRow => Loc.Get("ThemesMixOptionLoudRow");

    /// <summary>The mix's status line: {0} = Ready's lead (1.35×).</summary>
    public static string ThemesMixOkFormat => Loc.Get("ThemesMixOkFormat");

    /// <summary>The mix's status line with nothing mixed: {0} = the set.</summary>
    public static string ThemesMixOneSetFormat => Loc.Get("ThemesMixOneSetFormat");

    /// <summary>The mix's status line: {0} = a set without numbers.</summary>
    public static string ThemesMixUnmeasuredFormat => Loc.Get("ThemesMixUnmeasuredFormat");

    /// <summary>The mix's warning: {0} and {2} = states, {1} and {3} = their sets.</summary>
    public static string ThemesMixHardFormat => Loc.Get("ThemesMixHardFormat");

    /// <summary>The mix's warning: {0} and {2} = states, {1} and {3} = their sets.</summary>
    public static string ThemesMixCloseFormat => Loc.Get("ThemesMixCloseFormat");

    public static string ThemesMixLeadLarge => Loc.Get("ThemesMixLeadLarge");

    public static string ThemesMixLeadRow => Loc.Get("ThemesMixLeadRow");

    public static string ThemesMixCompletedLarge => Loc.Get("ThemesMixCompletedLarge");

    public static string ThemesMixCompletedRow => Loc.Get("ThemesMixCompletedRow");

    /// <summary>{0} = the first warning; {1} = how many more.</summary>
    public static string ThemesMixMoreFormat => Loc.Get("ThemesMixMoreFormat");

    public static string ThemesMixDetails => Loc.Get("ThemesMixDetails");

    public static string ThemesMixFix => Loc.Get("ThemesMixFix");

    public static string ThemesMixReset => Loc.Get("ThemesMixReset");

    /// <summary>Reset mix's tooltip: {0} = the mix's share code.</summary>
    public static string ThemesMixResetTooltipFormat => Loc.Get("ThemesMixResetTooltipFormat");

    public static string ThemesMixResetNothing => Loc.Get("ThemesMixResetNothing");

    public static string ThemesMixModeGrey => Loc.Get("ThemesMixModeGrey");

    public static string ThemesMixModeDeut => Loc.Get("ThemesMixModeDeut");

    public static string ThemesMixModeMachadoDeut => Loc.Get("ThemesMixModeMachadoDeut");

    public static string ThemesMixModeMachadoProt => Loc.Get("ThemesMixModeMachadoProt");

    public static string ThemesMixModeMachadoTrit => Loc.Get("ThemesMixModeMachadoTrit");

    /// <summary>Details: {0}, {2} = states; {1}, {3} = sets; {4} = the value; {5} = the vision mode; {6} = the bar.</summary>
    public static string ThemesMixDetailPairFormat => Loc.Get("ThemesMixDetailPairFormat");

    public static string ThemesMixTierLarge => Loc.Get("ThemesMixTierLarge");

    public static string ThemesMixTierRow => Loc.Get("ThemesMixTierRow");

    /// <summary>Details: {0} = At 48 px and up / In rows; {1} = the set; {2} = the lead (1.23×).</summary>
    public static string ThemesMixDetailLeadFormat => Loc.Get("ThemesMixDetailLeadFormat");

    /// <summary>Details: {0} = At 48 px and up / In rows; {1} = the set; {2} = its share (0.82×).</summary>
    public static string ThemesMixDetailCompletedFormat => Loc.Get("ThemesMixDetailCompletedFormat");

    /// <summary>Details: {0} = At 48 px and up / In rows; {1} = the lead.</summary>
    public static string ThemesMixDetailStillLeadsFormat => Loc.Get("ThemesMixDetailStillLeadsFormat");

    /// <summary>A ratio such as Ready's lead: {0} = the number.</summary>
    public static string ThemesMixRatioFormat => Loc.Get("ThemesMixRatioFormat");

    public static string ThemesMixFixTitle => Loc.Get("ThemesMixFixTitle");

    /// <summary>Fix it's proposal: {0} = a set; {1} = a state.</summary>
    public static string ThemesMixFixUseFormat => Loc.Get("ThemesMixFixUseFormat");

    /// <summary>Fix it's proposal when the set already draws another state: {0} = a set; {1} = a state.</summary>
    public static string ThemesMixFixUseTooFormat => Loc.Get("ThemesMixFixUseTooFormat");

    /// <summary>Fix it's fallback: {0} = a state.</summary>
    public static string ThemesMixFixThemeOwnFormat => Loc.Get("ThemesMixFixThemeOwnFormat");

    /// <summary>Fix it's proposal: {0}, {1} = Ready's lead after the change.</summary>
    public static string ThemesMixFixLeadFormat => Loc.Get("ThemesMixFixLeadFormat");

    public static string ThemesMixFixApart => Loc.Get("ThemesMixFixApart");

    public static string ThemesMixFixPartial => Loc.Get("ThemesMixFixPartial");

    /// <summary>Fix it's proposal: {0} = the state the player just picked.</summary>
    public static string ThemesMixFixKeepFormat => Loc.Get("ThemesMixFixKeepFormat");

    public static string ThemesMixFixNotNow => Loc.Get("ThemesMixFixNotNow");

    public static string ThemesMixFixUse => Loc.Get("ThemesMixFixUse");

    /// <summary>Fix it's tooltip when it has nothing to propose: {0} = the state the player just picked.</summary>
    public static string ThemesMixFixNoneKeepFormat => Loc.Get("ThemesMixFixNoneKeepFormat");

    public static string ThemesMixFixNone => Loc.Get("ThemesMixFixNone");

    /// <summary>Undo toast after a state's pick: {0} = the state; {1} = the set, or From theme.</summary>
    public static string UndoToastMixPickFormat => Loc.Get("UndoToastMixPickFormat");

    public static string UndoToastMixReset => Loc.Get("UndoToastMixReset");
}
