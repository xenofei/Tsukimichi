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
/// <item><b>Set it up for you</b> (while the add-on is missing): "Add to Umbra…" opens the card on its confirmation,
/// where Agree and add is the one button that changes Umbra.</item>
/// <item><b>Remove from Umbra</b> (while Tsukimichi has changes in Umbra): "Remove…" lists exactly what will be undone,
/// and Remove undoes only that (<see cref="UmbraSetupRunner.Remove"/>); Keep it cancels.</item>
/// <item>While either runs, the step it is on.</item>
/// </list>
/// </summary>
public sealed partial class ConfigWindow
{
    private const string UmbraSetupKeywords = UmbraKeywords + " set up setup automatic add remove undo repository widget custom plugins";

    private bool umbraRemoveAsk;
    private (UmbraFailure Failure, bool Kept, int Language) umbraRemovedKey = (UmbraFailure.None, false, -1);
    private string umbraRemovedLine = string.Empty;

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
        var record = setup.Record;
        if (state.Activity is UmbraSetupActivity.Adding or UmbraSetupActivity.PuttingBack or UmbraSetupActivity.Waiting)
        {
            Note(Strings.UmbraSetupSettingsBusyLabel, BusyLine(state), UmbraSetupKeywords);
        }
        else if (!umbra.AddonPresent && ShowUmbraConfirm is { } show)
        {
            // The last try's reason, when it failed; otherwise what Add does.
            var hint = state.Outcome is UmbraSetupOutcome.Failed or UmbraSetupOutcome.FailedPartly ? Strings.UmbraSetupReason(state.Failure) : Strings.UmbraSetupSettingsAddHint;
            if (ButtonRow(Strings.UmbraSetupSettingsAddLabel, hint, umbraSetupAddLabel.Value, UmbraSetupKeywords))
            {
                show();
            }
        }

        var removing = state.Activity == UmbraSetupActivity.Removing;
        if (record.IsEmpty && !removing && !state.RemoveDone)
        {
            return;
        }

        var button = removing ? umbraSetupRemovingLabel.Value : umbraSetupRemoveLabel.Value;
        if (!Setting(Strings.UmbraSetupSettingsRemoveLabel, Strings.UmbraSetupSettingsRemoveHint, UmbraSetupKeywords, ImGui.CalcTextSize(button, true).X + (ImGui.GetStyle().FramePadding.X * 2f)))
        {
            return;
        }

        var canAsk = !record.IsEmpty && state.Activity == UmbraSetupActivity.Idle;
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

        if (umbraRemoveAsk && canAsk)
        {
            // The confirmation: exactly what will be undone, then Remove or Keep it.
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
            SettingNote(UmbraRemovedLine(state.RemoveFailure, state.KeptCustomPluginsOn));
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
}
