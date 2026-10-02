using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The unlock route (feature plan v3 P6): for a job, duty, system, Moonlit reward or quest, the quests the viewed
/// character still has to do, in order (<see cref="UnlockRoute"/>), with a moon and level per quest, MSQ milestone
/// separators, level gates where the route asks for a new level, and the other ways into an Any join. The route uses
/// the viewed character's states, so a stored alt gets its own route; it is rebuilt when the session changes, so
/// quests drop off as they are completed. "Copy route" puts a Markdown list on the clipboard (names through the
/// spoiler shield, no character); "Pin all" is a hold-to-confirm button that pins every step through
/// <see cref="RoutePins.PinAll"/>, followed by an Undo line. Opened by <see cref="UiState.OpenRoute"/> from the detail
/// pane's action bar, a Moonlit row's menu, the Characters job rows, My blues, the Duty Finder panel and the todo
/// overlay's pins. Every string is composed when the route is rebuilt, and the list draws only its visible lines, so
/// a thousand-quest route costs nothing per frame.
/// <para>
/// 1.6.0 (R6 A, C3 C): "Follow this route" makes it the route the todo overlay shows (<see cref="ActiveRouteService"/>),
/// "Flag next stop" opens the map on the next step's giver, and every step says where its giver is: the aetheryte
/// nearest it (attuned or not), with Flag, Teleport and Walk (Go to giver in its right-click menu). Consecutive steps
/// whose givers share that aetheryte are one stop ("3 quests near Camp Dragonhead") with one Teleport; the order is
/// never changed for it. A route to several targets marks each target's milestone on the step that reaches it.
/// </para>
/// <para>
/// Questionable (feature plan v5, 1.6.0): "Send to Questionable" beside Pin all hands the route's steps, in route
/// order, to Questionable's priority list (<see cref="QuestionableActions"/>), and each step shows "Q #3" when it is on
/// that list or "Q no path" when Questionable cannot do it.
/// </para>
/// </summary>
public sealed class RouteWindow : Window
{
    private const double NoteSeconds = 8.0;
    private const float IndentLogical = 22f;

    private static readonly string RouteIcon = FontAwesomeIcon.MapSigns.ToIconString();
    private static readonly string PinAllLabelSuffix = Chrome.HoldIdSuffix;

    /// <summary>The widest level label ("Lv 100"), which sets where a step's text starts.</summary>
    private static readonly Localization.LocText LevelSample = new(static () => string.Format(CultureInfo.CurrentCulture, Strings.RouteLevelFormat, 100));

    private readonly SessionState session;
    private readonly Action<QuestRecord> showQuest;
    private readonly PinStore pins;
    private readonly ConfirmGate pinGate = new();
    private readonly GameLinks links;
    private readonly ActiveRouteService routes;

    private RouteTarget? target;
    private int builtVersion = -1;
    private CatalogBundle? builtBundle;
    private RouteTarget? builtTarget;
    private View view = View.Empty;
    private Theme.StyleScope nightChrome;

    private readonly QueryRunner runner;

    // What the last Pin all added and for whom; dropped with its note when the viewed character changes.
    private RoutePinBatch undo = RoutePinBatch.Empty;
    private string note = string.Empty;
    private double noteUntil = -1.0;

    // "Pin all (N)" counts the steps not pinned yet; rebuilt when the view or the pins change.
    private View? pinLabelView;
    private int pinLabelPins = -1;
    private string pinLabel = string.Empty;

