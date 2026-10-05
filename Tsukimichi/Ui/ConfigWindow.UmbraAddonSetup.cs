using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Umbra;
using Tsukimichi.Game;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › About › Umbra, setting up Tsukimichi for Umbra (<see cref="UmbraAddonSetupService"/>): the same actions as
/// the "Add Tsukimichi to your Umbra bar?" card, whatever the player answered there.
/// <list type="bullet">
/// <item><b>Set it up for you</b> (while the add-on is missing and no Remove runs): "Add to Umbra…" opens the card on
/// its confirmation, where Agree and add is the one button that changes Umbra.</item>
/// <item><b>Remove from Umbra</b> (while Tsukimichi has changes on this character's Umbra profile): "Remove…" lists
/// exactly what will be undone, and Remove undoes only that (<see cref="UmbraSetupRunner.Remove"/>); Keep it cancels.
/// Changes on another Umbra profile are named, and nothing is offered here.</item>
/// <item>Both restart Umbra's toolbar, so both are off in a fight, a duty or a cutscene, with the reason.</item>
/// <item>While either runs, the step it is on.</item>
/// </list>
/// </summary>
public sealed partial class ConfigWindow
{
    private const string UmbraSetupKeywords = UmbraKeywords + " set up setup automatic add remove undo repository widget custom plugins profile";

    private bool umbraRemoveAsk;
    private (UmbraFailure Failure, bool Kept, int Language) umbraRemovedKey = (UmbraFailure.None, false, -1);
    private string umbraRemovedLine = string.Empty;
    private (UmbraSetupBook? Book, string? Profile, int Language) umbraOtherKey = (null, null, -1);
    private string umbraOtherLine = string.Empty;

    // The button labels with their ids, made once per language (no string is built per frame).
    private readonly LocText umbraSetupAddLabel = new(static () => Strings.UmbraSetupSettingsAddButton + "##umbraSetupAdd");
    private readonly LocText umbraSetupRemoveLabel = new(static () => Strings.UmbraSetupSettingsRemoveButton + "##umbraSetupRemove");
    private readonly LocText umbraSetupRemovingLabel = new(static () => Strings.UmbraSetupRemoving + "##umbraSetupRemove");
    private readonly LocText umbraSetupConfirmLabel = new(static () => Strings.UmbraSetupRemoveConfirm + "##umbraSetupRemoveConfirm");
    private readonly LocText umbraSetupKeepLabel = new(static () => Strings.UmbraSetupRemoveKeep + "##umbraSetupRemoveKeep");
    private readonly LocText umbraOpenSettingsLabel = new(static () => Strings.UmbraSetupOpenUmbraSettings + "##umbraOpenSettings");

    /// <summary>Setting up the add-on in Umbra; set by the plugin. Null hides these rows.</summary>
    public UmbraAddonSetupService? UmbraSetup { get; set; }

    /// <summary>Opens the add-on card on its confirmation; set by the plugin.</summary>
    public Action? ShowUmbraConfirm { get; set; }

    private void DrawUmbraAddonSetup(UmbraProbe umbra)
    {
        if (UmbraSetup is not { } setup)
        {
            return;
        }

        var state = setup.View;
        var canChange = setup.CanChangeNow;
        var removing = state.Activity == UmbraSetupActivity.Removing;
        if (state.Activity is UmbraSetupActivity.Adding or UmbraSetupActivity.PuttingBack or UmbraSetupActivity.Waiting)
        {
            Note(Strings.UmbraSetupSettingsBusyLabel, BusyLine(state), UmbraSetupKeywords);
        }
        else if (!removing && !umbra.AddonPresent && ShowUmbraConfirm is { } show)
        {
            // The last try's reason, when it failed; otherwise what Add does.
            var hint = state.Outcome is UmbraSetupOutcome.Failed or UmbraSetupOutcome.FailedPartly ? Strings.UmbraSetupReason(state.Failure) : Strings.UmbraSetupSettingsAddHint;
            if (ButtonRow(Strings.UmbraSetupSettingsAddLabel, hint, umbraSetupAddLabel.Value, UmbraSetupKeywords, enabled: canChange, reason: Strings.UmbraSetupNotNowReason))
            {
                show();
            }
        }

        var book = setup.Book;
        if (book.IsEmpty && !removing && !state.RemoveDone)
        {
            return;
        }

        // This character's Umbra profile (as Umbra's settings were last read); unknown before a read, when the Remove
        // itself finds the live profile and refuses any other.
        var profile = setup.CurrentProfile;
        var record = profile is null ? (book.IsEmpty ? null : book.Records[0]) : book.For(profile);
        var here = record is { IsEmpty: false };
        if (!here && !removing && !book.IsEmpty)
        {
            // Changes only on other profiles: say where, offer nothing.
            Note(Strings.UmbraSetupSettingsRemoveLabel, UmbraOtherProfileLine(book, profile), UmbraSetupKeywords);
            return;
        }

        var button = removing ? umbraSetupRemovingLabel.Value : umbraSetupRemoveLabel.Value;
        if (!Setting(Strings.UmbraSetupSettingsRemoveLabel, Strings.UmbraSetupSettingsRemoveHint, UmbraSetupKeywords, ImGui.CalcTextSize(button, true).X + (ImGui.GetStyle().FramePadding.X * 2f), enabled: canChange || removing, reason: Strings.UmbraSetupNotNowReason))
        {
            return;
        }

        var canAsk = here && state.Activity == UmbraSetupActivity.Idle && canChange;
        if (!canAsk)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button(button))
        {
            umbraRemoveAsk = !umbraRemoveAsk;
        }

