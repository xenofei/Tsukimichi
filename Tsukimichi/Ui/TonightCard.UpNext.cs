using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Todo;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Up next (plan v7, 1.21.0 P1; spec-1.21 P1): the first block of the Tonight card, one quest and why, in words. The
/// pick is <see cref="UpNextPicker"/>'s: the followed route's next stop, the character's goal (N11), the next main
/// scenario quest while it is Ready or in the journal, the first Ready pin, the closest Ready quest of Next stops, then
/// the level that opens the next story quest. The reason line's hover lists that order.
/// <para>
/// <b>Anatomy.</b> A fixed-height tonal lift inside the card (Full: moonlight over the card's fill with a keyline, a
/// top highlight and a soft shadow straight down; Quiet: a fainter lift; Plain: no fill, a line under it): the eyebrow,
/// the hero medal of the quest's state (48 px at Full, 44 at Quiet, the 20 px glyph at Plain), the name and its level
/// chip on one line (cut, whole on hover), the reason, the giver's plate with "Talk to … · place" or, for a quest in the
/// journal, "Step 3: … · zone" (P2), then the primary travel pill (<see cref="TravelControls.Primary"/>) and Details. The
/// level gate's pill opens the Journal on the Ready quests instead. With nothing to pick the block keeps its size and
/// says so.
/// </para>
/// <para>
/// <b>Motion.</b> When the pick changes, the block's words, moon and plate cross-fade in place over
/// <see cref="MotionTokens.Select"/>; never at Plain or under Reduce motion (<see cref="Motion.Enabled"/>). The block
/// never changes size or place. Its strings are built when an input changes, never per frame.
/// </para>
/// </summary>
public sealed partial class TonightCard
{
    private const uint UpNextTag = 0x5550_4E58; // "UPNX"

    /// <summary>The roster's goals and states (1.21.0 N11, rule 2); set by the plugin. Null leaves the goal out.</summary>
    public RosterSource? Roster { get; set; }

    /// <summary>The followed route (rule 1); set by the plugin. Null leaves the route out.</summary>
    public ActiveRouteService? Routes { get; set; }

    private UpNextKey upNextKey;
    private UpNextView? upNext;
    private string upNextNothing = string.Empty;
    private string upNextOrder = string.Empty;
    private string upNextMsqLine = string.Empty;

    private readonly record struct UpNextKey(int Version, int Pins, int Route, int Stops, int Roster, int Language, int Spoilers, CatalogBundle? Bundle);

    /// <summary>What the block shows, built when an input changes.</summary>
    private sealed record UpNextView(
        QuestRecord Quest,
        UpNextRule Rule,
        string Name,
        bool NameMasked,
        string Level,
        string Reason,
        string Place,
        QuestState State,
        byte ReadyOnJob,
        StepView? Step);

    /// <summary>The rule Up next picked by, for the main scenario line's swap; null with no pick.</summary>
    private UpNextRule? UpNextRuleShown => upNext?.Rule;

    /// <summary>The quest Up next shows; 0 with none.</summary>
    private uint UpNextRowId => upNext?.Quest.RowId ?? 0;

