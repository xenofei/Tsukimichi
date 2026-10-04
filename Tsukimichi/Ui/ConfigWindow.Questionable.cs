using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Companions;
using Tsukimichi.Game;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings › Automation › Questionable (feature plan v5, 1.6.0, decision 1): whether Questionable is loaded with the
/// plugins it needs to run (vnavmesh, TextAdvance, Lifestream), "Add to priority list" (the detail pane's "Add to
/// Questionable priority"), "Allow starting Questionable" (on by default) and, under it, "Ask before starting" (on
/// until the player ticks "Don't ask again" in the confirmation), and "Before a duty with other players" (1.18.0, A3:
/// Stop, Warn or Do nothing). Send to Questionable itself needs no setting: it is a button the player presses, disabled
/// without Questionable. "Confirm Stop" lives in Advanced. Since 1.18.0 (feature plan v7 A4) "Recent runs" lists the
/// run receipts. The start settings follow the automation level; the duty guard and Recent runs show whenever
/// Questionable is installed, since the run watch guards and keeps a receipt of every run, whoever started it.
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>Questionable's IPC, for the loaded line; set by the plugin. Null leaves the line out.</summary>
    public QuestionableIpc? Questionable { get; set; }

    /// <summary>The Questionable run watch, for the receipts (feature plan v7 A4); set by the plugin. Null leaves them out.</summary>
    public QuestionableRunWatch? QuestionableRuns { get; set; }

    // The receipts as lines, composed again when one is added, the language changes or the day turns (a receipt from
    // before midnight then shows its date).
    private string[] runsLines = [];
    private int runsLinesVersion = -1;
    private int runsLinesLanguage = -1;
    private DateTime runsLinesDay;

    private void DrawQuestionableSettings()
    {
        // The start settings go with the automation level (1.18, A10): below it no button starts a run, so there is no
        // start to confirm. The duty guard and Recent runs do not: they serve every Questionable run, however it started.
        var buttons = AutomationGate.Shows(AutomationButtons.Questionable);
        var installed = CompanionInstalled(CompanionPlugin.Questionable);
        if (!buttons && !installed)
        {
            return;
        }

        Header(Strings.ConfigQuestionableSection);
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

            Note(Strings.SettingsQuestionableStatus, line, "questionable loaded status vnavmesh textadvance lifestream");
        }

        if (buttons)
        {
            DrawQuestionableStartSettings();
        }

        if (!installed)
        {
            return;
        }

        // 1.18.0 (A3): what happens before a duty with no Duty Support or Trust; read each frame by Game.RunWatch.
        var guard = Enum.IsDefined(settings.QuestionableDutyGuard) ? (int)settings.QuestionableDutyGuard : 0;
        if (Choice(Strings.ConfigQuestionableDutyGuard, Strings.ConfigQuestionableDutyGuardHint, ref guard, DutyGuardOptions.Value, "questionable duty support trust duty finder other players party stop warn guard queue"))
        {
            settings.QuestionableDutyGuard = (DutyGuardMode)guard;
            Save();
        }

        if (QuestionableRuns is { } runs)
        {
            DrawRecentRuns(runs);
        }
    }

    /// <summary>Add to priority list, Allow starting and Ask before starting: what the Questionable buttons do.</summary>
    private void DrawQuestionableStartSettings()
    {
        // Read per use by the detail pane, so no callback is needed.
        var handoff = settings.QuestionableHandoff;
        if (Toggle(Strings.ConfigQuestionableHandoff, Strings.ConfigQuestionableHandoffHint, ref handoff, "questionable priority list add hand-off"))
        {
            settings.QuestionableHandoff = handoff;
            Save();
        }

        var allowStart = settings.QuestionableAllowStart;
        if (Toggle(Strings.ConfigQuestionableAllowStart, Strings.ConfigQuestionableAllowStartHint, ref allowStart, "questionable start automation"))
        {
            settings.QuestionableAllowStart = allowStart;
            Save();
        }

        var confirm = settings.QuestionableConfirmStart;
        if (Toggle(Strings.ConfigQuestionableConfirmStart, Strings.ConfigQuestionableConfirmStartHint, ref confirm, "questionable start confirm ask", settings.QuestionableAllowStart, sub: true, reason: Strings.SettingsQuestionableStartOffReason))
        {
            settings.QuestionableConfirmStart = confirm;
            Save();
        }
    }

    /// <summary>
    /// Whether <paramref name="plugin"/> is installed (loaded, turned off or outdated); true while the registry is not
    /// attached (tools), so nothing hides for want of it.
    /// </summary>
    private bool CompanionInstalled(CompanionPlugin plugin) =>
        Companions is not { } companions || companions.Status(plugin).State != CompanionState.Missing;

    /// <summary>
    /// "Recent runs": the receipts as a list under the row, newest first ("21:04 · Questionable ran 42 min: …"), each
    /// line in full. Before the first run the row's hint says what will show there.
    /// </summary>
    private void DrawRecentRuns(QuestionableRunWatch runs)
    {
        var lines = RunsLines(runs);
        if (!Setting(Strings.SettingsQuestionableRuns, lines.Length == 0 ? Strings.SettingsQuestionableRunsNone : null, "questionable runs receipt history stopped why quests done", 0f))
        {
            return;
        }

        if (lines.Length > 0)
        {
            SettingBelow();
            using (Typography.Caption())
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                foreach (var line in lines)
                {
                    ImGui.TextWrapped(line);
                }
            }
        }

        EndSetting();
    }

    /// <summary>The receipts' lines, newest first: the time alone for today's, the date and time for an earlier day's.</summary>
    private string[] RunsLines(QuestionableRunWatch runs)
    {
        var today = DateTime.Today;
        if (runsLinesVersion == runs.Version && runsLinesLanguage == Localization.Loc.Version && runsLinesDay == today)
        {
            return runsLines;
        }

        runsLinesVersion = runs.Version;
        runsLinesLanguage = Localization.Loc.Version;
        runsLinesDay = today;
        var lines = new string[runs.Receipts.Count];
        for (var i = 0; i < lines.Length; i++)
        {
            var receipt = runs.Receipts[i];
            var when = receipt.EndedUtc.ToLocalTime();
            var stamp = when.Date == today ? when.ToString("t", CultureInfo.CurrentCulture) : when.ToString("g", CultureInfo.CurrentCulture);
            lines[i] = string.Format(CultureInfo.CurrentCulture, Strings.SettingsQuestionableRunRowFormat, stamp, runs.Describe(receipt));
        }

        runsLines = lines;
        return runsLines;
    }

    /// <summary>The duty guard's choices, in <see cref="DutyGuardMode"/> order.</summary>
    private static readonly LocArray DutyGuardOptions = new(static () =>
        [Strings.DutyGuardOptionStop, Strings.DutyGuardOptionWarn, Strings.DutyGuardOptionNothing]);

    /// <summary>Settings › Advanced › Questionable: ask before Stop when Questionable runs a command after being stopped.</summary>
    private void DrawQuestionableStopConfirm()
    {
        Header(Strings.ConfigQuestionableSection);
        var confirm = settings.QuestionableConfirmStopCommand;
        if (Toggle(Strings.ConfigQuestionableConfirmStopCommand, Strings.ConfigQuestionableConfirmStopCommandHint, ref confirm, "questionable stop confirm ask command after lifestream"))
        {
            settings.QuestionableConfirmStopCommand = confirm;
            Save();
        }
    }
}
