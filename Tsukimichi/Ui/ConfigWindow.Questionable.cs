using System.Globalization;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Integrations › Questionable (feature plan v5, 1.6.0, decision 1): whether Questionable is loaded with the
/// plugins it needs to run (vnavmesh, TextAdvance, Lifestream), "Allow Tsukimichi to start Questionable" (on by
/// default) and "Ask before starting Questionable" (on until the player ticks "Don't ask again" in the confirmation).
/// Send to Questionable itself needs no setting: it is a button the player presses, disabled without Questionable.
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>Questionable's IPC, for the loaded line; set by the plugin. Null leaves the line out.</summary>
    public QuestionableIpc? Questionable { get; set; }

    private void DrawQuestionableSettings()
    {
        ImGui.Spacing();
        ImGui.TextDisabled(Strings.ConfigQuestionableSection);
        if (Questionable is { } questionable)
        {
            var required = string.Join(", ", QuestionableIpc.RequiredPlugins);
            string line;
            if (!questionable.Available)
            {
                line = Strings.ConfigQuestionableStatusAbsent;
            }
            else if (questionable.MissingRequiredPlugins is { Count: > 0 } missing)
            {
                line = string.Format(CultureInfo.CurrentCulture, Strings.ConfigQuestionableStatusFormat, required, string.Join(", ", missing));
            }
            else
            {
                line = string.Format(CultureInfo.CurrentCulture, Strings.ConfigQuestionableStatusReadyFormat, required);
            }

            Chrome.Hint(line);
        }

        var allowStart = settings.QuestionableAllowStart;
        if (ImGui.Checkbox(Strings.ConfigQuestionableAllowStart, ref allowStart))
        {
            settings.QuestionableAllowStart = allowStart;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ConfigQuestionableAllowStartHint);
        }

        using (Dalamud.Interface.Utility.Raii.ImRaii.Disabled(!settings.QuestionableAllowStart))
        {
            var confirm = settings.QuestionableConfirmStart;
            if (ImGui.Checkbox(Strings.ConfigQuestionableConfirmStart, ref confirm))
            {
                settings.QuestionableConfirmStart = confirm;
                Save();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.ConfigQuestionableConfirmStartHint);
        }
    }
}
