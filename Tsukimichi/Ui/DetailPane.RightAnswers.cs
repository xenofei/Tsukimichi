using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Journal;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// "Right answers" (feature plan v7, 1.19.0) in the detail pane. Under the hero: "Switch to PLD" for a quest that needs a
/// job the current one is not (C8, an explicit button running the game's gearset change). Under the EXP amount: which
/// job gets the EXP and whether another would get more (C8). What the game's own offers say (C1) and the full journal
/// (C9) are in the hero and the disagreement card (DetailPane.GameAnswers.cs). Each line is rebuilt when the quest, the
/// session, the offers or the language change, so drawing allocates nothing.
/// </summary>
public sealed partial class DetailPane
{
    private static readonly string GearsetIcon = FontAwesomeIcon.Tshirt.ToIconString();

    /// <summary>The game's own offers (C1); their version moves the lines' key.</summary>
    public OfferObserver? Offers { get; set; }

    /// <summary>The gearset switch (C8); null leaves the button out.</summary>
    public GearsetSwitcher? Gearsets { get; set; }

    // What the lines were built from.
    private uint rightRowId = uint.MaxValue;
    private int rightVersion = -1;
    private int rightOffersVersion = -1;
    private int rightLanguage = -1;
    private long rightMinute = -1;

    private string expAdviceLine = string.Empty;
    private bool expAdviceWarns;
    private bool gearsetNeeded;
    private byte? gearsetPreferredJob;

    // "Switch to PLD", formatted once per gearset and language rather than every frame.
    private GearsetInfo? gearsetLabelFor;
    private int gearsetLabelLanguage = -1;
    private string gearsetLabel = string.Empty;

    /// <summary>Rebuilds the lines when the quest, the session, the offers, the language or the minute (the ages) moved.</summary>
    private void RefreshRightAnswers(SessionState session, QuestRecord quest)
    {
        var offersVersion = Offers?.Version ?? 0;
        var minute = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
        if (rightRowId == quest.RowId && rightVersion == session.Version && rightOffersVersion == offersVersion
            && rightLanguage == Localization.Loc.Version && rightMinute == minute)
        {
            return;
        }

        rightRowId = quest.RowId;
        rightVersion = session.Version;
        rightOffersVersion = offersVersion;
        rightLanguage = Localization.Loc.Version;
        rightMinute = minute;

        session.States.TryGetValue(quest.RowId, out var evaluation);
        expAdviceLine = string.Empty;
        expAdviceWarns = false;
        gearsetNeeded = false;
        gearsetPreferredJob = null;
        if (session.ViewedSnapshot is not { } snapshot || session.Bundle is not { } bundle || evaluation is null)
        {
            return;
        }

        gearsetNeeded = session.IsLive && GearsetChoice.Needed(quest, snapshot, session.Context, evaluation.State);
        gearsetPreferredJob = evaluation.ReadyOnJob;
        if (evaluation.State is not (QuestState.Completed or QuestState.Foreclosed or QuestState.DoneThisCycle)
            && ExpAdvisor.Advise(quest, snapshot, bundle.ExpTable, session.Context, IsLimitedJob(bundle)) is { } advice)
        {
            (expAdviceLine, expAdviceWarns) = AdviceText(advice, session.Names, snapshot.LevelCap);
            gearsetPreferredJob ??= advice.Best?.Job;
        }
    }

    /// <summary>
    /// Under the hero: Switch gearset. What the game's offers say and the full journal moved into the hero and the
    /// disagreement card (spec-1.19 C1 and C9, DetailPane.GameAnswers.cs).
    /// </summary>
    private void DrawRightAnswers(SessionState session, QuestRecord quest)
    {
        RefreshRightAnswers(session, quest);
        DrawGearsetSwitch(session, quest);
    }

