using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Journal;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Rewards;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// "Right answers" (feature plan v7, 1.19.0) in the detail pane. Under the unmet Job requirement (spec-1.19 C8):
/// "Switch gearset: Culinarian" for a quest that requires one class or job the current one is not, an explicit pill
/// running the game's gearset change, or "No Culinarian gearset saved". Under the EXP amount, with the job's icon:
/// "Hand in on DRG Lv 56: 50,700 EXP (5% of a level) · your SGE is capped, 0" (C8). What the game's own offers say (C1)
/// and the full journal (C9) are in the hero and the disagreement card (DetailPane.GameAnswers.cs). Each line is rebuilt
/// when the quest, the session, the offers or the language change, so drawing allocates nothing.
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
    private byte expAdviceJob;
    private bool gearsetNeeded;
    private byte gearsetJob;
    private SessionState? rightSession;
    private QuestRecord? rightQuest;

    // "Switch gearset: Culinarian" or "No Culinarian gearset saved", formatted once per gearset and language.
    private (GearsetInfo? Gearset, byte Job, int Language) gearsetLabelFor = (null, 0, -1);
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
        rightSession = session;
        rightQuest = quest;

        session.States.TryGetValue(quest.RowId, out var evaluation);
        expAdviceLine = string.Empty;
        expAdviceWarns = false;
        expAdviceJob = 0;
        gearsetNeeded = false;
        gearsetJob = 0;
        if (session.ViewedSnapshot is not { } snapshot || session.Bundle is not { } bundle || evaluation is null)
        {
            return;
        }

        gearsetNeeded = session.IsLive && GearsetChoice.Needed(quest, snapshot, session.Context, evaluation.State);
        gearsetJob = GearsetChoice.PinnedJob(quest, session.Context) ?? 0;
        if (evaluation.State is not (QuestState.Completed or QuestState.Foreclosed or QuestState.DoneThisCycle)
            && ExpAdvisor.Advise(quest, snapshot, bundle.ExpTable, session.Context, IsLimitedJob(bundle)) is { } advice)
        {
            (expAdviceLine, expAdviceWarns, expAdviceJob) = AdviceText(advice, session.Names, snapshot.LevelCap, bundle.ExpTable);
        }
    }

    /// <summary>
    /// Refreshes the C8 lines for the quest on view. Switch gearset draws under the Job requirement; what the game's
    /// offers say and the full journal are in the hero and the disagreement card (spec-1.19 C1 and C9,
    /// DetailPane.GameAnswers.cs).
    /// </summary>
    private void DrawRightAnswers(SessionState session, QuestRecord quest)
    {
        RefreshRightAnswers(session, quest);
    }

    /// <summary>
    /// Under the unmet Job requirement at (<paramref name="left"/>, <paramref name="top"/>), spec-1.19 C8: the small
    /// pill "Switch gearset: Culinarian" with the job's icon, only for a quest that requires one class or job the
    /// logged-in character is not on (<see cref="GearsetChoice.Needed"/>), equipping the first gearset of that job
    /// (<see cref="GearsetChoice.First"/>); disabled, saying why, while the game would refuse (combat, a duty, a cast…).
    /// Without such a gearset, the words "No Culinarian gearset saved". Allowed at every automation level: it is the
    /// game's own UI action. Returns the new bottom.
    /// </summary>
    private float DrawGearsetUnderJob(float left, float top)
    {
        if (!gearsetNeeded || gearsetJob == 0 || Gearsets is not { } switcher || rightRowId != model.RowId
            || rightSession is not { ViewedSnapshot: { } snapshot } session || rightQuest is not { } quest)
        {
            return top;
        }

        var gearset = GearsetChoice.First(switcher.Gearsets(), gearsetJob, quest, snapshot, session.Context);
        var job = gearset?.Job ?? gearsetJob;
        if (gearsetLabelFor != (gearset, job, Localization.Loc.Version))
        {
            gearsetLabelFor = (gearset, job, Localization.Loc.Version);
            var name = model.Bundle?.Names.ClassJob(job) is { Length: > 0 } full ? full : session.Names.JobAbbreviation(job);
            gearsetLabel = string.Format(CultureInfo.CurrentCulture, gearset is null ? Strings.GearsetNoneFormat : Strings.GearsetSwitchJobFormat, name);
        }

        ImGui.SetCursorScreenPos(new Vector2(left, top + UiMetrics.Px(4f)));
        if (gearset is not { } chosen)
        {
            TextFlow.Wrapped(gearsetLabel, RoomTo(cardRight), Theme.U32(Theme.Surface.TextSecondary));
            return ImGui.GetItemRectMax().Y;
        }

        var blocker = switcher.Blocker();
        var icon = PillIcon.GameOr(GameIconRef.Tile(ActionIcons.Job(chosen.Job)), GearsetIcon);
        if (Chrome.ActionPill("##gearsetSwitch", icon, gearsetLabel, PillTone.Normal, blocker is null, size: PillLayout.Row))
        {
            switcher.Switch(chosen);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            var tooltip = string.Format(CultureInfo.CurrentCulture, Strings.GearsetSwitchTooltipFormat, chosen.Id + 1, chosen.Name, chosen.ItemLevel);
            UiMetrics.Tooltip(blocker is null ? tooltip : tooltip + "\n" + string.Format(CultureInfo.CurrentCulture, Strings.GearsetSwitchBlockedFormat, blocker));
        }

        return ImGui.GetItemRectMax().Y;
    }

    /// <summary>
    /// Under the EXP amount (spec-1.19 C8): the job's icon (a 14 px tile), then "Hand in on DRG Lv 56: 50,700 EXP (5% of
    /// a level)" and, when another job would get nothing or less, why; in the unmet tone when the EXP would be lost or
    /// another job gets far more.
    /// </summary>
    private void DrawExpAdvice(SessionState session, QuestRecord quest)
    {
        RefreshRightAnswers(session, quest);
        if (expAdviceLine.Length == 0)
        {
            return;
        }

        var start = ImGui.GetCursorScreenPos();
        if (ActionIcons.Job(expAdviceJob) is var iconId and > 0)
        {
            var side = MathF.Round(UiMetrics.Px(ExpJobIconLogical));
            var min = new Vector2(start.X, start.Y + MathF.Round((ImGui.GetTextLineHeight() - side) * 0.5f));
            Chrome.DrawPillIcon(ImGui.GetWindowDrawList(), GameIconRef.Tile(iconId), min, side, Theme.U32(Theme.Surface.TextSecondary), enabled: true);
            ImGui.SetCursorScreenPos(new Vector2(start.X + side + UiMetrics.Px(5f), start.Y));
        }

        TextFlow.Wrapped(expAdviceLine, RoomTo(cardRight), Theme.U32(expAdviceWarns ? Theme.DangerText : Theme.Surface.TextSecondary));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ExpAdviceTooltip);
        }
    }

    /// <summary>The EXP line's job icon, logical px (spec-1.19 C8: a 14 px tile).</summary>
    private const float ExpJobIconLogical = 14f;

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

    /// <summary>
    /// The advice as one line, whether it warns, and the job it says to hand in on (whose icon leads it). The head is
    /// "Hand in on DRG Lv 56: 50,700 EXP (5% of a level)" for the job that should get it (the share left out where
    /// <c>ParamGrow</c> has no next level, the level is unknown or the EXP is 0); after it, the current job when it is
    /// capped or gets far less, else another capped job that could have taken it ("your SGE is capped, 0").
    /// </summary>
    private static (string Text, bool Warns, byte Job) AdviceText(ExpAdvice advice, BlockerNames names, byte levelCap, QuestExpTable table)
    {
        var culture = CultureInfo.CurrentCulture;
        string Job(byte job) => names.JobAbbreviation(job) is { Length: > 0 } abbreviation ? abbreviation : job.ToString(culture);
        string Amount(ulong exp) => exp.ToString("N0", culture);
        string HandIn(JobExp share)
        {
            var head = share.Level > 0
                ? string.Format(culture, Strings.ExpLineFormat, Job(share.Job), share.Level, Amount(share.Exp))
                : string.Format(culture, Strings.ExpLineNoLevelFormat, Job(share.Job), Amount(share.Exp));
            return QuestExp.ShareOfLevel(share.Exp, share.Level, table) is not { } part
                ? head
                : head + " " + (part.UnderOne ? Strings.ExpShareUnderOne : string.Format(culture, Strings.ExpShareFormat, part.Percent));
        }

        var current = advice.Current;
        switch (advice.Warning)
        {
            case ExpWarning.NeedsJob:
                return advice.Best is { } needed
                    ? (HandIn(needed), false, needed.Job)
                    : (Strings.ExpAdviceNeedsJobNone, false, (byte)0);

            case ExpWarning.Capped when advice.Best is { } best:
                return (HandIn(best) + MsqText.Separator + string.Format(culture, Strings.ExpCappedClauseFormat, Job(current.Job)), true, best.Job);

            case ExpWarning.LessThanBest when advice.Best is { } more:
                return (HandIn(more) + MsqText.Separator + string.Format(culture, Strings.ExpLessClauseFormat, Job(current.Job), current.Level, Amount(current.Exp)), true, more.Job);
        }

        if (current.Exp == 0 && levelCap > 0 && current.Level >= levelCap)
        {
            return (string.Format(culture, Strings.ExpAdviceAllCappedFormat, Job(current.Job)), true, current.Job);
        }

        var line = HandIn(current);
        if (advice.Capped is { } capped)
        {
            line += MsqText.Separator + string.Format(culture, Strings.ExpCappedClauseFormat, Job(capped.Job));
        }

        return (line, false, current.Job);
    }
}