    /// <param name="runner">The pin list "Pin all" adds to (the viewed character's).</param>
    /// <param name="links">Map flags and Lifestream teleports for the steps.</param>
    /// <param name="routes">The followed route ("Follow this route").</param>
    /// <param name="showQuest">Brings the main window forward and shows a quest in the Journal.</param>
    public RouteWindow(SessionState session, QueryRunner runner, GameLinks links, ActiveRouteService routes, Action<QuestRecord> showQuest)
        : base(Strings.RouteWindowTitle)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.routes = routes ?? throw new ArgumentNullException(nameof(routes));
        pins = new PinStore(runner);
        this.showQuest = showQuest ?? throw new ArgumentNullException(nameof(showQuest));
        Size = new Vector2(600f, 620f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(MinWidthLogical, MinHeightLogical) };
    }

    /// <summary>Logical minimum size of the window, scaled by the UI scale each frame.</summary>
    private const float MinWidthLogical = 380f;
    private const float MinHeightLogical = 260f;

    /// <summary>The host name of this window's Questionable confirmations.</summary>
    private const string QuestionableHost = "route";

    /// <summary>The shared Questionable hand-offs (1.6.0); null hides Send to Questionable and the step marks.</summary>
    public QuestionableActions? Questionable { get; set; }

    /// <summary>Opens the window on the route to <paramref name="routeTarget"/> and brings it to the front.</summary>
    public void Show(RouteTarget routeTarget)
    {
        target = routeTarget ?? throw new ArgumentNullException(nameof(routeTarget));
        builtVersion = -1;
        pinGate.Cancel();
        undo = RoutePinBatch.Empty;
        noteUntil = -1.0;
        Questionable?.Ipc.MarkListStale();
        IsOpen = true;
        BringToFront();
    }

    /// <summary>Opens the window on the followed route; false (nothing opened) when none is followed.</summary>
    public bool ShowFollowed()
    {
        if (routes.Saved is not { } saved)
        {
            return false;
        }

        Show(saved.ToTarget());
        return true;
    }

    public override void PreDraw()
    {
        // The window is its own top level, so its minimum follows the UI scale like Nearby's.
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(MinWidthLogical, MinHeightLogical) * UiMetrics.FontScale,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        nightChrome = Theme.PushNightWindow();
    }

    public override void PostDraw()
    {
        nightChrome.Dispose();
        nightChrome = default;
    }

    public override void OnClose()
    {
        pinGate.Cancel();
    }

    public override void Draw()
    {
        UiMetrics.ApplyFontScale();
        try
        {
            DrawContent();
            Questionable?.DrawModals(QuestionableHost);
        }
        finally
        {
            ImGui.SetWindowFontScale(1f);
        }
    }

    private void DrawContent()
    {
        if (target is null)
        {
            return;
        }

        if (session.Bundle is not { } bundle)
        {
            ImGui.TextDisabled(Strings.RouteLoading);
            return;
        }

        Refresh(bundle, target);
        var v = view;

        ImGui.PushFont(UiBuilder.IconFont);
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextUnformatted(RouteIcon);
        }

        ImGui.PopFont();
        ImGui.SameLine();
        ImGui.TextUnformatted(v.Title);
        ImGui.TextDisabled(v.Caption);

        switch (v.Route.Outcome)
        {
            case RouteOutcome.AlreadyDone:
                EmptyState.DrawWithAction(Strings.RouteAlreadyDoneHeading, Strings.RouteAlreadyDoneBody, null, moon: QuestState.Completed);
                return;
            case RouteOutcome.NoQuest:
                EmptyState.DrawWithAction(Strings.RouteNoQuestHeading, Strings.RouteNoQuestBody, null);
                return;
            case RouteOutcome.LockedOut when v.Route.Steps.Count == 0:
                // A route to several targets whose every part left is locked out: nothing to do, and never complete.
                EmptyState.DrawWithAction(Strings.RouteLockedOutHeading, Strings.RouteLockedOutBody, null);
                return;
        }

        ImGui.TextUnformatted(v.Summary);
        if (v.AlsoUnlockedBy.Length > 0)
        {
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextWrapped(v.AlsoUnlockedBy);
            }
        }

        if (v.Route.Outcome == RouteOutcome.LockedOut)
        {
            using (Theme.PushText(Theme.EclipseText))
            {
                ImGui.TextWrapped(Strings.RouteLockedOut);
            }
        }

        DrawActions(bundle, v);
        DrawFollowActions(v);
        Chrome.Hairline();
        DrawLines(bundle, v);
    }

    private void DrawActions(CatalogBundle bundle, View v)
    {
        // The pins follow the viewed character; a Pin all made for someone else can no longer be undone from here.
        runner.SyncPins();
        if (undo.Added.Count > 0 && !RoutePins.CanUndo(undo, pins))
        {
            undo = RoutePinBatch.Empty;
            noteUntil = -1.0;
        }

        if (ImGui.Button(Strings.RouteCopy))
        {
            ImGui.SetClipboardText(RouteMarkdown.Write(v.Route, bundle.Catalog, session.Spoilers.DisplayName));
            Note(Strings.RouteCopied, RoutePinBatch.Empty);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.RouteCopyTooltip);
        }

        ImGui.SameLine();
        var canPin = pins.CanPin && v.Route.Steps.Count > 0;
        ImGui.BeginDisabled(!canPin);
        var confirmed = Chrome.HoldButton(PinAllLabel(v), pinGate);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(pins.CanPin ? Strings.RoutePinAllTooltip : Strings.RoutePinAllUnavailable);
        }

        if (Questionable is { } questionable)
        {
            Chrome.SameLineOrWrap(QuestionableActions.ButtonWidth);
            questionable.DrawButton(QuestionableHost, "##questionable", v.Route, static route => StepRowIds(route));
        }

        if (confirmed && canPin)
        {
            var added = RoutePins.PinAll(v.Route, pins);
            var count = added.Added.Count;
            Note(
                count == 0 ? Strings.RouteAllPinnedAlready
                : count == 1 ? Strings.RoutePinnedOne
                : string.Format(CultureInfo.CurrentCulture, Strings.RoutePinnedFormat, count),
                added);
        }

        if (noteUntil < 0.0 || ImGui.GetTime() >= noteUntil)
        {
            noteUntil = -1.0;
            return;
        }

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        using (Theme.PushText(Theme.Surface.Text))
        {
            ImGui.TextUnformatted(note);
        }

        if (undo.Added.Count == 0)
        {
            return;
        }

        ImGui.SameLine(0f, 0f);
        ImGui.TextDisabled(Strings.RouteUndoSeparator);
        ImGui.SameLine(0f, 0f);
        if (ImGui.SmallButton(Strings.RouteUndo))
        {
            RoutePins.Undo(undo, pins);
            undo = RoutePinBatch.Empty;
            noteUntil = -1.0;
        }
        else if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.RouteUndoTooltip);
        }
    }

    /// <summary>
    /// The second row (1.6.0): "Follow this route" (or "Stop following" when it is the followed one) and "Flag next
    /// stop". Following needs a character, since the route belongs to the one it was built for.
    /// </summary>
    private void DrawFollowActions(View v)
    {
        var owner = session.ViewedContentId;
        var following = target is not null && routes.Follows(target, owner);
        var canFollow = owner is not null && v.Route.Steps.Count > 0;
        using (ImRaii.Disabled(!following && !canFollow))
        {
            if (ImGui.Button(following ? Strings.RouteStopFollowing : Strings.RouteFollow) && target is not null)
            {
                if (following)
                {
                    routes.Stop();
                }
                else if (owner is { } id)
                {
                    routes.Follow(target, id);
                }
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(following ? Strings.RouteStopFollowingTooltip : owner is null ? Strings.RouteFollowUnavailable : Strings.RouteFollowTooltip);
        }

        var stop = routes.NextStopQuest(v.Route);
        var canFlag = stop is not null && links.CanFlagMap(stop);
        Chrome.SameLineOrWrap(ImGui.CalcTextSize(Strings.RouteFlagNextStop).X + (ImGui.GetStyle().FramePadding.X * 2f));
        using (ImRaii.Disabled(!canFlag))
        {
            if (ImGui.Button(Strings.RouteFlagNextStop))
            {
                routes.FlagNextStop(v.Route);
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(canFlag ? Strings.RouteFlagNextStopTooltip : Strings.RouteFlagNextStopUnavailable);
        }
    }

    private static IEnumerable<uint> StepRowIds(UnlockRoute route)
    {
        foreach (var step in route.Steps)
        {
            yield return step.RowId;
        }
    }

    private void Note(string text, RoutePinBatch added)
    {
        note = text;
        undo = added;
        noteUntil = ImGui.GetTime() + NoteSeconds;
    }

    /// <summary>"Pin all (N)" with N the steps not pinned yet (every step when there is nobody to pin for); composed only when the route or the pins change.</summary>
    private int pinLabelLanguage = -1;

    private string PinAllLabel(View v)
    {
        if (ReferenceEquals(pinLabelView, v) && pinLabelPins == runner.PinsVersion && pinLabelLanguage == Localization.Loc.Version)
        {
            return pinLabel;
        }

        pinLabelLanguage = Localization.Loc.Version;

        var count = pins.CanPin ? RoutePins.CountUnpinned(v.Route, pins) : v.Route.Steps.Count;
        pinLabelView = v;
        pinLabelPins = runner.PinsVersion;
        pinLabel = string.Format(CultureInfo.CurrentCulture, Strings.RoutePinAllFormat, count) + PinAllLabelSuffix;
        return pinLabel;
    }

    /// <summary>
    /// The route's lines, every one the same height, drawn only where visible: a spacer for the lines above the
    /// scroll position, the visible lines, a spacer for the rest.
    /// </summary>
    private void DrawLines(CatalogBundle bundle, View v)
    {
        using var child = ImRaii.Child("##routeLines", new Vector2(-1f, -1f), false);
        if (!child)
        {
            return;
        }

        using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(ImGui.GetStyle().ItemSpacing.X, 0f));
        var line = ImGui.GetTextLineHeight();
        var rowHeight = MathF.Round(MathF.Max(line + UiMetrics.Px(6f), UiMetrics.InlineGlyphSize(line) + UiMetrics.Px(2f)));
        var lines = v.Lines;
        var first = Math.Clamp((int)(ImGui.GetScrollY() / rowHeight), 0, lines.Length);
        var visible = (int)MathF.Ceiling(ImGui.GetWindowHeight() / rowHeight) + 1;
        var last = Math.Min(lines.Length, first + visible);
        if (first > 0)
        {
            ImGui.Dummy(new Vector2(1f, first * rowHeight));
        }

        for (var i = first; i < last; i++)
        {
            using var id = ImRaii.PushId(i);
            DrawLine(bundle, lines[i], rowHeight, line);
        }

        if (last < lines.Length)
        {
            ImGui.Dummy(new Vector2(1f, (lines.Length - last) * rowHeight));
        }
    }

    /// <summary>A small button's width for <paramref name="label"/>.</summary>
    private static float SmallButtonWidth(string label) => ImGui.CalcTextSize(label).X + (ImGui.GetStyle().FramePadding.X * 2f);

    /// <summary>Walk's width, sized for the longer of Walk and Stop so the row does not shift when it turns.</summary>
    private static float WalkButtonWidth() =>
        MathF.Max(SmallButtonWidth(Strings.TravelWalkShort), SmallButtonWidth(Strings.TravelStop));

    /// <summary>
    /// The line's Flag, Teleport and Walk (Stop while the character moves), right-aligned at <paramref name="right"/> on
    /// the row starting at <paramref name="top"/>; returns the x where they begin (the text ends before it). A button
    /// that cannot start says why on hover.
    /// </summary>
    private float DrawStepButtons(QuestRecord quest, bool flag, bool teleport, bool walk, float right, float top, float rowHeight)
    {
        var gap = UiMetrics.Px(4f);
        var width = 0f;
        if (flag)
        {
            width += SmallButtonWidth(Strings.RouteStepFlag);
        }

        if (teleport)
        {
            width += SmallButtonWidth(Strings.RouteStepTeleport) + (flag ? gap : 0f);
        }

        if (walk)
        {
            width += WalkButtonWidth() + (flag || teleport ? gap : 0f);
        }

        if (width <= 0f)
        {
            return right;
        }

        var x = right - width;
        var y = top + ((rowHeight - ImGui.GetTextLineHeight()) * 0.5f);
        if (flag)
        {
            ImGui.SetCursorScreenPos(new Vector2(x, y));
            var canFlag = links.CanFlagMap(quest);
            using (ImRaii.Disabled(!canFlag))
            {
                if (ImGui.SmallButton(Strings.RouteStepFlag))
                {
                    links.FlagMap(quest);
                }
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(canFlag ? Strings.RouteStepFlagTooltip : Strings.RouteStepFlagUnavailable);
            }

            x += SmallButtonWidth(Strings.RouteStepFlag) + gap;
        }

        if (teleport)
        {
            ImGui.SetCursorScreenPos(new Vector2(x, y));
            var canTeleport = links.CanTeleport(quest);
            using (ImRaii.Disabled(!canTeleport))
            {
                if (ImGui.SmallButton(Strings.RouteStepTeleport))
                {
                    links.TeleportToGiver(quest);
                }
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(TeleportTip(quest));
            }

            x += SmallButtonWidth(Strings.RouteStepTeleport) + gap;
        }

        if (walk)
        {
            DrawWalkButton(quest, new Vector2(x, y));
        }

        return right - width - UiMetrics.Px(8f);
    }

    /// <summary>Walk to giver through vnavmesh, or Stop while the character moves; disabled, saying why, when it cannot start.</summary>
    private void DrawWalkButton(QuestRecord quest, Vector2 at)
    {
        ImGui.SetCursorScreenPos(at);
        var walk = links.CheckWalk(quest);
        using (ImRaii.PushId("walk"))
        using (ImRaii.Disabled(!walk.Ready && !walk.Stoppable))
        {
            if (ImGui.SmallButton(walk.Stoppable ? Strings.TravelStop : Strings.TravelWalkShort))
            {
                if (walk.Stoppable)
                {
                    links.StopTravel();
                }
                else
                {
                    links.WalkToGiver(quest);
                }
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(links.WalkTooltip(quest, walk));
        }
    }

    /// <summary>
    /// A step's Teleport tooltip: where it goes with the gil cost and "already here", or why it cannot (no Lifestream,
    /// Lifestream busy, no attuned aetheryte), the same text as every other Teleport (<see cref="GameLinks.TeleportTooltip(QuestRecord)"/>).
    /// </summary>
    private string TeleportTip(QuestRecord quest) => links.TeleportTooltip(quest);

    private void DrawLine(CatalogBundle bundle, Line l, float rowHeight, float line)
    {
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var textY = min.Y + ((rowHeight - line) * 0.5f);
        switch (l.Kind)
        {
            case LineKind.Milestone:
            {
                ImGui.Dummy(new Vector2(width, rowHeight));
                var size = ImGui.CalcTextSize(l.Text);
                dl.AddText(new Vector2(min.X, textY), Theme.U32(Theme.Surface.TextSecondary), l.Text);
                var x = min.X + size.X + UiMetrics.Px(8f);
                if (x < min.X + width)
                {
                    var y = MathF.Round(min.Y + (rowHeight * 0.5f));
                    dl.AddLine(new Vector2(x, y), new Vector2(min.X + width, y), Theme.U32(Theme.Surface.Line), UiMetrics.Hairline);
                }

                return;
            }

            case LineKind.Gate:
            {
                ImGui.Dummy(new Vector2(width, rowHeight));
                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.RouteGateTooltip);
                }

                dl.AddText(new Vector2(min.X + UiMetrics.Px(IndentLogical), textY), Theme.U32(Theme.Surface.TextSecondary), l.Text);
                return;
            }

            case LineKind.Stop:
            {
                // "3 quests near Camp Dragonhead": one Teleport for the stop; its steps keep their own Flag.
                var first = bundle.Catalog.GetByRowId(l.RowId);
                var end = first is null ? min.X + width : DrawStepButtons(first, flag: false, teleport: true, walk: false, min.X + width, min.Y, rowHeight);
                ImGui.SetCursorScreenPos(min);
                ImGui.Dummy(new Vector2(MathF.Max(1f, end - min.X), rowHeight));
                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.RouteStopTooltip);
                }

                dl.PushClipRect(min, new Vector2(end, min.Y + rowHeight), true);
                dl.AddText(new Vector2(min.X + UiMetrics.Px(IndentLogical), textY), Theme.U32(Theme.Surface.TextSecondary), l.Text);
                dl.PopClipRect();
                return;
            }
        }

        // A step or an alternative: one selectable up to the step's buttons, content drawn over it.
        var stepQuest = l.Kind == LineKind.Step ? bundle.Catalog.GetByRowId(l.RowId) : null;
        var textEnd = min.X + width;
        if (stepQuest is not null)
        {
            // Walk shows on every step (each giver is its own walk) while the window is wide enough; narrower, it and
            // Go to giver are in the step's right-click menu.
            var walk = links.WalkShown && !PaneFit.FoldActions(width / UiMetrics.Scale);
            textEnd = DrawStepButtons(stepQuest, flag: true, teleport: l.Teleport, walk, min.X + width, min.Y, rowHeight);
            ImGui.SetCursorScreenPos(min);
        }

        var clicked = ImGui.Selectable("##line", false, ImGuiSelectableFlags.None, new Vector2(MathF.Max(1f, textEnd - min.X), rowHeight));
        var hovered = ImGui.IsItemHovered();
        if (stepQuest is not null)
        {
            using var context = ImRaii.ContextPopupItem("##stepMenu");
            if (context)
            {
                // A popup is its own window: it scales itself.
                UiMetrics.ApplyFontScale();
                TravelControls.MenuItems(links, stepQuest, Strings.RouteStepTeleport);
            }
        }

        // Questionable's mark (1.6.0): on its list, or no path; asked lazily, a few steps per frame.
        var (questionableMark, questionableTooltip) = l.Kind == LineKind.Step && Questionable is { } questionable ? questionable.StepMark(l.RowId) : (string.Empty, string.Empty);
        if (hovered)
        {
            var hint = Strings.RouteStepTooltipHint + "\n" + Strings.RouteStepMenuHint;
            UiMetrics.Tooltip(l.Tooltip, l.Kind != LineKind.Step ? null : questionableTooltip.Length > 0 ? questionableTooltip + "\n" + hint : hint);
        }

        if (clicked && bundle.Catalog.GetByRowId(l.RowId) is { } quest)
        {
            showQuest(quest);
        }

        var max = new Vector2(textEnd, min.Y + rowHeight);
        dl.PushClipRect(min, max, true);
        try
        {
            if (l.Kind == LineKind.Alternative)
            {
                dl.AddText(new Vector2(min.X + UiMetrics.Px(IndentLogical * 2f), textY), Theme.U32(Theme.Surface.TextTertiary), l.Text);
                return;
            }

            var x = min.X + (l.InStop ? UiMetrics.Px(IndentLogical * 0.5f) : 0f);
            var numberWidth = ImGui.CalcTextSize("0000").X;
            var number = ImGui.CalcTextSize(l.Number);
            dl.AddText(new Vector2(x + numberWidth - number.X, textY), Theme.U32(Theme.Surface.TextTertiary), l.Number);
            x += numberWidth + UiMetrics.Px(6f);

            var glyph = UiMetrics.InlineGlyphSize(line);
            MoonGlyph.Draw(dl, new Vector2(x + (glyph * 0.5f), min.Y + (rowHeight * 0.5f)), glyph * MoonGlyph.InlineRadiusFraction, l.State);
            x += glyph + UiMetrics.Px(6f);

            dl.AddText(new Vector2(x, textY), Theme.U32(Theme.Surface.TextSecondary), l.Level);
            x += ImGui.CalcTextSize(LevelSample.Value).X + UiMetrics.Px(6f);

            // Gold only for a target the character can act on now; a Blocked or locked-out target reads like any step.
            var gold = l.IsTarget && l.State is QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Accepted;
            dl.AddText(new Vector2(x, textY), Theme.U32(gold ? Theme.Accent : Theme.Surface.Text), l.Text);
            x += ImGui.CalcTextSize(l.Text).X + UiMetrics.Px(8f);
            if (l.Mark.Length > 0)
            {
                dl.AddText(new Vector2(x, textY), gold ? Theme.AccentU32 : Theme.U32(l.IsTarget ? Theme.Surface.TextSecondary : Theme.Surface.TextTertiary), l.Mark);
                x += ImGui.CalcTextSize(l.Mark).X + UiMetrics.Px(8f);
            }

            if (questionableMark.Length > 0)
            {
                dl.AddText(new Vector2(x, textY), Theme.U32(Theme.Surface.TextSecondary), questionableMark);
                x += ImGui.CalcTextSize(questionableMark).X + UiMetrics.Px(8f);
            }

            dl.AddText(new Vector2(x, textY), Theme.U32(Theme.Surface.TextTertiary), l.Detail);
        }
        finally
        {
            dl.PopClipRect();
        }
    }

    /// <summary>Rebuilds the route and every string it shows when the target, the catalog or the session changed.</summary>
    private void Refresh(CatalogBundle bundle, RouteTarget routeTarget)
    {
        if (builtVersion == session.Version && ReferenceEquals(builtBundle, bundle) && ReferenceEquals(builtTarget, routeTarget))
        {
            return;
        }

        builtVersion = session.Version;
        builtBundle = bundle;
        builtTarget = routeTarget;

        var catalog = bundle.Catalog;
        var states = session.States;
        var snapshot = session.ViewedSnapshot;
        var levelOf = snapshot is null ? null : RouteLevels.For(snapshot, session.Context);

        // A quest target's name goes through the viewed character's shield again on every rebuild: the label passed at
        // click time belongs to whoever was viewed then, and the title and the copied header both print it.
        if (routeTarget.Kind == RouteTargetKind.Quest && routeTarget.QuestRowIds.Count > 0)
        {
            routeTarget = routeTarget with { Label = session.Spoilers.DisplayName(catalog, routeTarget.QuestRowIds[0], routeTarget.Label) };
        }

        var route = UnlockRoute.Build(routeTarget, catalog, states, session.Names, levelOf);

        var caption = snapshot is null
            ? Strings.RouteForNobody
            : session.IsLive
                ? string.Format(CultureInfo.CurrentCulture, Strings.RouteForLiveFormat, snapshot.Name)
                : string.Format(CultureInfo.CurrentCulture, Strings.RouteForStoredFormat, snapshot.Name, snapshot.TakenUtc.ToLocalTime().ToString(Strings.DateTimeFormat, CultureInfo.CurrentCulture));

        var also = new StringBuilder();
        foreach (var alternative in route.TargetAlternatives)
        {
            also.Append(also.Length == 0 ? string.Empty : ", ")
                .Append(string.Format(CultureInfo.CurrentCulture, Strings.RouteAlternativeFormat, NameOf(catalog, alternative.RowId), alternative.RemainingCount));
        }

        // Where each giver is: the aetheryte nearest it, attuned or not (the same grouping as Next stops, which the
        // rebuild key need not follow), its id for merging and its name for the line. Teleport checks attunement itself.
        var place = new Dictionary<uint, (uint Id, string Name)>(route.Steps.Count);
        foreach (var step in route.Steps)
        {
            if (catalog.GetByRowId(step.RowId) is { } quest && links.GiverAetheryte(quest) is { } aetheryte)
            {
                place[step.RowId] = aetheryte;
            }
        }

        var stops = RouteStops.Group(route.Steps, rowId => place.TryGetValue(rowId, out var p) ? p.Id : 0u);
        var stopAt = new Dictionary<int, RouteStop>(stops.Count);
        foreach (var stop in stops)
        {
            stopAt[stop.Start] = stop;
        }

        var lines = new List<Line>(route.Steps.Count + stops.Count + 8);
        RouteMilestone? milestone = null;
        var anyMilestone = route.Summary.Milestones.Count > 0;
        var stopEnd = -1;
        for (var i = 0; i < route.Steps.Count; i++)
        {
            var step = route.Steps[i];
            if (anyMilestone && (i == 0 || !ReferenceEquals(step.Milestone, milestone)))
            {
                milestone = step.Milestone;
                var text = milestone is null ? Strings.RouteAfterMainScenario : string.Format(CultureInfo.CurrentCulture, Strings.RouteMilestoneFormat, milestone.Name);
                lines.Add(new Line(LineKind.Milestone, 0, QuestState.Unknown, text));
            }

            var quest = catalog.GetByRowId(step.RowId);
            if (step.LevelGate > 0 && !step.LevelMet && quest is not null)
            {
                var best = levelOf?.Invoke(quest) ?? 0;
                var text = best > 0
                    ? string.Format(CultureInfo.CurrentCulture, Strings.RouteGateWithLevelFormat, step.LevelGate, best)
                    : string.Format(CultureInfo.CurrentCulture, Strings.RouteGateFormat, step.LevelGate);
                lines.Add(new Line(LineKind.Gate, 0, QuestState.Unknown, text));
            }

            // A stop of several steps opens with its own line; a milestone or gate inside it never splits it, since
            // the stop is about the place and those lines about the story and the level.
            var inStop = i <= stopEnd;
            if (stopAt.TryGetValue(i, out var group) && group.Count > 1 && place.TryGetValue(step.RowId, out var shared))
            {
                stopEnd = i + group.Count - 1;
                inStop = true;
                lines.Add(new Line(LineKind.Stop, step.RowId, QuestState.Unknown, string.Format(CultureInfo.CurrentCulture, Strings.RouteStopFormat, group.Count, shared.Name)));
            }

            var name = NameOf(catalog, step.RowId);
            var mark = step.TargetLabel.Length > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.RouteReachesFormat, step.TargetLabel)
                : step.IsTarget ? Strings.RouteTargetMark
                : step.IsMainScenario ? Strings.RouteMsqMark
                : string.Empty;
            var near = !inStop && place.TryGetValue(step.RowId, out var own) ? own.Name : string.Empty;
            var detail = near.Length > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.RouteNearFormat, near) + Strings.RouteDetailSeparator + step.StatusText : step.StatusText;
            lines.Add(new Line(LineKind.Step, step.RowId, step.State, name)
            {
                Number = (i + 1).ToString(CultureInfo.CurrentCulture) + ".",
                Level = string.Format(CultureInfo.CurrentCulture, Strings.RouteLevelFormat, step.DisplayLevel),
                Detail = detail,
                Mark = mark,
                IsTarget = step.IsTarget,
                InStop = inStop,
                Teleport = !inStop,
                Tooltip = near.Length > 0 ? name + "\n" + step.StatusText + "\n" + string.Format(CultureInfo.CurrentCulture, Strings.RouteNearFormat, near) : name + "\n" + step.StatusText,
            });

            foreach (var alternative in step.Alternatives)
            {
                var other = NameOf(catalog, alternative.RowId);
                var text = alternative.RemainingCount == 1
                    ? string.Format(CultureInfo.CurrentCulture, Strings.RouteOrInsteadOneFormat, other)
                    : string.Format(CultureInfo.CurrentCulture, Strings.RouteOrInsteadFormat, other, alternative.RemainingCount);
                lines.Add(new Line(LineKind.Alternative, alternative.RowId, alternative.State, text) { Tooltip = Strings.RouteAlternativeTooltip });
            }
        }

        var title = string.Format(CultureInfo.CurrentCulture, Strings.RouteTitleFormat, routeTarget.Label);
        view = new View(route, title, caption, route.Summary.Text, also.Length == 0 ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.RouteAlsoUnlockedByFormat, also), lines.ToArray());
    }

    private string NameOf(QuestCatalog catalog, uint rowId) =>
        session.Spoilers.DisplayName(catalog, rowId, rowId.ToString(CultureInfo.InvariantCulture));

    private enum LineKind : byte
    {
        Milestone,
        Gate,
        Step,
        Alternative,

        /// <summary>"3 quests near Camp Dragonhead", heading consecutive steps at one aetheryte; <see cref="Line.RowId"/> is its first step.</summary>
        Stop,
    }

    /// <summary>One line of the list; every string composed when the route is built.</summary>
    private sealed record Line(LineKind Kind, uint RowId, QuestState State, string Text)
    {
        public string Number { get; init; } = string.Empty;

        public string Level { get; init; } = string.Empty;

        public string Detail { get; init; } = string.Empty;

        public string Mark { get; init; } = string.Empty;

        public string Tooltip { get; init; } = string.Empty;

        public bool IsTarget { get; init; }

        /// <summary>A step inside a stop of several steps: indented, its Teleport on the stop's line.</summary>
        public bool InStop { get; init; }

        /// <summary>The step shows its own Teleport (a stop of one step).</summary>
        public bool Teleport { get; init; }
    }

    private sealed record View(UnlockRoute Route, string Title, string Caption, string Summary, string AlsoUnlockedBy, Line[] Lines)
    {
        public static readonly View Empty = new(
            UnlockRoute.Build(RouteTarget.ForQuest(0, string.Empty), QuestCatalog.Empty, new Dictionary<uint, Core.Evaluation.QuestEvaluation>()),
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            []);
    }

    /// <summary>
    /// The pins "Pin all" writes to: the main window's pin list for the viewed character (QueryRunner, saved in
    /// <c>user/pins.json</c>). Decision 10 (per character or per account) is taken in <see cref="RoutePins.PinAll"/>.
    /// </summary>
    private sealed class PinStore(QueryRunner runner) : IRoutePinStore
    {
        public bool CanPin => runner.CanPin;

        public ulong? Owner => runner.PinOwner;

        public bool IsPinned(uint rowId) => runner.IsPinned(rowId);

        public bool TogglePin(uint rowId) => runner.TogglePin(rowId);
    }
}
