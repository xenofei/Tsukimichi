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
using Tsukimichi.Core.Runtime;
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
/// <see cref="RoutePins.PinAll"/>, followed by the floating Undo. Opened by <see cref="UiState.OpenRoute"/> from the detail
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

    /// <summary>A step keeps this much text room (logical px) before its clear badges are left out.</summary>
    private const float BadgeTextMinLogical = 240f;

    private static readonly string RouteIcon = FontAwesomeIcon.MapSigns.ToIconString();

    /// <summary>The target's own icon before the title, logical px (spec-1.15 B4).</summary>
    private const float HeaderIconLogical = 22f;
    private static readonly string PinAllLabelSuffix = Chrome.HoldIdSuffix;

    /// <summary>The widest level label ("Lv 100"), which sets where a step's text starts.</summary>
    private static readonly Localization.LocText LevelSample = new(static () => string.Format(CultureInfo.CurrentCulture, Strings.RouteLevelFormat, 100));

    private readonly SessionState session;
    private readonly Action<QuestRecord> showQuest;
    private readonly PinStore pins;
    private readonly ConfirmGate pinGate = new();
    private readonly GameLinks links;
    private readonly DiscordCopy discordCopy = new();
    private readonly ActiveRouteService routes;

    private RouteTarget? target;
    private int builtVersion = -1;
    private CatalogBundle? builtBundle;
    private RouteTarget? builtTarget;
    private int builtEntrances = -1;
    private View view = View.Empty;
    private Theme.StyleScope nightChrome;

    private readonly QueryRunner runner;

    // What the last Pin all added and for whom; dropped with its note when the viewed character changes.
    private RoutePinBatch undo = RoutePinBatch.Empty;
    private int undoToast;
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

    /// <summary>The flying zones, for a route to flying (K3); null while they are read. Set by the plugin.</summary>
    public Func<FlightIndex?>? Flight { get; set; }

    /// <summary>
    /// Where a flying zone's field currents stand (<see cref="AetherCurrentPlaces"/>), read once per zone on a worker
    /// (it reads the zone's layout files), never on the draw thread. Set by the plugin.
    /// </summary>
    public Func<FlightZone, IReadOnlyList<AetherCurrentPlace>>? FieldPlaces { get; set; }

    /// <summary>Whether the viewed character has attuned an aether current; null when that cannot be read (a stored alt). Set by the plugin.</summary>
    public Func<uint, bool?>? Attuned { get; set; }

    /// <summary>The C7 clear badges (1.19.0): a step that involves a duty wears them at its trailing end; null wears none. Set by the plugin.</summary>
    public ClearBadgeSource? Badges { get; set; }

    // Each flying zone's field current places, read on a worker the first time a route asks for the zone.
    private readonly Dictionary<uint, WarmedValue<IReadOnlyList<AetherCurrentPlace>>> fieldPlaces = [];

    // The places the lines were built without (still being read): the route is built again once they land.
    private WarmedValue<IReadOnlyList<AetherCurrentPlace>>? builtFieldPending;

    // The badges' revision the lines were built with.
    private int builtBadges = -1;

    /// <summary>Opens the window on the route to <paramref name="routeTarget"/> and brings it to the front.</summary>
    public void Show(RouteTarget routeTarget)
    {
        ArgumentNullException.ThrowIfNull(routeTarget);

        // Route to flying (K3) from a surface that did not know the zone: the zone whose quest currents the route takes.
        if (routeTarget is { Kind: RouteTargetKind.Unlock, IsUnion: true, FlyingTerritory: 0 } && Flight?.Invoke()?.ZoneOfQuests(routeTarget.QuestRowIds) is { } zone)
        {
            routeTarget = routeTarget with { FlyingTerritory = zone.TerritoryId };
        }

        target = routeTarget;
        builtVersion = -1;
        pinGate.Cancel();
        DropUndo();
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
            MinimumSize = new Vector2(MinWidthLogical, MinHeightLogical) * UiMetrics.UiScale,
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

        var firstQuest = target.QuestRowIds.Count > 0 ? bundle.Catalog.GetByRowId(target.QuestRowIds[0]) : null;
        DrawHeader(v, ActionIcons.RouteHeader(target, firstQuest));

        // A route to flying whose quests are done can still have field currents left: those lines are the route then.
        var fieldOnly = v.FieldLeft > 0 && v.Route.Outcome == RouteOutcome.AlreadyDone;
        switch (fieldOnly ? RouteOutcome.Route : v.Route.Outcome)
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

        TextFlow.Wrapped(v.Summary, Chrome.RoomX());
        if (v.AlsoUnlockedBy.Length > 0)
        {
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextWrapped(v.AlsoUnlockedBy);
            }
        }

        if (v.Route.Outcome == RouteOutcome.LockedOut)
        {
            using (Theme.PushText(Theme.DangerText))
            {
                ImGui.TextWrapped(Strings.RouteLockedOut);
            }
        }

        DrawActions(bundle, v);
        DrawFollowActions(v);
        Chrome.Hairline();
        DrawLines(bundle, v);
    }

    /// <summary>
    /// The route's title and whom it is for (R3 #5, #9): the sign icon, then the title, which wraps between words rather
    /// than running off a narrow window; in the Title role at Flair Full and Quiet (the icon centred on its first line),
    /// the body font under Plain. The caption wraps under it in the disabled tone. 1.15 (UI-5e, I19): the sign gives way
    /// to the target's own icon at 22 px (<see cref="ActionIcons.RouteHeader"/>: a quest's map marker, a duty's tile, a
    /// reward's, a job's or an expansion's icon), the sign staying for a target without one.
    /// </summary>
    private static void DrawHeader(View v, GameIconRef icon)
    {
        var moonRoad = Theme.Sectioned;
        var y = ImGui.GetCursorPosY();
        float titleLine;
        using (moonRoad ? Typography.Title(v.Title) : default)
        {
            titleLine = ImGui.GetTextLineHeight();
        }

        if (icon.HasIcon)
        {
            var side = MathF.Round(UiMetrics.Px(HeaderIconLogical));
            ImGui.SetCursorPosY(y + MathF.Max(0f, (titleLine - side) * 0.5f));
            var min = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(side));
            Chrome.DrawPillIcon(ImGui.GetWindowDrawList(), icon, min, side, Theme.U32(Theme.Surface.TextSecondary), enabled: true);
        }
        else
        {
            ImGui.SetCursorPosY(y + MathF.Max(0f, (titleLine - ImGui.GetTextLineHeight()) * 0.5f));
            ImGui.PushFont(UiBuilder.IconFont);
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextUnformatted(RouteIcon);
            }

            ImGui.PopFont();
        }

        ImGui.SameLine();
        ImGui.SetCursorPosY(y);

        // A route to an unlock (K3) says how many stops are left at the title line's right end ("10 stops").
        var stopsWidth = v.Stops.Length > 0 ? ImGui.CalcTextSize(v.Stops).X + UiMetrics.Px(12f) : 0f;
        var titleTop = ImGui.GetCursorScreenPos();
        using (moonRoad ? Typography.Title(v.Title) : default)
        {
            TextFlow.Wrapped(v.Title, MathF.Max(1f, Chrome.RoomX() - stopsWidth), Theme.U32(Theme.Surface.Text));
        }

        if (stopsWidth > 0f)
        {
            var right = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
            ImGui.GetWindowDrawList().AddText(
                new Vector2(right - stopsWidth + UiMetrics.Px(12f), titleTop.Y + MathF.Max(0f, (titleLine - ImGui.GetTextLineHeight()) * 0.5f)),
                Theme.U32(Theme.Surface.TextSecondary),
                v.Stops);
        }

        TextFlow.Wrapped(v.Caption, Chrome.RoomX(), ImGui.GetColorU32(ImGuiCol.TextDisabled));
    }

    private void DrawActions(CatalogBundle bundle, View v)
    {
        // The pins follow the viewed character; a Pin all made for someone else can no longer be undone from here.
        runner.SyncPins();
        if (undo.Added.Count > 0 && !RoutePins.CanUndo(undo, pins))
        {
            DropUndo();
        }

        if (ImGui.Button(Strings.RouteCopy))
        {
            ImGui.SetClipboardText(RouteMarkdown.Write(v.Route, bundle.Catalog, session.Spoilers.DisplayName));
            Note(Strings.RouteCopied);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.RouteCopyTooltip);
        }

        // Copy for Discord (1.8.0): bullets, optional links (never on a masked name), parts of 2,000 characters.
        Chrome.SameLineOrWrap(ImGui.CalcTextSize(Strings.LinksCopyDiscord).X + (ImGui.GetStyle().FramePadding.X * 2f));
        discordCopy.Draw("route", v.Route, (Window: this, Route: v.Route, Catalog: bundle.Catalog), static (s, addLinks) => s.Window.RouteDiscordText(s.Route, s.Catalog, addLinks));

        var canPin = pins.CanPin && v.Route.Steps.Count > 0;
        var pinAll = PinAllLabel(v);
        Chrome.SameLineOrWrap(ImGui.CalcTextSize(pinAll, true, -1f).X + (ImGui.GetStyle().FramePadding.X * 2f));
        ImGui.BeginDisabled(!canPin);
        var confirmed = Chrome.HoldButton(pinAll, pinGate);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            if (pins.CanPin)
            {
                Safety.Tooltip(Strings.RoutePinAllTooltip, GuardedAction.PinAll);
            }
            else
            {
                UiMetrics.Tooltip(Strings.RoutePinAllUnavailable);
            }
        }

        if (AutomationGate.Questionable(Questionable) is { } questionable)
        {
            Chrome.SameLineOrWrap(QuestionableActions.ButtonWidth);
            questionable.DrawButton(QuestionableHost, "##questionable", v.Route, static route => StepRowIds(route));
        }

        if (confirmed && canPin)
        {
            var added = RoutePins.PinAll(v.Route, pins);
            var count = added.Added.Count;
            if (count == 0)
            {
                Note(Strings.RouteAllPinnedAlready);
            }
            else
            {
                // The floating Undo (feature plan v6 S2) takes back exactly what this Pin all added.
                undo = added;
                noteUntil = -1.0;
                undoToast = UndoToast.Show(
                    count == 1 ? Strings.RoutePinnedOne : string.Format(CultureInfo.CurrentCulture, Strings.RoutePinnedFormat, count),
                    UndoPinAll);
            }
        }

        if (noteUntil < 0.0 || ImGui.GetTime() >= noteUntil)
        {
            noteUntil = -1.0;
            return;
        }

        // The note goes on the line when it fits, else under the buttons, never off the window's edge.
        Chrome.SameLineOrWrap(ImGui.CalcTextSize(note).X);
        ImGui.AlignTextToFramePadding();
        using (Theme.PushText(Theme.Surface.Text))
        {
            ImGui.TextUnformatted(note);
        }
    }

    /// <summary>The Undo of the last Pin all: unpins what it added, if those pins are still the viewed character's.</summary>
    private void UndoPinAll()
    {
        if (undo.Added.Count > 0 && RoutePins.CanUndo(undo, pins))
        {
            RoutePins.Undo(undo, pins);
        }

        undo = RoutePinBatch.Empty;
    }

    /// <summary>Forgets the last Pin all and takes its Undo down (another route, another character).</summary>
    private void DropUndo()
    {
        undo = RoutePinBatch.Empty;
        UndoToast.Dismiss(undoToast);
    }

    /// <summary>
    /// The second row (1.6.0): "Follow this route" (or "Stop following" when it is the followed one) and "Flag next
    /// stop". Following needs a character, since the route belongs to the one it was built for.
    /// </summary>
    /// <summary>The route as Copy for Discord copies it; built only on a click.</summary>
    private string RouteDiscordText(UnlockRoute route, QuestCatalog catalog, bool addLinks)
    {
        var spoilers = session.Spoilers;
        return RouteMarkdown.WriteDiscord(route, catalog, spoilers.DisplayName, addLinks ? q => spoilers.IsMasked(q) ? null : links.PreferredLink(q) : null);
    }

    private void DrawFollowActions(View v)
    {
        var owner = session.ViewedContentId;
        var following = target is not null && routes.Follows(target, owner);
        var canFollow = owner is not null && v.Route.Steps.Count > 0;
        var followLabel = following ? Strings.RouteStopFollowing : Strings.RouteFollow;
        if (TravelControls.ToolbarButton("##follow", following ? ActionGlyphs.Stop : ActionGlyphs.Follow, followLabel, following || canFollow) && target is not null)
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

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(following ? Strings.RouteStopFollowingTooltip : owner is null ? Strings.RouteFollowUnavailable : Strings.RouteFollowTooltip);
        }

        var stop = routes.NextStopQuest(v.Route);
        var canFlag = stop is not null && links.CanFlagMap(stop);
        Chrome.SameLineOrWrap(TravelControls.ToolbarButtonWidth(ActionIcons.FlagIcon, Strings.RouteFlagNextStop));
        if (TravelControls.ToolbarButton("##flagNextStop", ActionIcons.FlagIcon, Strings.RouteFlagNextStop, canFlag))
        {
            routes.FlagNextStop(v.Route);
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

    private void Note(string text)
    {
        note = text;
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
        if (GiverPortraits.Enabled)
        {
            // Every step's giver as a 24 px avatar (1.15, F5): the rows stand a little taller, all alike.
            rowHeight = MathF.Max(rowHeight, MathF.Round(UiMetrics.Px(PortraitPlate.AvatarSize + 4f)));
        }
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

    /// <summary>Walk's width, sized for the longer of Walk and Stop so the row does not shift when it turns.</summary>
    private static float WalkButtonWidth() => TravelControls.WalkWidth();

    /// <summary>
    /// The line's Flag, Teleport and Walk (Stop while the character moves), right-aligned at <paramref name="right"/> on
    /// the row starting at <paramref name="top"/>; returns the x where they begin (the text ends before it). A button
    /// that cannot start says why on hover.
    /// </summary>
    private float DrawStepButtons(QuestRecord quest, bool flag, bool teleport, bool walk, float right, float top, float rowHeight)
    {
        var gap = UiMetrics.Px(4f);
        var width = 0f;
        var flagWidth = TravelControls.FlagWidth(Strings.RouteStepFlag);
        var teleportWidth = Chrome.ActionPillWidth(ActionIcons.TeleportIcon, Strings.RouteStepTeleport, PillLayout.Row);
        if (flag)
        {
            width += flagWidth;
        }

        if (teleport)
        {
            width += teleportWidth + (flag ? gap : 0f);
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
            if (TravelControls.FlagButton(Strings.RouteStepFlag, canFlag))
            {
                links.FlagMap(quest);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(canFlag ? Strings.RouteStepFlagTooltip : Strings.RouteStepFlagUnavailable);
            }

            x += flagWidth + gap;
        }

        if (teleport)
        {
            ImGui.SetCursorScreenPos(new Vector2(x, y));
            var canTeleport = links.CanTeleport(quest);
            if (Chrome.ActionPill("##stepTeleport", ActionIcons.TeleportIcon, Strings.RouteStepTeleport, PillTone.Normal, canTeleport, size: PillLayout.Row))
            {
                links.TeleportToGiver(quest);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(TeleportTip(quest));
            }

            x += teleportWidth + gap;
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
        TravelControls.WalkButton(links, quest);
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
                // An MSQ milestone: the Moon Road heading (sigil, Eyebrow, fading brass rule) at Full and Quiet, drawn
                // inside the row so every line keeps its height (R3 #9); under Plain the label and a hairline.
                ImGui.Dummy(new Vector2(width, rowHeight));
                var ink = Theme.U32(Theme.Surface.TextSecondary);
                if (!SectionHeading.DrawInRow(dl, min, width, rowHeight, l.Text, ink, out var cut))
                {
                    var size = ImGui.CalcTextSize(l.Text).X;
                    cut = Chrome.EllipsisTextAt(dl, new Vector2(min.X, textY), width, l.Text, ink, size);
                    var x = min.X + size + UiMetrics.Px(8f);
                    if (x < min.X + width)
                    {
                        var y = MathF.Round(min.Y + (rowHeight * 0.5f));
                        dl.AddLine(new Vector2(x, y), new Vector2(min.X + width, y), Theme.U32(Theme.Surface.Line), UiMetrics.Hairline);
                    }
                }

                if (cut && ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(l.Text);
                }

                return;
            }

            case LineKind.Gate:
            {
                ImGui.Dummy(new Vector2(width, rowHeight));
                var indent = UiMetrics.Px(IndentLogical);
                var cut = Chrome.EllipsisTextAt(dl, new Vector2(min.X + indent, textY), width - indent, l.Text, Theme.U32(Theme.Surface.TextSecondary));
                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(cut ? l.Text : Strings.RouteGateTooltip, cut ? Strings.RouteGateTooltip : null);
                }

                return;
            }

            case LineKind.Stop:
            {
                // "3 quests near Camp Dragonhead": one Teleport for the stop; its steps keep their own Flag.
                var first = bundle.Catalog.GetByRowId(l.RowId);
                var end = first is null ? min.X + width : DrawStepButtons(first, flag: false, teleport: true, walk: false, min.X + width, min.Y, rowHeight);
                ImGui.SetCursorScreenPos(min);
                ImGui.Dummy(new Vector2(MathF.Max(1f, end - min.X), rowHeight));
                var indent = UiMetrics.Px(IndentLogical);
                var cut = Chrome.EllipsisTextAt(dl, new Vector2(min.X + indent, textY), end - min.X - indent, l.Text, Theme.U32(Theme.Surface.TextSecondary));
                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(cut ? l.Text : Strings.RouteStopTooltip, cut ? Strings.RouteStopTooltip : null);
                }

                return;
            }
        }

        if (l.Kind == LineKind.Field)
        {
            DrawFieldLine(dl, l, min, width, rowHeight, textY);
            return;
        }

        // A step or an alternative: one selectable up to the step's buttons, content drawn over it.
        var stepQuest = l.Kind == LineKind.Step ? bundle.Catalog.GetByRowId(l.RowId) : null;
        var textEnd = min.X + width;
        if (stepQuest is not null)
        {
            // Walk shows on every step (each giver is its own walk) while the window is wide enough; narrower, it and
            // Go to giver are in the step's right-click menu.
            var walk = links.WalkShown && !PaneFit.FoldActions(width / UiMetrics.Scale);
            textEnd = DrawStepButtons(stepQuest, flag: true, teleport: l.Teleport && links.TeleportShown, walk, min.X + width, min.Y, rowHeight);
            ImGui.SetCursorScreenPos(min);
        }

        // The clear badges (1.19.0, C7) of the duty the step involves, before its buttons, while the step's text keeps
        // room for its number, moon, level and a readable name; on a narrower window the step goes without them.
        var badgesLeft = 0f;
        if (l.Badges.Length > 0)
        {
            var run = DutyBadges.RunWidth(l.Badges);
            if (textEnd - run - UiMetrics.Px(8f) - min.X >= UiMetrics.Px(BadgeTextMinLogical))
            {
                badgesLeft = textEnd - run;
                textEnd = badgesLeft - UiMetrics.Px(8f);
            }
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
        if (clicked && bundle.Catalog.GetByRowId(l.RowId) is { } quest)
        {
            showQuest(quest);
        }

        // The step's giver as an avatar between the level and the name (1.15, F5); its hover shows the portrait.
        var avatar = stepQuest is not null && GiverPortraits.Enabled ? MathF.Round(UiMetrics.Px(PortraitPlate.AvatarSize)) : 0f;
        var request = avatar > 0f ? GiverPortraits.For(stepQuest!, session.Spoilers) : PortraitRequest.None;
        var nameCut = DrawStepText(dl, l, min, textEnd, textY, rowHeight, line, questionableMark, avatar, request, out var avatarMin);
        if (hovered && avatar > 0f && ImGui.IsMouseHoveringRect(avatarMin, avatarMin + new Vector2(avatar)))
        {
            Chrome.PortraitTooltip(request, stepQuest!.Issuer?.Name ?? string.Empty, GiverPortraits.Place(stepQuest));
        }
        else if (hovered)
        {
            // A step's tooltip already opens with its whole name; an alternative cut short gets its name first.
            var hint = Strings.RouteStepTooltipHint + "\n" + Strings.RouteStepMenuHint;
            if (l.Kind != LineKind.Step)
            {
                UiMetrics.Tooltip(nameCut ? l.Text : l.Tooltip, nameCut ? l.Tooltip : null);
            }
            else
            {
                UiMetrics.Tooltip(l.Tooltip, questionableTooltip.Length > 0 ? questionableTooltip + "\n" + hint : hint);
            }
        }

        if (badgesLeft > 0f)
        {
            DutyBadges.DrawRun(l.Badges, badgesLeft, min.Y, rowHeight, badgesLeft + DutyBadges.RunWidth(l.Badges), Plugin.TextureProvider);
        }
    }

    /// <summary>
    /// A field current (K3): the number, "Aether current · &lt;nearest aetheryte&gt;" and "field", with a Flag on where the
    /// game's layout places it (none when it does not).
    /// </summary>
    private void DrawFieldLine(ImDrawListPtr dl, Line l, Vector2 min, float width, float rowHeight, float textY)
    {
        var end = min.X + width;
        if (l.FieldPosition is { } at && l.FieldTerritory != 0)
        {
            var flagWidth = TravelControls.FlagWidth(Strings.RouteStepFlag);
            end -= flagWidth;
            ImGui.SetCursorScreenPos(new Vector2(end, min.Y + ((rowHeight - ImGui.GetTextLineHeight()) * 0.5f)));
            var canFlag = links.CanFlagSpot(l.FieldTerritory);
            if (TravelControls.FlagButton(Strings.RouteStepFlag, canFlag))
            {
                links.FlagSpot(l.FieldTerritory, at);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(canFlag ? Strings.RouteFieldFlagTooltip : Strings.RouteStepFlagUnavailable);
            }

            end -= UiMetrics.Px(8f);
        }

        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(new Vector2(MathF.Max(1f, end - min.X), rowHeight));
        var hovered = ImGui.IsItemHovered();
        var numberWidth = ImGui.CalcTextSize("0000").X;
        var number = ImGui.CalcTextSize(l.Number);
        dl.AddText(new Vector2(min.X + numberWidth - number.X, textY), Theme.U32(Theme.Surface.TextTertiary), l.Number);
        var x = min.X + numberWidth + UiMetrics.Px(6f) + UiMetrics.InlineGlyphSize(ImGui.GetTextLineHeight()) + UiMetrics.Px(6f);
        var markWidth = ImGui.CalcTextSize(l.Detail).X;
        var room = MathF.Max(1f, end - x - markWidth - UiMetrics.Px(8f));
        var cut = Chrome.EllipsisTextAt(dl, new Vector2(x, textY), room, l.Text, Theme.U32(Theme.Surface.Text));
        dl.AddText(new Vector2(end - markWidth, textY), Theme.U32(Theme.Surface.TextTertiary), l.Detail);
        if (hovered)
        {
            UiMetrics.Tooltip(cut ? l.Tooltip : Strings.RouteFieldTooltip);
        }
    }

    /// <summary>
    /// A step's or an alternative's text, from <paramref name="min"/> to <paramref name="textEnd"/> (R3 #5): the number,
    /// moon and level, the giver's avatar when <paramref name="avatar"/> is over 0 (its top-left in
    /// <paramref name="avatarMin"/>), then the name, which ends in an ellipsis rather than being cut mid-letter, then its
    /// marks (dropped, least important first, before the name falls under its minimum) and the detail in what is left.
    /// Returns whether the name was cut.
    /// </summary>
    private static bool DrawStepText(ImDrawListPtr dl, Line l, Vector2 min, float textEnd, float textY, float rowHeight, float line, string questionableMark, float avatar, in PortraitRequest request, out Vector2 avatarMin)
    {
        avatarMin = new Vector2(float.MaxValue);
        if (l.Kind == LineKind.Alternative)
        {
            var indent = UiMetrics.Px(IndentLogical * 2f);
            return Chrome.EllipsisTextAt(dl, new Vector2(min.X + indent, textY), textEnd - min.X - indent, l.Text, Theme.U32(Theme.Surface.TextTertiary));
        }

        // The number, the moon and the level are fixed width; the clip keeps them off the buttons on a very narrow window.
        dl.PushClipRect(min, new Vector2(textEnd, min.Y + rowHeight), true);
        try
        {
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
            if (avatar > 0f && x + avatar < textEnd)
            {
                avatarMin = new Vector2(x, min.Y + MathF.Round((rowHeight - avatar) * 0.5f));
                Chrome.Portrait(dl, avatarMin, avatar, request);
                x += avatar + UiMetrics.Px(PortraitPlate.AvatarGap);
            }

            var gap = UiMetrics.Px(8f);
            var markWidth = l.Mark.Length > 0 ? ImGui.CalcTextSize(l.Mark).X : 0f;
            var questionableWidth = questionableMark.Length > 0 ? ImGui.CalcTextSize(questionableMark).X : 0f;
            var nameWidth = ImGui.CalcTextSize(l.Text).X;
            Span<float> parts = stackalloc float[2];
            Span<bool> shown = stackalloc bool[2];
            parts[0] = markWidth > 0f ? gap + markWidth : 0f;
            parts[1] = questionableWidth > 0f ? gap + questionableWidth : 0f;
            var fit = LineFit.Fit(textEnd - x, nameWidth, UiMetrics.Px(LayoutBudgets.RowNameMinLogical), parts, shown, gap, UiMetrics.Px(24f));

            // Gold only for a target the character can act on now; a Blocked or locked-out target reads like any step.
            var gold = l.IsTarget && l.State is QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Accepted;
            Chrome.EllipsisTextAt(dl, new Vector2(x, textY), fit.NameRoom, l.Text, Theme.U32(gold ? Theme.Accent : Theme.Surface.Text), nameWidth);
            x += fit.NameDrawn;
            if (shown[0] && markWidth > 0f)
            {
                x += gap;
                dl.AddText(new Vector2(x, textY), gold ? Theme.AccentU32 : Theme.U32(l.IsTarget ? Theme.Surface.TextSecondary : Theme.Surface.TextTertiary), l.Mark);
                x += markWidth;
            }

            if (shown[1] && questionableWidth > 0f)
            {
                x += gap;
                dl.AddText(new Vector2(x, textY), Theme.U32(Theme.Surface.TextSecondary), questionableMark);
                x += questionableWidth;
            }

            if (fit.TailRoom > 0f && l.Detail.Length > 0)
            {
                Chrome.EllipsisTextAt(dl, new Vector2(x + gap, textY), fit.TailRoom, l.Detail, Theme.U32(Theme.Surface.TextTertiary));
            }

            return fit.NameCut;
        }
        finally
        {
            dl.PopClipRect();
        }
    }

    /// <summary>Rebuilds the route and every string it shows when the target, the catalog, the session or the known ways into interiors changed.</summary>
    private void Refresh(CatalogBundle bundle, RouteTarget routeTarget)
    {
        // The ways into interiors becoming known (resolved ahead on a worker) moves givers inside them to their door's aetheryte.
        var entrances = links.EntranceRevision;
        var badges = Badges?.Revision ?? 0;
        if (builtVersion == session.Version && ReferenceEquals(builtBundle, bundle) && ReferenceEquals(builtTarget, routeTarget) && builtEntrances == entrances && builtBadges == badges
            && builtFieldPending is not { IsDone: true })
        {
            return;
        }

        builtVersion = session.Version;
        builtBundle = bundle;
        builtTarget = routeTarget;
        builtEntrances = entrances;
        builtBadges = badges;
        builtFieldPending = null;

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
        var clearBadges = Badges;
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
                Badges = quest is not null && clearBadges is not null ? clearBadges.ForQuest(quest, Core.Companions.DutyBadgeSurface.Route) : [],
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

        // Route to flying (K3): after the quests, each field current not attuned yet, by the aetheryte nearest it.
        var fieldLeft = AddFieldLines(routeTarget, route.Steps.Count, lines);
        var stopsText = string.Empty;
        if (routeTarget.Kind == RouteTargetKind.Unlock)
        {
            var count = FieldCurrentStops.Stops(route.Steps.Count, fieldLeft);
            stopsText = string.Format(CultureInfo.CurrentCulture, count == 1 ? Strings.RouteStopsOne : Strings.RouteStopsFormat, count);
        }

        var title = string.Format(CultureInfo.CurrentCulture, Strings.RouteTitleFormat, routeTarget.Label);
        var summary = route.Steps.Count == 0 && fieldLeft > 0 ? stopsText : route.Summary.Text;
        view = new View(route, title, caption, summary, also.Length == 0 ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.RouteAlsoUnlockedByFormat, also), lines.ToArray())
        {
            FieldLeft = fieldLeft,
            Stops = stopsText,
        };
    }

    /// <summary>
    /// The field currents of a route to flying the viewed character has not attuned (a stored alt's are all listed,
    /// since attunement can only be read live), numbered on from the quests; returns how many. Places come from the
    /// game's layouts, read once per zone on a worker; a current no layout places reads "in the field". While they are
    /// read, every current left has its line already, unplaced, and the route is built again when they land: the lines
    /// fill in where they stand, and nothing below them moves.
    /// </summary>
    private int AddFieldLines(RouteTarget routeTarget, int questSteps, List<Line> lines)
    {
        if (routeTarget.FlyingTerritory == 0 || Flight?.Invoke()?.ZoneFor(routeTarget.FlyingTerritory) is not { } zone || zone.FieldCurrentIds.Count == 0)
        {
            return 0;
        }

        if (!fieldPlaces.TryGetValue(zone.TerritoryId, out var warm))
        {
            var read = FieldPlaces;
            warm = new WarmedValue<IReadOnlyList<AetherCurrentPlace>>(() => read?.Invoke(zone) ?? []);
            fieldPlaces[zone.TerritoryId] = warm;
            _ = warm.Start();
        }

        var attuned = Attuned ?? (static _ => null);
        Func<uint, bool?> attunedNow = id => session.IsLive ? attuned(id) : null;
        if (!warm.IsDone)
        {
            builtFieldPending = warm;
            var pending = FieldCurrentStops.Pending(zone.FieldCurrentIds, attunedNow);
            for (var i = 0; i < pending.Count; i++)
            {
                lines.Add(new Line(LineKind.Field, pending[i].AetherCurrentId, QuestState.Unknown, Strings.RouteFieldCurrentReading)
                {
                    Number = (questSteps + i + 1).ToString(CultureInfo.CurrentCulture) + ".",
                    Detail = Strings.RouteFieldMark,
                    Tooltip = Strings.RouteFieldCurrentReading + "\n" + Strings.RouteFieldTooltip,
                });
            }

            return pending.Count;
        }

        var placed = warm.Value ?? [];
        var placeOf = new Dictionary<uint, AetherCurrentPlace>(placed.Count);
        foreach (var place in placed)
        {
            placeOf[place.AetherCurrentId] = place;
        }

        var all = new List<FieldCurrentStop>(zone.FieldCurrentIds.Count);
        foreach (var id in zone.FieldCurrentIds)
        {
            var nearest = placeOf.TryGetValue(id, out var at) ? links.Aetherytes.Nearest(at.TerritoryId, at.X, at.Z) : null;
            all.Add(new FieldCurrentStop(id, nearest?.RowId ?? 0, nearest?.Name ?? string.Empty));
        }

        var stops = FieldCurrentStops.Left(all, attunedNow);
        for (var i = 0; i < stops.Count; i++)
        {
            var stop = stops[i];
            var text = stop.AetheryteName.Length > 0
                ? string.Format(CultureInfo.CurrentCulture, Strings.RouteFieldCurrentFormat, stop.AetheryteName)
                : Strings.RouteFieldCurrentUnplaced;
            var place = placeOf.TryGetValue(stop.AetherCurrentId, out var spot) ? spot : null;
            lines.Add(new Line(LineKind.Field, stop.AetherCurrentId, QuestState.Unknown, text)
            {
                Number = (questSteps + i + 1).ToString(CultureInfo.CurrentCulture) + ".",
                Detail = Strings.RouteFieldMark,
                Tooltip = text + "\n" + Strings.RouteFieldTooltip,
                FieldTerritory = place?.TerritoryId ?? 0,
                FieldPosition = place is null ? null : new Vector3(place.X, place.Y, place.Z),
            });
        }

        return stops.Count;
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

        /// <summary>A field aether current on a route to flying (K3): "Aether current · Yedlihmad"; <see cref="Line.RowId"/> is the AetherCurrent row.</summary>
        Field,
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

        /// <summary>The C7 clear badges of the duty the step involves (1.19.0); empty for none.</summary>
        public DutyBadges.Look[] Badges { get; init; } = [];

        /// <summary>A field current's place, for its Flag; 0 and null when the game's layout does not place it.</summary>
        public uint FieldTerritory { get; init; }

        public Vector3? FieldPosition { get; init; }
    }

    private sealed record View(UnlockRoute Route, string Title, string Caption, string Summary, string AlsoUnlockedBy, Line[] Lines)
    {
        /// <summary>The field currents left on a route to flying (K3); 0 for any other route.</summary>
        public int FieldLeft { get; init; }

        /// <summary>"10 stops" beside the title of a route to an unlock (K3): the quests and field currents left; empty otherwise.</summary>
        public string Stops { get; init; } = string.Empty;

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
