using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Commands;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Keyboard › Chat commands (1.11.0, A12): extra names for <c>/tsuki</c>, typed as one line and applied when
/// the field is left, so a half-typed alias is never registered. Under it, which names Tsukimichi answers to, and one
/// line each for words that are not an alias and for aliases something else already uses.
/// </summary>
public sealed partial class ConfigWindow
{
    private const int CommandAliasesMaxLength = 256;

    // The lines under the field, rebuilt only when the aliases change or the language does.
    private int aliasLinesVersion = -1;
    private int aliasLinesLanguage = -1;
    private string aliasActiveLine = string.Empty;
    private string? aliasSkippedLine;
    private string? aliasInvalidLine;

    /// <summary>The chat command, whose aliases this block edits; set by the plugin. Null hides the block.</summary>
    public TsukimichiCommand? Command { get; set; }

    private void DrawCommandAliases()
    {
        if (Command is not { } command)
        {
            return;
        }

        Header(Strings.ConfigCommandAliasesHeading);
        if (!Row(Strings.ConfigCommandAliases, Strings.ConfigCommandAliasesHint, "alias aliases command commands slash chat macro /ts /moon"))
        {
            return;
        }

        var text = settings.CommandAliases ?? string.Empty;
        ImGui.SetNextItemWidth(Chrome.FitWidth(UiMetrics.Px(260f)));
        if (ImGui.InputTextWithHint("##commandAliases", "/quests /tm", ref text, CommandAliasesMaxLength))
        {
            settings.CommandAliases = text;
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            Save();
            command.ApplyAliases(settings.CommandAliases);
        }

        HintOnHover(Strings.ConfigCommandAliasesHint);
        Chrome.TrailingLabel(Strings.ConfigCommandAliases);

        RefreshAliasLines(command);
        using (ImRaii.TextWrapPos(0f))
        {
            Chrome.Hint(aliasActiveLine);
            if (aliasInvalidLine is not null || aliasSkippedLine is not null)
            {
                using var eclipse = Theme.PushText(Theme.Eclipse);
                if (aliasInvalidLine is not null)
                {
                    ImGui.TextWrapped(aliasInvalidLine);
                }

                if (aliasSkippedLine is not null)
                {
                    ImGui.TextWrapped(aliasSkippedLine);
                }
            }
        }
    }

    private void RefreshAliasLines(TsukimichiCommand command)
    {
        var language = Localization.Loc.Version;
        if (aliasLinesVersion == command.AliasesVersion && aliasLinesLanguage == language)
        {
            return;
        }

        aliasLinesVersion = command.AliasesVersion;
        aliasLinesLanguage = language;
        aliasActiveLine = string.Format(CultureInfo.CurrentCulture, Strings.ConfigCommandAliasesActiveFormat, string.Join(Strings.CommandListSeparator, command.ActiveAliases));
        aliasSkippedLine = command.SkippedAliases.Count == 0
            ? null
            : string.Format(CultureInfo.CurrentCulture, Strings.ConfigCommandAliasesSkippedFormat, string.Join(Strings.CommandListSeparator, command.SkippedAliases));
        aliasInvalidLine = command.InvalidAliases.Count == 0
            ? null
            : string.Format(CultureInfo.CurrentCulture, Strings.ConfigCommandAliasesInvalidFormat, string.Join(Strings.CommandListSeparator, command.InvalidAliases));
    }
}
