using Tsukimichi.Core.Companions;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Strings for the automation level and About automation (plan v7, 1.18.0, A10; spec-1.18 §A10): the four level cards,
/// the fine-tuning toggles, the About card, and the links to it from Setup, Help and the first-start confirmation.
/// </summary>
public static partial class Strings
{
    public static string AutomationButtonsHeading => Loc.Get("AutomationButtonsHeading");

    public static string AutomationButtonsHint => Loc.Get("AutomationButtonsHint");

    public static string AutomationLevelTrackerOnly => Loc.Get("AutomationLevelTrackerOnly");

    public static string AutomationLevelTravel => Loc.Get("AutomationLevelTravel");

    public static string AutomationLevelTravelAndWalking => Loc.Get("AutomationLevelTravelAndWalking");

    public static string AutomationLevelFullHandOffs => Loc.Get("AutomationLevelFullHandOffs");

    public static string AutomationLevelTrackerOnlyBody => Loc.Get("AutomationLevelTrackerOnlyBody");

    public static string AutomationLevelTravelBody => Loc.Get("AutomationLevelTravelBody");

    public static string AutomationLevelTravelAndWalkingBody => Loc.Get("AutomationLevelTravelAndWalkingBody");

    public static string AutomationLevelFullHandOffsBody => Loc.Get("AutomationLevelFullHandOffsBody");

    public static string AutomationLevelCustom => Loc.Get("AutomationLevelCustom");

    /// <summary>The line under the cards while the buttons match no level.</summary>
    public static string AutomationCustomNote => Loc.Get("AutomationCustomNote");

    public static string AutomationPillFlag => Loc.Get("AutomationPillFlag");

    public static string AutomationPillMap => Loc.Get("AutomationPillMap");

    public static string AutomationPillAethernet => Loc.Get("AutomationPillAethernet");

    public static string AutomationFineTune => Loc.Get("AutomationFineTune");

    public static string AutomationButtonTeleport => Loc.Get("AutomationButtonTeleport");

    public static string AutomationButtonTeleportHint => Loc.Get("AutomationButtonTeleportHint");

    public static string AutomationButtonGather => Loc.Get("AutomationButtonGather");

    public static string AutomationButtonGatherHint => Loc.Get("AutomationButtonGatherHint");

    public static string AutomationButtonQuestionable => Loc.Get("AutomationButtonQuestionable");

    public static string AutomationButtonQuestionableHint => Loc.Get("AutomationButtonQuestionableHint");

    public static string AutomationButtonAutoDuty => Loc.Get("AutomationButtonAutoDuty");

    public static string AutomationButtonAutoDutyHint => Loc.Get("AutomationButtonAutoDutyHint");

    public static string AutomationButtonArtisan => Loc.Get("AutomationButtonArtisan");

    public static string AutomationButtonArtisanHint => Loc.Get("AutomationButtonArtisanHint");

    /// <summary>{0} = the level's name (or Custom).</summary>
    public static string UndoToastAutomationFormat => Loc.Get("UndoToastAutomationFormat");

    public static string AboutAutomationTitle => Loc.Get("AboutAutomationTitle");

    public static string AboutAutomationRowHint => Loc.Get("AboutAutomationRowHint");

    public static string AboutAutomationOpen => Loc.Get("AboutAutomationOpen");

    public static string AboutAutomationDoesHeading => Loc.Get("AboutAutomationDoesHeading");

    public static string AboutAutomationDoesBody => Loc.Get("AboutAutomationDoesBody");

    public static string AboutAutomationOthersHeading => Loc.Get("AboutAutomationOthersHeading");

    public static string AboutAutomationOthersBody => Loc.Get("AboutAutomationOthersBody");

    public static string AboutAutomationRulesHeading => Loc.Get("AboutAutomationRulesHeading");

    public static string AboutAutomationRulesBody => Loc.Get("AboutAutomationRulesBody");

    public static string AboutAutomationLineHeading => Loc.Get("AboutAutomationLineHeading");

    public static string AboutAutomationLineBody => Loc.Get("AboutAutomationLineBody");

    public static string AboutAutomationMannersHeading => Loc.Get("AboutAutomationMannersHeading");

    public static string AboutAutomationMannersBody => Loc.Get("AboutAutomationMannersBody");

    public static string AboutAutomationLocalHeading => Loc.Get("AboutAutomationLocalHeading");

    public static string AboutAutomationLocalBody => Loc.Get("AboutAutomationLocalBody");

    public static string AboutAutomationAgreement => Loc.Get("AboutAutomationAgreement");

    /// <summary>The link question's body for the User Agreement (in place of the spoiler question).</summary>
    public static string AboutAutomationAgreementBody => Loc.Get("AboutAutomationAgreementBody");

    public static string AboutAutomationClose => Loc.Get("AboutAutomationClose");

    /// <summary>{0} = the level's name (or Custom).</summary>
    public static string SetupAutomationFormat => Loc.Get("SetupAutomationFormat");

    public static string SetupAutomationButton => Loc.Get("SetupAutomationButton");

    public static string SetupAutomationTooltip => Loc.Get("SetupAutomationTooltip");

    public static string HelpAboutAutomationTip => Loc.Get("HelpAboutAutomationTip");

    /// <summary>{0} = the item crafted.</summary>
    public static string ArtisanCraftingFormat => Loc.Get("ArtisanCraftingFormat");

    /// <summary>A level's name, or Custom for null.</summary>
    public static string AutomationLevelName(AutomationLevel? level) => level switch
    {
        AutomationLevel.TrackerOnly => AutomationLevelTrackerOnly,
        AutomationLevel.Travel => AutomationLevelTravel,
        AutomationLevel.TravelAndWalking => AutomationLevelTravelAndWalking,
        AutomationLevel.FullHandOffs => AutomationLevelFullHandOffs,
        _ => AutomationLevelCustom,
    };

    /// <summary>A level's one sentence.</summary>
    public static string AutomationLevelBody(AutomationLevel level) => level switch
    {
        AutomationLevel.TrackerOnly => AutomationLevelTrackerOnlyBody,
        AutomationLevel.Travel => AutomationLevelTravelBody,
        AutomationLevel.TravelAndWalking => AutomationLevelTravelAndWalkingBody,
        _ => AutomationLevelFullHandOffsBody,
    };
}
