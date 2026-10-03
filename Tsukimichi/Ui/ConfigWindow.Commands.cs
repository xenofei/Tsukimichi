using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Commands;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Advanced › Chat commands (1.11.0, A12): extra names for <c>/tsuki</c>, typed as one line and applied when
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

    // The aliases as typed, until the field is left (or Settings closes): only then are they saved and registered
    // together, so Settings never holds an alias that is not applied. Null when not editing.
    private string? aliasDraft;

    /// <summary>The chat command, whose aliases this block edits; set by the plugin. Null hides the block.</summary>
    public TsukimichiCommand? Command { get; set; }

    private void DrawCommandAliases()
    {
        if (Command is not { } command)
        {
            return;
        }

        Header(Strings.ConfigCommandAliasesHeading);
        if (!Setting(Strings.ConfigCommandAliases, Strings.ConfigCommandAliasesHint, "alias aliases command commands slash chat macro /ts /moon"))
        {
            return;
        }

        var text = aliasDraft ?? settings.CommandAliases ?? string.Empty;
        ImGui.SetNextItemWidth(ControlWidth);
        if (ImGui.InputTextWithHint("##commandAliases", "/quests /tm", ref text, CommandAliasesMaxLength))
        {
            aliasDraft = text;
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            ApplyAliasDraft();
        }

        RefreshAliasLines(command);
        SettingNote(aliasActiveLine);
        if (aliasInvalidLine is not null)
        {
            SettingNote(aliasInvalidLine, Theme.DangerText);
        }

        if (aliasSkippedLine is not null)
        {
            SettingNote(aliasSkippedLine, Theme.DangerText);
        }

        EndSetting();
    }

    /// <summary>
    /// Commits the aliases being typed: into Settings, saved, and registered, together. Runs when the field is left
    /// after an edit and when the Settings window closes with the field still being typed in.
    /// </summary>
    private void ApplyAliasDraft()
    {
        if (aliasDraft is not { } draft)
        {
            return;
        }

        aliasDraft = null;
        if (string.Equals(draft, settings.CommandAliases ?? string.Empty, StringComparison.Ordinal))
        {
            return;
        }

        settings.CommandAliases = draft;
        Save();
        Command?.ApplyAliases(settings.CommandAliases);
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