    /// <summary>
    /// "Switch to PLD": only for a quest that needs a job the current one is not, and only with a gearset of a job that
    /// takes it. Disabled, saying why, while the game would refuse (combat, a duty, a cast…).
    /// </summary>
    private void DrawGearsetSwitch(SessionState session, QuestRecord quest)
    {
        if (!gearsetNeeded || Gearsets is not { } switcher || session.ViewedSnapshot is not { } snapshot)
        {
            return;
        }

        if (GearsetChoice.Pick(switcher.Gearsets(), quest, snapshot, session.Context, gearsetPreferredJob) is not { } gearset)
        {
            return;
        }

        if (gearsetLabelFor != gearset || gearsetLabelLanguage != Localization.Loc.Version)
        {
            gearsetLabelFor = gearset;
            gearsetLabelLanguage = Localization.Loc.Version;
            var job = session.Names.JobAbbreviation(gearset.Job);
            gearsetLabel = string.Format(CultureInfo.CurrentCulture, Strings.GearsetSwitchFormat, job.Length > 0 ? job : gearset.Name);
        }

        var blocker = switcher.Blocker();
        if (Chrome.ActionPill("##gearsetSwitch", GearsetIcon, gearsetLabel, PillTone.Normal, blocker is null, size: PillLayout.Row))
        {
            switcher.Switch(gearset);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            var tooltip = string.Format(CultureInfo.CurrentCulture, Strings.GearsetSwitchTooltipFormat, gearset.Id + 1, gearset.Name, gearset.ItemLevel);
            UiMetrics.Tooltip(blocker is null ? tooltip : tooltip + "\n" + string.Format(CultureInfo.CurrentCulture, Strings.GearsetSwitchBlockedFormat, blocker));
        }
    }

    /// <summary>Under the EXP amount: which job gets it, and the warning when another would get more.</summary>
    private void DrawExpAdvice(SessionState session, QuestRecord quest)
    {
        RefreshRightAnswers(session, quest);
        if (expAdviceLine.Length == 0)
        {
            return;
        }

        using (Theme.PushText(expAdviceWarns ? Theme.DangerText : Theme.Surface.TextSecondary))
        {
            TextFlow.Wrapped(expAdviceLine, RoomTo(cardRight));
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ExpAdviceTooltip);
        }
    }

    /// <summary>The bundle's limited jobs (Blue Mage, Beastmaster), which no EXP advice names.</summary>
    private static Func<byte, bool> IsLimitedJob(CatalogBundle bundle) => job =>
    {
        foreach (var info in bundle.Names.ClassJobInfos)
        {
            if (info.RowId == job)
            {
                return info.IsLimited;
            }
        }

        return false;
    };

    /// <summary>The advice as one line, and whether it warns.</summary>
    private static (string Text, bool Warns) AdviceText(ExpAdvice advice, BlockerNames names, byte levelCap)
    {
        var culture = CultureInfo.CurrentCulture;
        string Job(byte job) => names.JobAbbreviation(job) is { Length: > 0 } abbreviation ? abbreviation : job.ToString(culture);
        string Amount(ulong exp) => exp.ToString("N0", culture);

        var current = advice.Current;
        switch (advice.Warning)
        {
            case ExpWarning.NeedsJob:
                return advice.Best is { } needed
                    ? (string.Format(culture, Strings.ExpAdviceNeedsJobFormat, Job(needed.Job), needed.Level, Amount(needed.Exp)), false)
                    : (Strings.ExpAdviceNeedsJobNone, false);

            case ExpWarning.Capped when advice.Best is { } best:
                return (string.Format(culture, Strings.ExpAdviceCappedFormat, Job(current.Job), Job(best.Job), best.Level, Amount(best.Exp)), true);

            case ExpWarning.LessThanBest when advice.Best is { } more:
                return (string.Format(culture, Strings.ExpAdviceLessFormat, Job(current.Job), current.Level, Amount(current.Exp), Job(more.Job), more.Level, Amount(more.Exp)), true);
        }

        if (current.Exp == 0 && levelCap > 0 && current.Level >= levelCap)
        {
            return (string.Format(culture, Strings.ExpAdviceAllCappedFormat, Job(current.Job)), true);
        }

        return advice.Reward.Kind == Core.Rewards.ExpKind.Range
            ? (string.Format(culture, Strings.ExpAdviceGoesToAmountFormat, Job(current.Job), current.Level, Amount(current.Exp)), false)
            : (string.Format(culture, Strings.ExpAdviceGoesToFormat, Job(current.Job), current.Level), false);
    }
}
