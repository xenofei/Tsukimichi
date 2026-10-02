using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Chains;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Display › Free trial and story recap (1.9.0 collector extras, feature plan v5): "I'm on the free trial",
/// off by default (R9 F3: the quest counts, My blues and the quest table fold what the trial does not include into
/// "Beyond your trial"), and how many main scenario quests the story recap reads (R9 F5). When the game's own
/// condition flag says the account is on the free trial while the view is off, a line says so; the view never turns
/// itself on.
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
        if (Row(Strings.TrialSetting, Strings.TrialSettingHint, "free trial beyond your trial shadowbringers level 80 starter"))
        {
            var trial = settings.FreeTrialView;
            if (ImGui.Checkbox(Strings.TrialSetting, ref trial))
            {
                settings.FreeTrialView = trial;
                Save();
            }

            Chrome.Hint(Strings.TrialSettingHint);
            if (!trial && FreeTrialDetected?.Invoke() == true)
            {
                using (ImRaii.PushIndent())
                using (Theme.PushText(Theme.Moon))
                {
                    ImGui.TextWrapped(Strings.TrialDetected);
                }
            }
        }

        if (Row(Strings.RecapLengthSetting, Strings.RecapLengthHint, "story recap previously journal length main scenario"))
        {
            var length = settings.RecapLengthClamped;
            ImGui.SetNextItemWidth(Chrome.FitWidth(UiMetrics.Px(160f)));
            if (ImGui.SliderInt("##recapLength", ref length, StoryRecap.MinLength, StoryRecap.MaxLength, Strings.RecapLengthFormat, ImGuiSliderFlags.AlwaysClamp))
            {
                settings.RecapLength = length;
                Save();
            }

            Chrome.TrailingLabel(Strings.RecapLengthSetting);
            Chrome.Hint(Strings.RecapLengthHint);
        }
    }
}