    private void RefreshUpNext(SessionState session, CatalogBundle bundle)
    {
        var key = new UpNextKey(
            session.Version,
            runner.PinsVersion,
            Routes?.Revision ?? 0,
            Stops?.Revision ?? 0,
            Roster?.Revision ?? 0,
            Localization.Loc.Version,
            session.Spoilers.Fingerprint,
            bundle);
        if (key == upNextKey)
        {
            return;
        }

        upNextKey = key;
        upNext = null;
        upNextNothing = string.Empty;
        upNextMsqLine = string.Empty;
        upNextOrder = string.Join('\n', UpNextPicker.Order.Select(static (rule, i) => (i + 1).ToString(CultureInfo.CurrentCulture) + ". " + RuleWords(rule)));
        if (session.ViewedSnapshot is not { } snapshot || session.ViewedContentId is not { } id)
        {
            return;
        }

        var catalog = bundle.Catalog;
        var states = session.States;
        var route = Routes?.ViewedRoute;
        var routeNext = route is null ? null : Routes!.NextStopQuest(route);
        var goalProgress = Roster?.GoalProgressOf(id);
        var gate = Planning?.MsqGate;
        var pick = UpNextPicker.Pick(
            states,
            routeNext?.RowId,
            goalProgress?.Quests.Select(static q => q.RowId) ?? [],
            MsqProgress.Compute(catalog, states)?.Next?.RowId,
            runner.PinnedInOrder,
            Stops?.Stops.SelectMany(static s => s.Quests).Select(static q => q.Quest.RowId) ?? [],
            gate?.Quest.RowId);

        if (pick is not { } chosen || catalog.GetByRowId(chosen.RowId) is not { } quest)
        {
            var job = bundle.Names.ClassJobAbbreviation(snapshot.CurrentJob);
            var other = GuidancePick.ReadyOnOtherJob(states);
            upNextNothing = other switch
            {
                0 => string.Format(CultureInfo.CurrentCulture, Strings.UpNextNothingFormat, job),
                1 => string.Format(CultureInfo.CurrentCulture, Strings.UpNextNothingOtherOneFormat, job),
                _ => string.Format(CultureInfo.CurrentCulture, Strings.UpNextNothingOtherFormat, job, other),
            };
            return;
        }

        var spoilers = session.Spoilers;
        states.TryGetValue(quest.RowId, out var evaluation);
        var step = Links?.CurrentStep(quest, evaluation, session.IsLive, session.IsLive ? null : snapshot.Name, id);
        var reason = chosen.Rule switch
        {
            UpNextRule.Route => RouteReason(bundle, spoilers, route!),
            UpNextRule.Goal => Roster?.Settings.Goal(id) is { } goal
                ? string.Format(CultureInfo.CurrentCulture, Strings.UpNextGoalFormat, GoalText.Phrase(goal, Roster, e => bundle.Names.Expansion(e)), goalProgress?.Left ?? 0)
                : RuleWords(UpNextRule.Goal),
            UpNextRule.MainScenario => evaluation?.State == QuestState.Accepted ? Strings.UpNextMsqInJournal : Strings.UpNextMsq,
            UpNextRule.LevelGate when gate is not null => string.Format(CultureInfo.CurrentCulture, Strings.UpNextGateFormat, gate.Level, bundle.Names.ClassJobAbbreviation(gate.Job), gate.JobLevel),
            _ => RuleWords(chosen.Rule),
        };

        string place;
        if (step is not null)
        {
            var objective = step.Objective.Length > 0 ? step.Objective : Strings.StepNoObjective;
            place = string.Format(CultureInfo.CurrentCulture, Strings.UpNextStepFormat, step.Step.Step, objective);
            if (step.Zone.Length > 0)
            {
                place += Strings.UpNextSeparator + step.Zone;
            }
        }
        else if (chosen.Rule == UpNextRule.LevelGate)
        {
            place = Strings.UpNextGateHint;
        }
        else
        {
            place = string.Format(CultureInfo.CurrentCulture, Strings.UpNextTalkFormat, GiverPortraits.Name(quest, spoilers));
            if (GiverPortraits.Place(quest, spoilers) is { Length: > 0 } where)
            {
                place += Strings.UpNextSeparator + where;
            }
        }

        upNextMsqLine = chosen.Rule == UpNextRule.MainScenario ? MsqCatchUpLine(session, bundle) : string.Empty;
        upNext = new UpNextView(
            quest,
            chosen.Rule,
            spoilers.DisplayName(quest),
            spoilers.IsMasked(quest),
            string.Format(CultureInfo.CurrentCulture, Strings.UpNextLevelFormat, quest.DisplayLevel),
            reason,
            place,
            evaluation?.State ?? QuestState.Unknown,
            evaluation?.ReadyOnJob ?? 0,
            step);
    }