        if (!canAsk)
        {
            ImGui.EndDisabled();
        }

        if (umbraRemoveAsk && canAsk && record is not null)
        {
            // The confirmation: exactly what will be undone on this profile, then Remove or Keep it.
            SettingBelow();
            using (Typography.Caption())
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                if (record.WidgetIds.Count > 0)
                {
                    ImGui.TextWrapped(Strings.UmbraSetupRemoveWidgets);
                }

                if (record.AddedRepository)
                {
                    ImGui.TextWrapped(Strings.UmbraSetupRemoveRepository);
                }

                if (record.TurnedOnCustomPlugins)
                {
                    ImGui.TextWrapped(Strings.UmbraSetupRemoveCustomPlugins);
                }
            }

            if (ImGui.Button(umbraSetupConfirmLabel.Value))
            {
                umbraRemoveAsk = false;
                setup.RemoveFromUmbra();
            }

            ImGui.SameLine();
            if (ImGui.Button(umbraSetupKeepLabel.Value))
            {
                umbraRemoveAsk = false;
            }
        }
        else if (state.RemoveDone && !removing)
        {
            SettingNote(state.RemoveFailure == UmbraFailure.OtherProfile
                ? UmbraOtherProfileLine(book, profile)
                : UmbraRemovedLine(state.RemoveFailure, state.KeptCustomPluginsOn));
        }

        EndSetting();
    }

    /// <summary>The step a running setup is on.</summary>
    private static string BusyLine(in UmbraSetupView state) => state.Activity switch
    {
        UmbraSetupActivity.Waiting => Strings.UmbraSetupStepHello,
        UmbraSetupActivity.PuttingBack => Strings.UmbraSetupPuttingBack,
        _ => state.Step switch
        {
            UmbraSetupStep.TurnOnCustomPlugins => Strings.UmbraSetupStepCustomPlugins,
            UmbraSetupStep.AddRepository => Strings.UmbraSetupStepRepository,
            UmbraSetupStep.RestartUmbra => Strings.UmbraSetupStepRestart,
            UmbraSetupStep.PlaceWidget => Strings.UmbraSetupStepWidget,
            _ => Strings.UmbraSetupChecking,
        },
    };

    /// <summary>How the last Remove ended, rebuilt when that or the language changes.</summary>
    private string UmbraRemovedLine(UmbraFailure failure, bool kept)
    {
        var key = (failure, kept, Loc.Version);
        if (key != umbraRemovedKey || umbraRemovedLine.Length == 0)
        {
            umbraRemovedKey = key;
            umbraRemovedLine = failure != UmbraFailure.None
                ? string.Format(CultureInfo.CurrentCulture, Strings.UmbraSetupRemoveStoppedFormat, Strings.UmbraSetupReason(failure))
                : kept ? Strings.UmbraSetupRemovedKeptOn : Strings.UmbraSetupRemoved;
        }

        return umbraRemovedLine;
    }

    /// <summary>"Tsukimichi's changes are on Umbra profile Main…", rebuilt when the book, the profile or the language changes.</summary>
    private string UmbraOtherProfileLine(UmbraSetupBook book, string? profile)
    {
        var language = Loc.Version;
        if (!ReferenceEquals(book, umbraOtherKey.Book) || !string.Equals(profile, umbraOtherKey.Profile, StringComparison.Ordinal) || language != umbraOtherKey.Language || umbraOtherLine.Length == 0)
        {
            umbraOtherKey = (book, profile, language);
            umbraOtherLine = string.Format(CultureInfo.CurrentCulture, Strings.UmbraSetupOtherProfileFormat, string.Join(", ", book.OtherProfiles(profile)));
        }

        return umbraOtherLine;
    }
}
