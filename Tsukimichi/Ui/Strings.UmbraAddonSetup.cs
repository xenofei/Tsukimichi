using Tsukimichi.Core.Umbra;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for setting up Tsukimichi for Umbra from Tsukimichi (<see cref="UmbraAddonCard"/> and Settings › About ›
/// Umbra, <c>ConfigWindow.UmbraAddonSetup.cs</c>). English only until localization reopens.
/// </summary>
static partial class Strings
{
    public static string UmbraSetupTitle => Loc.Get("UmbraSetup.Title");

    public static string UmbraSetupLead => Loc.Get("UmbraSetup.Lead");

    public static string UmbraSetupAdd => Loc.Get("UmbraSetup.Add");

    public static string UmbraSetupShowHow => Loc.Get("UmbraSetup.ShowHow");

    public static string UmbraSetupNotNow => Loc.Get("UmbraSetup.NotNow");

    public static string UmbraSetupLater => Loc.Get("UmbraSetup.Later");

    public static string UmbraSetupConfirmTitle => Loc.Get("UmbraSetup.ConfirmTitle");

    public static string UmbraSetupChecking => Loc.Get("UmbraSetup.Checking");

    public static string UmbraSetupChangeCustomPlugins => Loc.Get("UmbraSetup.ChangeCustomPlugins");

    public static string UmbraSetupWarning => Loc.Get("UmbraSetup.Warning");

    public static string UmbraSetupChangeRepositoryFormat => Loc.Get("UmbraSetup.ChangeRepositoryFormat");

    public static string UmbraSetupChangeRestart => Loc.Get("UmbraSetup.ChangeRestart");

    public static string UmbraSetupChangeWidget => Loc.Get("UmbraSetup.ChangeWidget");

    public static string UmbraSetupNothingToChange => Loc.Get("UmbraSetup.NothingToChange");

    public static string UmbraSetupAgreement => Loc.Get("UmbraSetup.Agreement");

    public static string UmbraSetupAgree => Loc.Get("UmbraSetup.Agree");

    public static string UmbraSetupBack => Loc.Get("UmbraSetup.Back");

    public static string UmbraSetupClose => Loc.Get("UmbraSetup.Close");

    public static string UmbraSetupByHand => Loc.Get("UmbraSetup.ByHand");

    public static string UmbraSetupProgressTitle => Loc.Get("UmbraSetup.ProgressTitle");

    public static string UmbraSetupStepCustomPlugins => Loc.Get("UmbraSetup.StepCustomPlugins");

    public static string UmbraSetupStepRepository => Loc.Get("UmbraSetup.StepRepository");

    public static string UmbraSetupStepRestart => Loc.Get("UmbraSetup.StepRestart");

    public static string UmbraSetupStepWidget => Loc.Get("UmbraSetup.StepWidget");

    public static string UmbraSetupStepHello => Loc.Get("UmbraSetup.StepHello");

    public static string UmbraSetupPuttingBack => Loc.Get("UmbraSetup.PuttingBack");

    public static string UmbraSetupKeepsGoing => Loc.Get("UmbraSetup.KeepsGoing");

    public static string UmbraSetupAddedTitle => Loc.Get("UmbraSetup.AddedTitle");

    public static string UmbraSetupAddedFormat => Loc.Get("UmbraSetup.AddedFormat");

    public static string UmbraSetupNotConfirmedTitle => Loc.Get("UmbraSetup.NotConfirmedTitle");

    public static string UmbraSetupNotConfirmedLine => Loc.Get("UmbraSetup.NotConfirmedLine");

    public static string UmbraSetupFailedTitle => Loc.Get("UmbraSetup.FailedTitle");

    public static string UmbraSetupPutBack => Loc.Get("UmbraSetup.PutBack");

    public static string UmbraSetupNotPutBack => Loc.Get("UmbraSetup.NotPutBack");

    public static string UmbraSetupTryAgain => Loc.Get("UmbraSetup.TryAgain");

    public static string UmbraSetupOpenUmbraSettings => Loc.Get("UmbraSetup.OpenUmbraSettings");

    public static string UmbraSetupOpenUmbraFailed => Loc.Get("UmbraSetup.OpenUmbraFailed");

    public static string UmbraSetupCopyLink => Loc.Get("UmbraSetup.CopyLink");

    public static string UmbraSetupSettingsAddLabel => Loc.Get("UmbraSetup.SettingsAddLabel");

    public static string UmbraSetupSettingsAddHint => Loc.Get("UmbraSetup.SettingsAddHint");

    public static string UmbraSetupSettingsAddButton => Loc.Get("UmbraSetup.SettingsAddButton");

    public static string UmbraSetupSettingsBusyLabel => Loc.Get("UmbraSetup.SettingsBusyLabel");

    public static string UmbraSetupSettingsRemoveLabel => Loc.Get("UmbraSetup.SettingsRemoveLabel");

    public static string UmbraSetupSettingsRemoveHint => Loc.Get("UmbraSetup.SettingsRemoveHint");

    public static string UmbraSetupSettingsRemoveButton => Loc.Get("UmbraSetup.SettingsRemoveButton");

    public static string UmbraSetupRemoveWidgets => Loc.Get("UmbraSetup.RemoveWidgets");

    public static string UmbraSetupRemoveRepository => Loc.Get("UmbraSetup.RemoveRepository");

    public static string UmbraSetupRemoveCustomPlugins => Loc.Get("UmbraSetup.RemoveCustomPlugins");

    public static string UmbraSetupRemoveConfirm => Loc.Get("UmbraSetup.RemoveConfirm");

    public static string UmbraSetupRemoveKeep => Loc.Get("UmbraSetup.RemoveKeep");

    public static string UmbraSetupRemoving => Loc.Get("UmbraSetup.Removing");

    public static string UmbraSetupRemoved => Loc.Get("UmbraSetup.Removed");

    public static string UmbraSetupRemovedKeptOn => Loc.Get("UmbraSetup.RemovedKeptOn");

    public static string UmbraSetupRemoveStoppedFormat => Loc.Get("UmbraSetup.RemoveStoppedFormat");

    public static string UmbraSetupOtherProfileFormat => Loc.Get("UmbraSetup.OtherProfileFormat");

    public static string UmbraSetupNotNowReason => Loc.Get("UmbraSetup.NotNowReason");

    public static string UmbraSetupPutBackKeptOn => Loc.Get("UmbraSetup.PutBackKeptOn");

    /// <summary>Why setting up or removing stopped, in plain words.</summary>
    public static string UmbraSetupReason(UmbraFailure failure) => failure switch
    {
        UmbraFailure.NotRunning => Loc.Get("UmbraSetup.Reason.NotRunning"),
        UmbraFailure.SeveralUmbras => Loc.Get("UmbraSetup.Reason.SeveralUmbras"),
        UmbraFailure.OtherProfile => Loc.Get("UmbraSetup.Reason.OtherProfile"),
        UmbraFailure.RestartTimedOut => Loc.Get("UmbraSetup.Reason.RestartTimedOut"),
        UmbraFailure.UnknownUmbra => Loc.Get("UmbraSetup.Reason.UnknownUmbra"),
        UmbraFailure.ReleaseUnreachable => Loc.Get("UmbraSetup.Reason.ReleaseUnreachable"),
        UmbraFailure.ReleaseRefused => Loc.Get("UmbraSetup.Reason.ReleaseRefused"),
        UmbraFailure.NotLoaded => Loc.Get("UmbraSetup.Reason.NotLoaded"),
        UmbraFailure.WidgetNotPlaced => Loc.Get("UmbraSetup.Reason.WidgetNotPlaced"),
        UmbraFailure.TimedOut => Loc.Get("UmbraSetup.Reason.TimedOut"),
        _ => Loc.Get("UmbraSetup.Reason.Unexpected"),
    };
}