    /// <summary>"Your route to Flying in Thavnair · 8 stops left"; a target past the story point in the shield's words.</summary>
    private string RouteReason(CatalogBundle bundle, SpoilerMask spoilers, Core.Route.UnlockRoute route)
    {
        var saved = Routes?.Saved;
        string label;
        if (saved is { FlyingTerritory: > 0 } && Links is { } links)
        {
            // The zone through the shield ("a zone ahead"), never its name past the story point.
            label = string.Format(CultureInfo.CurrentCulture, Strings.UpNextFlyingInFormat, links.TerritoryNameOf(saved.FlyingTerritory));
        }
        else
        {
            label = spoilers.MaskNamesIn(Routes?.Label ?? string.Empty, bundle.Catalog, saved?.QuestRowIds ?? []);
        }

        var stops = route.Steps.Count;
        return stops == 1
            ? string.Format(CultureInfo.CurrentCulture, Strings.UpNextRouteOneFormat, label)
            : string.Format(CultureInfo.CurrentCulture, Strings.UpNextRouteFormat, label, stops);
    }

    /// <summary>The rule in words, as the hover lists it and as a reason line falls back to.</summary>
    private static string RuleWords(UpNextRule rule) => rule switch
    {
        UpNextRule.Route => Strings.UpNextRuleRoute,
        UpNextRule.Goal => Strings.UpNextRuleGoal,
        UpNextRule.MainScenario => Strings.UpNextRuleMsq,
        UpNextRule.Pinned => Strings.UpNextRulePinned,
        UpNextRule.ClosestStop => Strings.UpNextRuleClosest,
        _ => Strings.UpNextRuleGate,
    };

    /// <summary>The block, fixed in height at the Decoration level; then the card's own lines under it.</summary>
    private void DrawUpNext(SessionState session, CatalogBundle bundle)
    {
        RefreshUpNext(session, bundle);
        var flair = Theme.Flair;
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        var light = Theme.IsLight;
        var highContrast = Theme.Glyphs.HighContrast;
        var origin = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, Chrome.RoomX());
        var pad = flair == Flair.Plain ? 0f : UiMetrics.Px(flair == Flair.Full ? 12f : 10f);
        var medal = MathF.Round(UiMetrics.Px(flair switch { Flair.Full => 48f, Flair.Quiet => 44f, _ => 20f }));
        var body = ImGui.GetTextLineHeight();
        float titleLine;
        using (TitleRole(flair, "Ag"))
        {
            titleLine = ImGui.GetTextLineHeight();
        }

        float eyebrowLine;
        using (EyebrowRole(flair))
        {
            eyebrowLine = ImGui.GetTextLineHeight();
        }

        var gap = UiMetrics.Px(6f);
        var plate = MathF.Round(UiMetrics.Px(flair == Flair.Plain ? 18f : 20f));
        var textColumn = titleLine + body + MathF.Max(body, plate) + (gap * 0.5f);
        var pill = Chrome.PillHeight(PillLayout.Row);
        var height = MathF.Round(pad + eyebrowLine + gap + MathF.Max(medal, textColumn) + gap + pill + pad);
        var min = origin;
        var max = origin + new Vector2(width, height);
        var rounding = UiMetrics.Px(8f);

        // The lift: one step lighter than the card it sits in (a tonal lift, not a framed card), or a line at Plain.
        if (flair == Flair.Plain)
        {
            dl.AddLine(new Vector2(min.X, max.Y - 0.5f), new Vector2(max.X, max.Y - 0.5f), Theme.U32(s.Line), UiMetrics.Hairline);
        }
        else
        {
            if (flair == Flair.Full && !highContrast)
            {
                Ornament.DropShadow(dl, min, max, rounding, UiMetrics.Px(light ? 4f : 2f), UiMetrics.Px(light ? 10f : 5f), light ? 0.10f : 0.28f);
            }

            var top = light ? Vector4.One : Theme.WithAlphaVector(s.Text, flair == Flair.Full ? 0.085f : 0.055f);
            var foot = light ? new Vector4(0.957f, 0.965f, 0.980f, 1f) : Theme.WithAlphaVector(s.Text, flair == Flair.Full ? 0.05f : 0.055f);
            dl.AddRectFilledMultiColor(min + new Vector2(rounding * 0.3f, 0f), max - new Vector2(rounding * 0.3f, 0f), Theme.U32(top), Theme.U32(top), Theme.U32(foot), Theme.U32(foot));
            dl.AddRectFilled(min, max, Theme.U32(Vector4.Lerp(top, foot, 0.5f)), rounding);
            var keyline = light ? s.Line : Theme.WithAlphaVector(s.Text, 0.10f);
            dl.AddRect(min, max, Theme.U32(keyline), rounding, ImDrawFlags.None, highContrast ? UiMetrics.Px(1.5f) : UiMetrics.Hairline);
            if (flair == Flair.Full && !light)
            {
                dl.AddLine(new Vector2(min.X + rounding, min.Y + 0.5f), new Vector2(max.X - rounding, min.Y + 0.5f), Theme.U32(new Vector4(1f, 0.941f, 0.745f, 0.12f)), UiMetrics.Hairline);
            }
        }

