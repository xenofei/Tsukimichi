using System;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Chains;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Journal › Story and account (1.9.0 collector extras, feature plan v5): how many main scenario quests the
/// story recap reads (R9 F5), and "Free trial account", off by default (R9 F3: the quest counts, My blues and the quest
/// table fold what the trial does not include into "Beyond your trial"). When the game's own condition flag says the
/// account is on the free trial while the view is off, a line says so; the view never turns itself on.
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>
    /// Whether the game's condition flag reports a free-trial account (<c>ConditionFlag.OnFreeTrial</c>); set by the
    /// plugin. Unverified on a trial account, so it only adds a hint. Null leaves the hint out.
    /// </summary>
    public Func<bool>? FreeTrialDetected { get; set; }

    private void DrawCollectorSettings()
    {
        Header(Strings.CollectorSettingsHeading);
        if (Setting(Strings.RecapLengthSetting, Strings.RecapLengthHint, "story recap previously journal length main scenario"))
        {
            var length = settings.RecapLengthClamped;
            ImGui.SetNextItemWidth(ControlWidth);
            if (ImGui.SliderInt("##recapLength", ref length, StoryRecap.MinLength, StoryRecap.MaxLength, Strings.RecapLengthFormat, ImGuiSliderFlags.AlwaysClamp))
            {
                settings.RecapLength = length;
                SaveSoon();
            }

            EndSetting();
        }

        if (!ToggleSetting(Strings.TrialSetting, Strings.TrialSettingHint, "free trial beyond your trial shadowbringers level 80 starter"))
        {
            return;
        }

        var trial = settings.FreeTrialView;
        if (RowToggle(ref trial))
        {
            settings.FreeTrialView = trial;
            Save();
        }

        if (!settings.FreeTrialView && FreeTrialDetected?.Invoke() == true)
        {
            SettingNote(Strings.TrialDetected, Theme.Moon);
        }

        EndSetting();
    }
}
