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
/// pane's action bar, a Moonlit row's menu and the Characters job rows. Every string is composed when the route is
/// rebuilt, and the list draws only its visible lines, so a thousand-quest route costs nothing per frame.
/// </summary>
public sealed class RouteWindow : Window
{
    private const double NoteSeconds = 8.0;
    private const float IndentLogical = 22f;

    private static readonly string RouteIcon = FontAwesomeIcon.MapSigns.ToIconString();
    private static readonly string PinAllLabelSuffix = Chrome.HoldIdSuffix;

    private readonly SessionState session;
    private readonly Action<QuestRecord> showQuest;
    private readonly PinStore pins;
    private readonly ConfirmGate pinGate = new();

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
    /// <param name="showQuest">Brings the main window forward and shows a quest in the Journal.</param>
    public RouteWindow(SessionState session, QueryRunner runner, Action<QuestRecord> showQuest)
        : base(Strings.RouteWindowTitle)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        pins = new PinStore(runner);
        this.showQuest = showQuest ?? throw new ArgumentNullException(nameof(showQuest));
        Size = new Vector2(560f, 620f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(380f, 260f) };
    }

    /// <summary>Opens the window on the route to <paramref name="routeTarget"/> and brings it to the front.</summary>
    public void Show(RouteTarget routeTarget)
    {
        target = routeTarget ?? throw new ArgumentNullException(nameof(routeTarget));
        builtVersion = -1;
        pinGate.Cancel();
        undo = RoutePinBatch.Empty;
        noteUntil = -1.0;
        IsOpen = true;
        BringToFront();
    }

    public override void PreDraw()
    {
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
        using (Theme.PushText(Theme.Accent))
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
        using (Theme.PushText(Theme.Accent))
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

    private void Note(string text, RoutePinBatch added)
    {
        note = text;
        undo = added;
        noteUntil = ImGui.GetTime() + NoteSeconds;
    }

    /// <summary>"Pin all (N)" with N the steps not pinned yet (every step when there is nobody to pin for); composed only when the route or the pins change.</summary>
    private string PinAllLabel(View v)
    {
        if (ReferenceEquals(pinLabelView, v) && pinLabelPins == runner.PinsVersion)
        {
            return pinLabel;
        }

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

                dl.AddText(new Vector2(min.X + UiMetrics.Px(IndentLogical), textY), Theme.AccentU32, l.Text);
                return;
            }
        }

        // A step or an alternative: one selectable across the line, content drawn over it.
        var clicked = ImGui.Selectable("##line", false, ImGuiSelectableFlags.None, new Vector2(0f, rowHeight));
        var hovered = ImGui.IsItemHovered();
        if (hovered)
        {
            UiMetrics.Tooltip(l.Tooltip, l.Kind == LineKind.Step ? Strings.RouteStepTooltipHint : null);
        }

        if (clicked && bundle.Catalog.GetByRowId(l.RowId) is { } quest)
        {
            showQuest(quest);
        }

        var max = new Vector2(min.X + width, min.Y + rowHeight);
        dl.PushClipRect(min, max, true);
        try
        {
            if (l.Kind == LineKind.Alternative)
            {
                dl.AddText(new Vector2(min.X + UiMetrics.Px(IndentLogical * 2f), textY), Theme.U32(Theme.Surface.TextTertiary), l.Text);
                return;
            }

            var x = min.X;
            var numberWidth = ImGui.CalcTextSize("0000").X;
            var number = ImGui.CalcTextSize(l.Number);
            dl.AddText(new Vector2(x + numberWidth - number.X, textY), Theme.U32(Theme.Surface.TextTertiary), l.Number);
            x += numberWidth + UiMetrics.Px(6f);

            var glyph = UiMetrics.InlineGlyphSize(line);
            MoonGlyph.Draw(dl, new Vector2(x + (glyph * 0.5f), min.Y + (rowHeight * 0.5f)), glyph * MoonGlyph.InlineRadiusFraction, l.State);
            x += glyph + UiMetrics.Px(6f);

            dl.AddText(new Vector2(x, textY), Theme.U32(Theme.Surface.TextSecondary), l.Level);
            x += ImGui.CalcTextSize("Lv 000").X + UiMetrics.Px(6f);

            dl.AddText(new Vector2(x, textY), Theme.U32(l.IsTarget ? Theme.Accent : Theme.Surface.Text), l.Text);
            x += ImGui.CalcTextSize(l.Text).X + UiMetrics.Px(8f);
            if (l.Mark.Length > 0)
            {
                dl.AddText(new Vector2(x, textY), l.IsTarget ? Theme.AccentU32 : Theme.U32(Theme.Surface.TextTertiary), l.Mark);
                x += ImGui.CalcTextSize(l.Mark).X + UiMetrics.Px(8f);
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

        var lines = new List<Line>(route.Steps.Count + 8);
        RouteMilestone? milestone = null;
        var anyMilestone = route.Summary.Milestones.Count > 0;
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

            var name = NameOf(catalog, step.RowId);
            var mark = step.IsTarget ? Strings.RouteTargetMark : step.IsMainScenario ? Strings.RouteMsqMark : string.Empty;
            lines.Add(new Line(LineKind.Step, step.RowId, step.State, name)
            {
                Number = (i + 1).ToString(CultureInfo.CurrentCulture) + ".",
                Level = string.Format(CultureInfo.CurrentCulture, Strings.RouteLevelFormat, step.DisplayLevel),
                Detail = step.StatusText,
                Mark = mark,
                IsTarget = step.IsTarget,
                Tooltip = name + "\n" + step.StatusText,
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