        // The words, moon and plate cross-fade in place when the pick changes.
        var firstVertex = dl.VtxBuffer.Size;
        var progress = Motion.Changed(Motion.Key(UpNextTag, 0), UpNextRowId, MotionTokens.Select);
        var alpha = progress < 0f ? 1f : Math.Clamp(progress, 0f, 1f);

        var left = min.X + pad;
        var right = max.X - pad;
        var y = min.Y + pad;
        using (EyebrowRole(flair))
        {
            var eyebrow = flair == Flair.Full ? SectionHeading.Label(Strings.UpNextEyebrow) : Strings.UpNextEyebrow;
            dl.AddText(new Vector2(left, y), Theme.U32(flair == Flair.Full ? Theme.OrnamentLight : s.TextSecondary), eyebrow);
        }

        y += eyebrowLine + gap;
        if (upNext is not { } view)
        {
            ImGui.SetCursorScreenPos(new Vector2(left, y));
            DrawUpNextNothing(right - left);
        }
        else
        {
            DrawUpNextQuest(session, view, dl, left, right, y, medal, plate, titleLine, body, gap);
            ImGui.SetCursorScreenPos(new Vector2(left, max.Y - pad - pill));
            DrawUpNextActions(session, bundle, view, right);
        }

        if (alpha < 1f)
        {
            Chrome.FadeVertices(dl, firstVertex, alpha);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawUpNextQuest(SessionState session, UpNextView view, ImDrawListPtr dl, float left, float right, float y, float medal, float plate, float titleLine, float body, float gap)
    {
        var s = Theme.Surface;
        var flair = Theme.Flair;
        var center = new Vector2(left + (medal * 0.5f), y + (medal * 0.5f));
        MoonWax.Draw(dl, center, medal * 0.494f, view.State, view.Quest.RowId, view.ReadyOnJob);
        ImGui.SetCursorScreenPos(new Vector2(left, y));
        ImGui.InvisibleButton("##upNextMoon", new Vector2(medal, medal));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.StateTooltip(view.State));
        }

        var textLeft = left + medal + UiMetrics.Px(flair == Flair.Plain ? 8f : 12f);
        var room = MathF.Max(1f, right - textLeft);

        // The name (Title role at Full) and its level chip, one line; cut, the whole name on hover.
        var chip = Chrome.PillSize(view.Level);
        var nameRoom = MathF.Max(1f, room - chip.X - UiMetrics.Px(8f));
        bool cut;
        float nameWidth;
        using (TitleRole(flair, view.Name))
        {
            nameWidth = MathF.Min(ImGui.CalcTextSize(view.Name).X, nameRoom);
            var ink = view.NameMasked ? s.TextSecondary : s.Text;
            cut = Chrome.EllipsisTextAt(dl, new Vector2(textLeft, y), nameRoom, view.Name, Theme.U32(ink));
        }

        if (cut && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(new Vector2(textLeft, y), new Vector2(textLeft + nameRoom, y + titleLine)))
        {
            UiMetrics.Tooltip(view.Name);
        }

        using (Typography.Caption())
        {
            var chipMin = new Vector2(textLeft + nameWidth + UiMetrics.Px(8f), y + MathF.Round((titleLine - chip.Y) * 0.5f));
            Chrome.PillAt(dl, chipMin, chip, view.Level, Theme.U32(s.Raised), Theme.U32(s.Line), Theme.U32(s.TextSecondary));
        }

