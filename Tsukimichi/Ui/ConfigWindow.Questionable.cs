using System;
using System.Globalization;
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
/// run receipts.
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>Questionable's IPC, for the loaded line; set by the plugin. Null leaves the line out.</summary>
    public QuestionableIpc? Questionable { get; set; }

    /// <summary>The Questionable run watch, for the receipts (feature plan v7 A4); set by the plugin. Null leaves them out.</summary>
    public QuestionableRunWatch? QuestionableRuns { get; set; }

    // The receipts' text, composed when a receipt is added or the language changes.
    private string runsText = string.Empty;
    private int runsTextVersion = -1;
    private int runsTextLanguage = -1;

    private void DrawQuestionableSettings()
    {
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

        // 1.18.0 (A3): what happens before a duty with no Duty Support or Trust; read each frame by Game.RunWatch.
        var guard = Enum.IsDefined(settings.QuestionableDutyGuard) ? (int)settings.QuestionableDutyGuard : 0;
        if (Choice(Strings.ConfigQuestionableDutyGuard, Strings.ConfigQuestionableDutyGuardHint, ref guard, DutyGuardOptions.Value, "questionable duty support trust duty finder other players party stop warn guard queue"))
        {
            settings.QuestionableDutyGuard = (DutyGuardMode)guard;
            Save();
        }

        if (QuestionableRuns is { } runs)
        {
            Note(Strings.SettingsQuestionableRuns, RunsText(runs), "questionable runs receipt history stopped why quests done");
        }
    }

    /// <summary>The receipts as lines, newest first ("21:04 · Questionable ran 42 min: …"), composed again only when one is added.</summary>
    private string RunsText(QuestionableRunWatch runs)
    {
        if (runsTextVersion == runs.Version && runsTextLanguage == Localization.Loc.Version)
        {
            return runsText;
        }

        runsTextVersion = runs.Version;
        runsTextLanguage = Localization.Loc.Version;
        if (runs.Receipts.Count == 0)
        {
            runsText = Strings.SettingsQuestionableRunsNone;
            return runsText;
        }

        var lines = new string[runs.Receipts.Count];
        for (var i = 0; i < lines.Length; i++)
        {
            var receipt = runs.Receipts[i];
            var when = receipt.EndedUtc.ToLocalTime();
            var stamp = when.Date == DateTime.Today ? when.ToString("t", CultureInfo.CurrentCulture) : when.ToString("g", CultureInfo.CurrentCulture);
            lines[i] = string.Format(CultureInfo.CurrentCulture, Strings.SettingsQuestionableRunRowFormat, stamp, runs.Describe(receipt));
        }

        runsText = string.Join("\n", lines);
        return runsText;
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