        // The reason, one line in Text; its hover is the order in words.
        var reasonY = y + titleLine;
        var reasonCut = Chrome.EllipsisTextAt(dl, new Vector2(textLeft, reasonY), room, view.Reason, Theme.U32(s.Text));
        ImGui.SetCursorScreenPos(new Vector2(textLeft, reasonY));
        ImGui.InvisibleButton("##upNextWhy", new Vector2(room, body));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(reasonCut ? view.Reason + "\n\n" + Strings.UpNextWhyTitle : Strings.UpNextWhyTitle, Strings.UpNextWhyLead + "\n" + upNextOrder);
        }

        // The giver's plate and "Talk to … · place", or the step for a quest in the journal (P2).
        var placeY = reasonY + body + (gap * 0.5f);
        var placeLeft = textLeft;
        if (view.Rule != UpNextRule.LevelGate)
        {
            var request = GiverPortraits.For(view.Quest, session.Spoilers);
            var plateMin = new Vector2(textLeft, placeY + MathF.Round((MathF.Max(body, plate) - plate) * 0.5f));
            Chrome.Portrait(dl, plateMin, plate, request);
            placeLeft += plate + UiMetrics.Px(6f);
            if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(plateMin, plateMin + new Vector2(plate)))
            {
                Chrome.PortraitTooltip(request, GiverPortraits.Name(view.Quest, session.Spoilers), GiverPortraits.Place(view.Quest, session.Spoilers));
            }
        }

        var placeRoom = MathF.Max(1f, right - placeLeft);
        var textY = placeY + MathF.Round((MathF.Max(body, plate) - body) * 0.5f);
        var placeCut = Chrome.EllipsisTextAt(dl, new Vector2(placeLeft, textY), placeRoom, view.Place, Theme.U32(s.TextSecondary));
        if (placeCut && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(new Vector2(placeLeft, textY), new Vector2(right, textY + body)))
        {
            UiMetrics.Tooltip(view.Place);
        }
    }

    /// <summary>The primary travel pill (or the level gate's Show Ready quests) and Details, right-aligned.</summary>
    private void DrawUpNextActions(SessionState session, CatalogBundle bundle, UpNextView view, float right)
    {
        _ = session;
        _ = bundle;
        var start = ImGui.GetCursorScreenPos();
        if (view.Rule == UpNextRule.LevelGate)
        {
            if (Chrome.ActionPill("##upNextGate", PillIcon.JournalBook, Strings.UpNextGateShow, PillTone.Primary, true, Strings.TonightShowReadyTooltip, PillLayout.Row))
            {
                ShowReadyInJournal();
            }
        }
        else if (Links is { } links)
        {
            var target = links.TravelTarget(view.Quest, view.Step);
            var pill = TravelControls.Primary(links, target, !ReferenceEquals(target, view.Quest));
            if (pill.Kind != PrimaryTravel.None
                && Chrome.ActionPill("##upNextGo", pill.Icon, pill.Label, pill.Kind == PrimaryTravel.Stop ? PillTone.Danger : PillTone.Primary, true, null, PillLayout.Row))
            {
                TravelControls.Run(links, target, in pill);
            }

            if (pill.Kind == PrimaryTravel.Teleport && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(links.TeleportTooltip(target));
            }
        }

        var details = Chrome.ActionChipWidth(Strings.UpNextDetails);
        var pillHeight = Chrome.PillHeight(PillLayout.Row);
        ImGui.SetCursorScreenPos(new Vector2(right - details, start.Y + MathF.Round((pillHeight - Chrome.ChipHeightPx()) * 0.5f)));
        if (Chrome.ActionChip("##upNextDetails", Strings.UpNextDetails))
        {
            ui.Reveal(view.Quest);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.UpNextDetailsTooltip);
        }
    }

    /// <summary>"Nothing is Ready on DRK. 4 quests are Ready on another job ›": the link opens the Journal on them.</summary>
    private void DrawUpNextNothing(float room)
    {
        var s = Theme.Surface;
        TextFlow.Wrapped(upNextNothing, room, Theme.U32(s.TextSecondary));
        if (ImGui.IsItemHovered() && upNextNothing.Contains('›', StringComparison.Ordinal))
        {
            UiMetrics.Tooltip(Strings.UpNextNothingTooltip);
            if (ImGui.IsItemClicked())
            {
                ShowInJournal(QuestStateMask.ReadyOnOtherJob);
            }
        }
    }

    /// <summary>The Journal on the Ready quests, as "Show them" opens it.</summary>
    private void ShowReadyInJournal() => ShowInJournal(QuestStateMask.Ready);

    private void ShowInJournal(QuestStateMask mask)
    {
        ui.Tab = NavTab.Journal;
        ui.Scope = QuestScope.None;
        var includeUnlisted = ui.Filters.IncludeUnlisted;
        ui.Filters.Reset();
        ui.Filters.IncludeUnlisted = includeUnlisted;
        ui.Filters.StateMask = mask;
        ui.SearchText = string.Empty;
        ui.SelectedRowId = null;
        filtersChanged();
    }

    private static Typography.Scope TitleRole(Flair flair, string text) => flair == Flair.Full ? Typography.Title(text) : Typography.Lead();

    private static Typography.Scope EyebrowRole(Flair flair) => flair == Flair.Full ? Typography.Eyebrow(SectionHeading.Label(Strings.UpNextEyebrow)) : Typography.Caption();

    /// <summary>
    /// The main scenario line's text when Up next already shows the next main scenario quest (spec-1.21 decision 2):
    /// the catch-up instead of the name, "91 quests to the latest story, Lv 95–100"; null when the line keeps the name.
    /// </summary>
    // The frame the card last drew: a gap means it appears again (the selection was cleared), and it fades in.
    private int lastDrawFrame = -10;

    /// <summary>
    /// Where this frame's drawing starts in the card's draw list; a card appearing again (back from a quest: the Tonight
    /// button, Esc, a second click) starts its fade-in over <see cref="MotionTokens.Select"/> (none under Reduce motion).
    /// </summary>
    private int BeginAppear()
    {
        var frame = ImGui.GetFrameCount();
        if (frame - lastDrawFrame > 1)
        {
            Motion.Trigger(Motion.Key(UpNextTag, 1));
        }

        lastDrawFrame = frame;
        return ImGui.GetWindowDrawList().VtxBuffer.Size;
    }

    /// <summary>Fades what the card drew this frame while it appears.</summary>
    private static void EndAppear(int from)
    {
        var progress = Motion.Pulse(Motion.Key(UpNextTag, 1), MotionTokens.Select);
        if (progress is >= 0f and < 1f)
        {
            Chrome.FadeVertices(ImGui.GetWindowDrawList(), from, progress);
        }
    }

    private TodoRow? msqSwapFrom;
    private string msqSwapLine = string.Empty;
    private TodoRow? msqSwap;

    /// <summary>The main scenario row as the card shows it: the catch-up in place of the name Up next already shows.</summary>
    private TodoRow MsqRowShown(TodoRow row)
    {
        if (MsqCatchUp(row.RowId) is not { } line)
        {
            return row;
        }

        if (!ReferenceEquals(msqSwapFrom, row) || !string.Equals(msqSwapLine, line, StringComparison.Ordinal) || msqSwap is null)
        {
            msqSwapFrom = row;
            msqSwapLine = line;
            msqSwap = row with { Name = line, Hint = string.Empty };
        }

        return msqSwap;
    }

    private string? MsqCatchUp(uint rowId) =>
        UpNextRuleShown == UpNextRule.MainScenario && UpNextRowId == rowId && upNextMsqLine.Length > 0 ? upNextMsqLine : null;

    /// <summary>The catch-up words for the main scenario line, built with the pick.</summary>
    private static string MsqCatchUpLine(SessionState session, CatalogBundle bundle)
    {
        if (MsqLeft.For(bundle.Catalog, session.States) is not { } left)
        {
            return string.Empty;
        }

        return left.LeftToLatest == 1
            ? string.Format(CultureInfo.CurrentCulture, Strings.UpNextMsqCatchUpOneFormat, left.MinLevel)
            : string.Format(CultureInfo.CurrentCulture, Strings.UpNextMsqCatchUpFormat, left.LeftToLatest, left.MinLevel, left.MaxLevel);
    }
}
