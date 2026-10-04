using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Seasonal;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Todo;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Todo overlay (feature plan V2-13), the surface read while playing (game UX panel finding 5, accessibility A6/A8):
/// a small always-visible panel with one section per enabled part of <see cref="TodoList"/> (pins in the order they were
/// pinned, the first <see cref="TodoList.MaxPinned"/> then a "+N more" line that opens the Journal on them, the quests of the
/// seasonal events running now with the Lodestone's end date when curated data has one, unlock quests startable here,
/// the next main scenario quest, the current job's next job and role quest). It draws on the Night
/// chrome (<see cref="Theme.PushNightWindow"/>) at <see cref="Configuration.TodoOverlayOpacity"/>, and every text is
/// outlined in the window colour (<see cref="Chrome.OutlinedText"/>) so it reads over snow, sand and sky; hints are in
/// the secondary tone (Mist, 8.7 : 1 against its outline), moons never under 14 px
/// (<see cref="UiMetrics.PlayingGlyphSize"/>). Section headers are outlined captions over a hairline, click to fold.
///
/// Rows: the state moon, the quest name and its hint, and a "…" button that opens the same menu as a right-click
/// (Reveal in Tsukimichi, Flag, Teleport through Lifestream, Link in chat). A click shows the quest in the main window;
/// the map is flagged only on a double-click or from the menu, never on a single click. Compact mode
/// (<see cref="Configuration.TodoOverlayCompact"/>) keeps the moon and the name on one line at a fixed width.
///
/// Locked (<see cref="Configuration.TodoOverlayLocked"/>) is click-through: NoMove, NoResize and NoInputs, so the game
/// behind gets every click; the lock is set from the title's menu (or its "…") and cleared from Settings. Unlocked and
/// nearly invisible (opacity under <see cref="TodoClickThrough.NearlyInvisibleOpacity"/>), it lets clicks through too,
/// except over its rows, captions and title line, or while a modifier key is held (<see cref="TodoClickThrough"/>). The window is
/// not drawn while logged out, in a duty or in a cutscene. Rows are rebuilt on <see cref="SessionState.Changed"/>,
/// <see cref="IClientState.TerritoryChanged"/>, when a section toggle flips and when the viewed character's pins change
/// (<see cref="QueryRunner.PinsVersion"/>: a pin here, or another client's save merged in; the draw thread reads no
/// file); drawing allocates nothing. <see cref="ResetPosition"/>
/// moves the panel back to the top left on the next frame.
///
/// 1.6.0: the followed route's section ("Route: everything for Dragoon", <see cref="ActiveRoutes"/>) comes first, with
/// its next three steps, the level gate line, Flag next stop and Stop, and a "+N more" line that opens the route;
/// Next stops (<see cref="NextStops"/>, off by default) lists one row per stop standing on its first quest; and a
/// right-click on the Pinned caption offers a route through every pin (<see cref="OpenRoute"/>).
/// Questionable (feature plan v5, 1.6.0): while it runs, a gold "Questionable: running · &lt;quest&gt; · step 3 of 7"
/// in the title's own line, cut to the room it has, with a Stop beside the title's "…" (polled at most once a second,
/// only while the panel draws), its quest's name in gold in the rows, and "Send pins to Questionable" in the title's
/// menu (<see cref="QuestionableActions"/>).
///
/// 1.12.0 (feature plan v6, U4: the panel never moves under the player): the Questionable status no longer adds a line
/// under the title, and the text column only grows during a session; it gives back room only after
/// <see cref="ShrinkAfterSeconds"/> without a change, and never while the pointer is on the panel.
///
/// 1.13.0 (feature plan v6 M3, a calmer overlay): it fades in when it opens; it can step aside in combat, while talking
/// to NPCs or in group pose (<see cref="Configuration.TodoHiding"/>, each off by default), fading out and back over
/// <see cref="MotionTokens.Rise"/> and taking no clicks meanwhile; its opacity goes down to 0 (the text is outlined); a
/// finished quest's row stays a moment as a ghost (its moon fills, one soft halo, then it fades out where it stood);
/// new rows fade in; and the rows' "…" buttons show only while the pointer or the keyboard is on the panel. Nothing
/// moves under Reduce motion, and no beat plays in combat.
///
/// 1.18.0 (feature plan v7 A2): the "Why it stopped" card sits in the title's line too, in place of the Questionable
/// status while it is up, so the rows never move for it; and while a hand-off other than Questionable runs, a Stop all
/// stands where Questionable's Stop would.
/// </summary>
public sealed class TodoOverlay : Window, IDisposable
{
    /// <summary>The lowest background opacity: none at all (1.13.0); every text is outlined, so it still reads.</summary>
    public const float MinOpacity = 0f;
    public const float MaxOpacity = 1f;

    /// <summary>Logical minimum width of the panel.</summary>
    private const float MinWidthLogical = 180f;

    /// <summary>How long the text column waits, unchanged and not hovered, before it narrows to what the rows need.</summary>
    private const double ShrinkAfterSeconds = 3.0;

    /// <summary>Logical width of the panel in Compact mode (fixed; long names are clipped).</summary>
    private const float CompactWidthLogical = 260f;

    /// <summary>The row's selectable has no visible label; the name is painted over it (with its outline) instead.</summary>
    private const string RowSelectableId = "##row";

    /// <summary>The row menu's popup id, shared by the right-click and the "…" button (same id scope, the row's).</summary>
    private const string RowMenuId = "##todoRowMenu";

    private const string HeaderMenuId = "##todoHeaderMenu";

    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize
                                               | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoDocking;

    /// <summary>Locked = click-through (accessibility A8): nothing of the panel takes the mouse or keyboard.</summary>
    private const ImGuiWindowFlags LockedFlags = BaseFlags | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoInputs;

    /// <summary>Offset from the main viewport's work area the panel returns to on <see cref="ResetPosition"/>, in logical pixels.</summary>
    private static readonly Vector2 DefaultOffset = new(24f, 96f);

    private static readonly string LockGlyph = Chrome.Icon(FontAwesomeIcon.Lock);
    private static readonly string MoreGlyph = Chrome.Icon(FontAwesomeIcon.EllipsisH);
    private static readonly string FoldedGlyph = Chrome.Icon(FontAwesomeIcon.CaretRight);
    private static readonly string OpenGlyph = Chrome.Icon(FontAwesomeIcon.CaretDown);

    /// <param name="Name">The quest's name as the spoiler shield prints it.</param>
    /// <param name="Ghost">A row whose quest was just completed, kept for the completion beat (<see cref="TodoBeat"/>): drawn, not clickable.</param>
    /// <param name="Avatar">A Next stops row: its first quest's giver shows as a 24 px avatar before the name (1.15, F5).</param>
    private readonly record struct Row(QuestRecord Quest, string Name, QuestState State, string Hint, string Tooltip, bool Ghost = false, bool Avatar = false);

    /// <summary>
    /// <paramref name="HeaderText"/> is the caption; <paramref name="ToggleTooltip"/> says a click folds it;
    /// <paramref name="Notes"/> are lines under the caption ("Ends Aug 28 (Lodestone)"), not drawn in Compact mode;
    /// <paramref name="MoreText"/> is the "+N more" line under the rows of a capped section, null when none was left out.
    /// </summary>
    private sealed record SectionView(TodoSection Section, string HeaderText, string ToggleTooltip, string[] Notes, Row[] Rows, string? MoreText);

    private readonly Configuration settings;
    private readonly SessionState session;
    private readonly GameLinks links;
    private readonly Action<QuestRecord> reveal;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly QueryRunner pins;
    private readonly IDalamudPluginInterface pluginInterface;

    private SectionView[] sections = [];
    private int enabledSections;
    private bool catalogReady;
    private bool dirty = true;
    private int builtVersion = -1;
    private int builtPins = -1;
    private uint builtTerritory;
    private int builtSettings = -1;

    // Folded sections, by TodoSection value (not persisted; the old collapsing headers were not either).
    private readonly bool[] folded = new bool[Enum.GetValues<TodoSection>().Length + 1];

    // Pins: the viewed character's pins in the order they were pinned, copied from the query runner per PinsVersion.
    private readonly List<uint> pinned = [];
    private int pinsCopied = -1;

    private JobLadder ladder = JobLadder.Empty;
    private CatalogBundle? ladderBundle;
    private bool resetPosition;
    private bool disposed;
    private Theme.StyleScope nightChrome;

    // The text column's width as shown, the width the rows asked for last frame, and when that last changed (ImGui time).
    private float heldText;
    private float wantedText = -1f;
    private double wantedSince;

    // A clicked row whose reveal waits out the double-click window (ImGui time of the click); see DrawRow.
    private QuestRecord? pendingReveal;
    private double pendingRevealTime;

    // 1.13.0 (M3): the panel's own fade (it opens, or steps aside), the frame it last asked to be drawn, whether it is
    // stepping aside now, the rows' "…" buttons' fade, and per quest when its ghost row's beat or its new row's fade-in
    // started (ImGui time), with the character the rows were built for.
    private float shown;
    private int shownFrame = -10;
    private bool steppingAside;
    private float moreShown;
    private readonly Dictionary<uint, double> ghostSince = [];
    private readonly Dictionary<uint, double> arrivedAt = [];
    private readonly List<(int Index, uint RowId)> ghostScratch = [];
    private ulong? builtCharacter;

    // Where the panel takes the pointer, recorded as it draws (the title line, section captions, rows with their "…",
    // the "+N more" lines and the route buttons): PreDraw reads the last frame's to let a nearly invisible panel pass
    // every other click through (TodoClickThrough).
    private readonly List<ScreenRect> targets = [];

    /// <summary>The host name of this window's Questionable confirmations.</summary>
    private const string QuestionableHost = "todo";

    /// <summary>The shared Questionable hand-offs (1.6.0); null hides the status line and the menu item.</summary>
    public QuestionableActions? Questionable { get; set; }

    /// <param name="settings">Overlay settings; read every frame so the config window's changes show at once.</param>
    /// <param name="session">Catalog, viewed character and its evaluations.</param>
    /// <param name="links">Map flags, Lifestream teleports and chat links.</param>
    /// <param name="reveal">Shows a quest in the main window (open, bring to front, select).</param>
    /// <param name="clientState">Login state and the current territory.</param>
    /// <param name="condition">Duty and cutscene flags that hide the panel.</param>
    /// <param name="pins">The viewed character's pins, as the query runner holds them (its saves and other clients' merges included).</param>
    /// <param name="pluginInterface">Saves the settings the panel's own menu changes.</param>
    public TodoOverlay(
        Configuration settings,
        SessionState session,
        GameLinks links,
        Action<QuestRecord> reveal,
        IClientState clientState,
        ICondition condition,
        QueryRunner pins,
        IDalamudPluginInterface pluginInterface)
        : base(Strings.TodoWindowTitle, BaseFlags)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.reveal = reveal ?? throw new ArgumentNullException(nameof(reveal));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
        this.pins = pins ?? throw new ArgumentNullException(nameof(pins));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ShowCloseButton = false;
        AllowPinning = false;
        AllowClickthrough = false;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(MinWidthLogical, 0f),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        session.Changed += OnSessionChanged;
        session.DataDeleted += MarkDirty;
        clientState.TerritoryChanged += OnTerritoryChanged;
        IsOpen = settings.TodoOverlayEnabled;
    }

    /// <summary>Flips <see cref="Configuration.TodoOverlayEnabled"/> and saves; the window follows on the next frame (<c>/tsuki todo</c>).</summary>
    public void ToggleEnabled()
    {
        settings.TodoOverlayEnabled = !settings.TodoOverlayEnabled;
        Save();
    }

    /// <summary>Moves the panel back to its default place on the next frame (the settings window's "Reset position").</summary>
    public void ResetPosition() => resetPosition = true;

    /// <summary>Rebuilds the rows on the next frame, e.g. after a setting outside the per-frame signature changed.</summary>
    public void MarkDirty() => dirty = true;

    /// <summary><see cref="Configuration.TodoOverlayOpacity"/> within the allowed bounds; a corrupt value reads as the default.</summary>
    public static float ClampOpacity(float opacity) => float.IsFinite(opacity) ? Math.Clamp(opacity, MinOpacity, MaxOpacity) : 0.85f;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        session.Changed -= OnSessionChanged;
        session.DataDeleted -= MarkDirty;
        clientState.TerritoryChanged -= OnTerritoryChanged;
    }

    public override void PreOpenCheck()
    {
        IsOpen = settings.TodoOverlayEnabled;
    }

    /// <summary>
    /// Not drawn while logged out, bound by a duty or watching a cutscene. Otherwise it fades in when it opens and, with
    /// a hiding option on, fades out while the player is in combat, talking to an NPC or in group pose (M3), and is not
    /// drawn once faded out.
    /// </summary>
    public override bool DrawConditions()
    {
        if (!clientState.IsLoggedIn
            || condition[ConditionFlag.BoundByDuty]
            || condition[ConditionFlag.WatchingCutscene]
            || condition[ConditionFlag.OccupiedInCutSceneEvent])
        {
            shown = 0f;
            return false;
        }

        // Not asked for last frame (just opened, or back from a duty): it fades in from nothing.
        var frame = ImGui.GetFrameCount();
        if (frame - shownFrame > 1)
        {
            shown = 0f;
        }

        shownFrame = frame;
        steppingAside = settings.TodoHiding().Hides(new TodoContext(
            condition[ConditionFlag.InCombat],
            condition[ConditionFlag.OccupiedInQuestEvent] || condition[ConditionFlag.OccupiedInEvent],
            clientState.IsGPosing));
        var target = steppingAside ? 0f : 1f;
        shown = Motion.WorldEnabled ? MotionMath.Approach(shown, target, MotionTokens.RateFor(MotionTokens.Rise), ImGui.GetIO().DeltaTime) : target;
        return shown > 0f || target > 0f;
    }

    public override void PreDraw()
    {
        Flags = settings.TodoOverlayLocked ? LockedFlags : BaseFlags;
        if (steppingAside)
        {
            // Stepping aside: the game behind takes every click while the panel fades out.
            Flags |= ImGuiWindowFlags.NoInputs;
        }
        else if (!settings.TodoOverlayLocked && PassesClicks())
        {
            // Nearly invisible: the game behind takes the clicks the panel's rows and captions do not.
            Flags |= ImGuiWindowFlags.NoInputs;
        }

        // The overlay's own opacity setting; the Night chrome keeps this alpha (it never touches BgAlpha).
        BgAlpha = ClampOpacity(settings.TodoOverlayOpacity);
        var compactWidth = CompactWidthLogical * UiMetrics.UiScale;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(settings.TodoOverlayCompact ? compactWidth : MinWidthLogical * UiMetrics.UiScale, 0f),
            MaximumSize = new Vector2(settings.TodoOverlayCompact ? compactWidth : float.MaxValue, float.MaxValue),
        };
        if (resetPosition)
        {
            resetPosition = false;
            ImGui.SetNextWindowPos(ImGuiHelpers.MainViewport.WorkPos + DefaultOffset * ImGuiHelpers.GlobalScale, ImGuiCond.Always);
        }

        Refresh();
        nightChrome = Theme.PushNightWindow();
    }

    public override void PostDraw()
    {
        nightChrome.Dispose();
        nightChrome = default;
    }

    /// <summary>
    /// Whether the unlocked panel lets the pointer through this frame (<see cref="TodoClickThrough"/>): it is nearly
    /// invisible, the pointer is on none of last frame's targets and no modifier key is held.
    /// </summary>
    private bool PassesClicks()
    {
        var io = ImGui.GetIO();
        return TodoClickThrough.PassesClicks(
            ClampOpacity(settings.TodoOverlayOpacity),
            TodoClickThrough.Hits(CollectionsMarshal.AsSpan(targets), io.MousePos),
            io.KeyCtrl || io.KeyShift || io.KeyAlt);
    }

    /// <summary>Records the last item as a place the panel takes the pointer.</summary>
    private void NoteTarget() => targets.Add(new ScreenRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax()));

    public override void Draw()
    {
        // The panel is its own top-level window, so it scales itself (the glyphs already followed the icon scale;
        // the text now keeps pace). Reset before Begin lays the window out again.
        UiMetrics.ApplyFontScale();
        try
        {
            DrawContent();
            Questionable?.DrawModals(QuestionableHost);
        }
        finally
        {
            ImGui.SetWindowFontScale(1f);

            // The panel's fade (opening, stepping aside): every vertex of its window, frame, outlines and moons alike.
            if (shown < 1f)
            {
                Chrome.FadeVertices(ImGui.GetWindowDrawList(), 0, MotionMath.EaseOutCubic(shown));
            }
        }
    }

    private void DrawContent()
    {
        targets.Clear();
        FireDueReveal();
        var compact = settings.TodoOverlayCompact;
        var layout = Measure(compact);

        // The rows' "…" buttons show only while the pointer or the keyboard is on the panel (M3), fading in and out.
        var reaching = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByPopup) || ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows);
        moreShown = Motion.WorldEnabled
            ? MotionMath.ApproachAsym(moreShown, reaching ? 1f : 0f, MotionMath.HoverRate, MotionMath.HoverOutRate, ImGui.GetIO().DeltaTime)
            : reaching ? 1f : 0f;
        DrawHeader(layout);
        if (!catalogReady)
        {
            Chrome.OutlinedText(session.CatalogLoading ? Strings.CatalogNotReady : Strings.CatalogUnavailable, Theme.Surface.TextSecondary);
            return;
        }

        // A New Game+ session runs (1.19.0, C4): the status bar's line, above the sections.
        if (session.Bundle is { } replayBundle && newGamePlusText.Line(session, replayBundle) is { Length: > 0 } replayLine)
        {
            Chrome.OutlinedText(replayLine, Theme.Surface.Text);
        }

        // The journal count (1.19.0, C9), when asked for: the status bar's words from 25 slots used.
        DrawJournalCount();

        if (sections.Length == 0)
        {
            Chrome.OutlinedText(enabledSections > 0 ? Strings.TodoEmpty : Strings.TodoNoSections, Theme.Surface.TextSecondary);
            return;
        }

        for (var s = 0; s < sections.Length; s++)
        {
            var section = sections[s];
            using var id = ImRaii.PushId((int)section.Section);
            var open = compact || !folded[(int)section.Section];
            if (compact)
            {
                // Compact: no captions, a hairline between sections.
                if (s > 0)
                {
                    Chrome.Hairline(width: layout.RowWidth);
                }
            }
            else
            {
                DrawSectionHeader(section, layout, open);
            }

            if (!open)
            {
                continue;
            }

            if (!compact)
            {
                foreach (var note in section.Notes)
                {
                    Chrome.OutlinedText(note, Theme.Surface.TextSecondary);
                }

                if (section.Section == TodoSection.Route && !settings.TodoOverlayLocked)
                {
                    DrawRouteActions();
                }
            }

            for (var i = 0; i < section.Rows.Length; i++)
            {
                var row = section.Rows[i];
                if (row.Ghost)
                {
                    DrawGhostRow(row, i, layout);
                }
                else
                {
                    DrawRow(row, i, layout, compact);
                }
            }

            if (section.MoreText is { } moreText)
            {
                DrawMoreLine(moreText, layout, section.Section == TodoSection.Route ? ShowFollowedRoute : ShowPins);
            }
        }
    }

    /// <summary>
    /// The route section's buttons (1.6.0): "Flag next stop" opens the map on the next step's giver, "Stop" stops
    /// following the route (the pins and the route itself are untouched).
    /// </summary>
    private void DrawRouteActions()
    {
        if (ActiveRoutes is not { } routes)
        {
            return;
        }

        var route = routes.ViewedRoute;
        var stop = routes.NextStopQuest(route);
        var canFlag = stop is not null && links.CanFlagMap(stop);
        if (TravelControls.FlagButton(Strings.RouteFlagNextStop, canFlag, "##flagNextStop"))
        {
            routes.FlagNextStop(route);
        }

        NoteTarget();

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(canFlag ? Strings.RouteFlagNextStopTooltip : Strings.RouteFlagNextStopUnavailable);
        }

        ImGui.SameLine();
        if (Chrome.ActionPill("##routeStop", ActionGlyphs.Stop, Strings.TodoRouteStop, PillTone.Danger, true, size: PillLayout.Row))
        {
            routes.Stop();
            dirty = true;
        }

        NoteTarget();

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.RouteStopFollowingTooltip);
        }
    }

    /// <summary>
    /// "+52 more" under a capped section's rows, in the name column and the secondary tone. A click runs
    /// <paramref name="open"/>: the main window on the Journal filtered to the pins (<see cref="ShowPins"/>), or the
    /// followed route's window (<see cref="ShowFollowedRoute"/>); while locked it is plain text.
    /// </summary>
    private void DrawMoreLine(string text, in RowLayout layout, Action? open)
    {
        var start = ImGui.GetCursorScreenPos();
        var textX = start.X + layout.Glyph + ImGui.GetStyle().ItemSpacing.X;
        var line = ImGui.GetTextLineHeight();
        ImGui.SetCursorScreenPos(new Vector2(textX, start.Y));
        var clicked = ImGui.InvisibleButton("##moreLine", new Vector2(layout.TextWidth, line));
        NoteTarget();
        var hovered = ImGui.IsItemHovered();
        Chrome.FocusRing();
        if (clicked)
        {
            open?.Invoke();
        }

        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(new Vector2(textX, start.Y), new Vector2(textX + layout.TextWidth, start.Y + line), true);
        Chrome.OutlinedTextAt(dl, new Vector2(textX, start.Y), text, Theme.U32(hovered ? Theme.Surface.Text : Theme.Surface.TextSecondary));
        dl.PopClipRect();
        if (hovered)
        {
            UiMetrics.Tooltip(ReferenceEquals(open, ShowPins) ? Strings.TodoMoreTooltip : Strings.TodoRouteMoreTooltip);
        }
    }

    /// <summary>Per-frame layout: the moon box, the row height (never under the "…" target) and the text column's width.</summary>
    private readonly record struct RowLayout(float Glyph, float RowHeight, float TextWidth, float RowWidth);

    /// <summary>
    /// The text column is as wide as the widest name (and hint) this frame, so every "…" lines up in one column; in
    /// Compact mode it is what the fixed width leaves. Allocation-free: a few dozen CalcTextSize calls.
    /// </summary>
    private RowLayout Measure(bool compact)
    {
        var line = ImGui.GetTextLineHeight();
        var glyph = UiMetrics.PlayingGlyphSize(line);
        var button = UiMetrics.MinTarget;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var rowHeight = MathF.Max(glyph, button);
        float text;
        if (compact)
        {
            text = MathF.Max(UiMetrics.Px(40f), ImGui.GetContentRegionAvail().X - glyph - button - 2f * spacing);
        }
        else
        {
            text = ImGui.CalcTextSize(Strings.TodoHeader).X;

            // A Moon Road caption also holds its sigil (the rule takes only what is left).
            var caption = ImGui.GetFontSize() + (Theme.MoonRoadArt ? UiMetrics.Px(HeadingLayout.SigilLogical + HeadingLayout.SigilGapLogical) : 0f);
            foreach (var section in sections)
            {
                text = MathF.Max(text, ImGui.CalcTextSize(section.HeaderText).X + caption);
                foreach (var note in section.Notes)
                {
                    text = MathF.Max(text, ImGui.CalcTextSize(note).X);
                }

                if (section.MoreText is { } more)
                {
                    text = MathF.Max(text, ImGui.CalcTextSize(more).X);
                }

                foreach (var row in section.Rows)
                {
                    var width = ImGui.CalcTextSize(row.Name).X + AvatarRoom(row, rowHeight);
                    if (row.Hint.Length > 0)
                    {
                        width += spacing + ImGui.CalcTextSize(row.Hint).X;
                    }

                    text = MathF.Max(text, width);
                }
            }
        }

        text = compact ? text : HeldWidth(text);
        return new RowLayout(glyph, rowHeight, text, glyph + spacing + text + spacing + button);
    }

    /// <summary>
    /// The width ratchet: wider at once, narrower only once the rows have asked for less for
    /// <see cref="ShrinkAfterSeconds"/> and the pointer is not on the panel, so a zone change or a finished quest does not
    /// pull the "…" column out from under the eye.
    /// </summary>
    private float HeldWidth(float wanted)
    {
        var now = ImGui.GetTime();
        if (wanted != wantedText)
        {
            wantedText = wanted;
            wantedSince = now;
        }

        if (wanted >= heldText)
        {
            heldText = wanted;
        }
        else if (now - wantedSince >= ShrinkAfterSeconds && !ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByPopup))
        {
            heldText = wanted;
        }

        return heldText;
    }

    /// <summary>
    /// "☾ Tsukimichi" outlined in the primary tone, a lock glyph while locked, Questionable's status while it runs, and a
    /// "…" with the overlay's options (the same menu as a right-click on the title): Lock (click-through), Compact,
    /// Reset position, Hide.
    /// </summary>
    private void DrawHeader(in RowLayout layout)
    {
        var start = ImGui.GetCursorScreenPos();
        targets.Add(new ScreenRect(start, start + new Vector2(layout.RowWidth, layout.RowHeight)));
        var textY = start.Y + (layout.RowHeight - ImGui.GetTextLineHeight()) * 0.5f;
        ImGui.SetCursorScreenPos(new Vector2(start.X, textY));
        Chrome.OutlinedText(Strings.TodoHeader, Theme.Surface.Text);
        var openMenu = ImGui.IsItemHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right);

        if (settings.TodoOverlayLocked)
        {
            ImGui.SameLine();
            ImGui.SetWindowFontScale(0.75f * UiMetrics.FontScale);
            ImGui.PushFont(UiBuilder.IconFont);
            Chrome.OutlinedText(LockGlyph, Theme.Surface.TextSecondary);
            ImGui.PopFont();
            UiMetrics.ApplyFontScale();
        }

        var titleEnd = ImGui.GetItemRectMax().X;
        DrawHeaderStatus(start, titleEnd, textY, layout);
        if (!settings.TodoOverlayLocked)
        {
            // The "…" sits in the rows' button column; while locked nothing can be clicked, so it is not drawn.
            ImGui.SetCursorScreenPos(new Vector2(start.X + layout.RowWidth - UiMetrics.MinTarget, start.Y));
            openMenu |= Chrome.IconButtonRound("##headerMore", MoreGlyph, Strings.TodoHeaderMoreTooltip);
        }

        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + layout.RowHeight));
        ImGui.Dummy(Vector2.Zero);
        if (openMenu)
        {
            ImGui.OpenPopup(HeaderMenuId);
        }

        DrawHeaderMenu();
    }

    private void DrawHeaderMenu()
    {
        if (!ImGui.IsPopupOpen(HeaderMenuId))
        {
            return;
        }

        using var style = Theme.PushPopup();
        using var popup = ImRaii.Popup(HeaderMenuId);
        if (!popup)
        {
            return;
        }

        // Only reachable while unlocked: once locked the panel takes no input, and Settings unlocks it.
        if (ImGui.MenuItem(Strings.TodoMenuLock, string.Empty, settings.TodoOverlayLocked))
        {
            settings.TodoOverlayLocked = true;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.TodoMenuLockTooltip);
        }

        if (ImGui.MenuItem(Strings.TodoMenuCompact, string.Empty, settings.TodoOverlayCompact))
        {
            settings.TodoOverlayCompact = !settings.TodoOverlayCompact;
            Save();
        }

        if (ImGui.MenuItem(Strings.TodoMenuResetPosition))
        {
            ResetPosition();
        }

        if (ImGui.MenuItem(Strings.TodoMenuHide))
        {
            settings.TodoOverlayEnabled = false;
            Save();
        }

        if (AutomationGate.Questionable(Questionable) is { } questionable)
        {
            // The viewed character's pins, in the order they were pinned.
            ImGui.Separator();
            questionable.DrawSubmenu(QuestionableHost, Strings.QuestionableSendPins, pinned, static pins => pins);
        }
    }

    /// <summary>
    /// The title's line after the title (1.12.0, U4: never a line of its own; the panel is not widened for it): the
    /// "Why it stopped" card while one is up (1.18, A2: its cue dot, title, first fix and ×, so the quest rows never move
    /// when it comes or goes), else Questionable's live status while it runs, outlined in gold and cut to the room, the
    /// whole text on hover. Then a small Stop left of the "…": Questionable's while it runs, else Stop all while another
    /// hand-off runs (a walk, a craft, an AutoDuty run), whatever the automation level shows. Not while locked: the
    /// panel takes no clicks then.
    /// </summary>
    private void DrawHeaderStatus(Vector2 start, float titleEnd, float textY, in RowLayout layout)
    {
        var stops = RunStops;
        var card = stops?.Dock.Current;
        var questionable = Questionable;
        var text = questionable?.PollStatusText();
        var stopAll = text is null && stops is { AnyRunning: true } ? StopCardView.StopAll(stops) : null;

        if (card is null && text is null && stopAll is null)
        {
            return;
        }

        var style = ImGui.GetStyle();
        var locked = settings.TodoOverlayLocked;
        var right = start.X + layout.RowWidth - (locked ? 0f : UiMetrics.MinTarget + style.ItemSpacing.X);
        var showStop = !locked && (text is not null || stopAll is not null);
        var stopLabel = text is not null ? Strings.QuestionableStopShort : Strings.ActionStopShort;
        var stopWidth = showStop ? ImGui.CalcTextSize(stopLabel).X + (style.FramePadding.X * 2f) : 0f;
        var textX = titleEnd + style.ItemSpacing.X;
        var room = right - textX - (showStop ? stopWidth + style.ItemSpacing.X : 0f);
        if (card is not null && stops is not null)
        {
            DrawStopCardInline(stops, card, textX, textX + MathF.Max(0f, room), start.Y, textY, layout.RowHeight, locked);
        }
        else if (text is not null && room >= UiMetrics.Px(32f))
        {
            // Without room beside the title, the Stop alone still shows Questionable is running.
            var line = ImGui.GetTextLineHeight();
            var dl = ImGui.GetWindowDrawList();
            var textMin = new Vector2(textX, textY);
            Chrome.OutlinedEllipsisAt(dl, textMin, room, text, Theme.AccentU32);
            if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(textMin, textMin + new Vector2(room, line)))
            {
                UiMetrics.Tooltip(text, Strings.QuestionableStatusTooltip);
            }
        }

        if (!showStop || right - stopWidth < titleEnd)
        {
            return;
        }

        ImGui.SetCursorScreenPos(new Vector2(right - stopWidth, textY));
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(style.FramePadding.X, 0f)))
        {
            if (text is not null && questionable is not null)
            {
                questionable.DrawStopSmallButton(QuestionableHost, "##questionableStop");
            }
            else if (stopAll is not null)
            {
                if (ImGui.SmallButton(stopLabel + "##stopAll"))
                {
                    stopAll();
                }

                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.NeedsYouStopAllTooltip);
                }
            }
        }
    }

    /// <summary>
    /// The "Why it stopped" card in the title's line between <paramref name="left"/> and <paramref name="right"/>,
    /// fading with the card; under the pointer (and not locked) it holds the card's clock.
    /// </summary>
    private void DrawStopCardInline(Game.RunStops stops, Core.Companions.StopCard card, float left, float right, float top, float textY, float rowHeight, bool locked)
    {
        var dl = ImGui.GetWindowDrawList();
        var start = dl.VtxBuffer.Size;
        var hovered = StopCardView.DrawInline(stops, card, left, right, top, rowHeight, textY, buttons: !locked, QuestionableHost);
        var alpha = stops.Dock.Alpha(stops.Now, UiMetrics.ReduceMotion);
        if (alpha < 1f)
        {
            Chrome.FadeVertices(dl, start, alpha);
        }

        stops.NoteShown(hovered && !locked);
    }

    /// <summary>A section caption (caret, name and count) outlined in the secondary tone over a hairline; a click folds it.</summary>
    private void DrawSectionHeader(SectionView section, in RowLayout layout, bool open)
    {
        var start = ImGui.GetCursorScreenPos();
        var line = ImGui.GetTextLineHeight();
        if (ImGui.InvisibleButton("##section", new Vector2(layout.RowWidth, line)))
        {
            folded[(int)section.Section] = open;
        }

        NoteTarget();

        var hovered = ImGui.IsItemHovered();
        // The pins' caption has one menu (1.6.0): a route through every pin, and Send pins to Questionable.
        var pinsMenu = section.Section == TodoSection.Pinned && (OpenRoute is not null || AutomationGate.Questionable(Questionable) is not null);
        if (pinsMenu && hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
        {
            ImGui.OpenPopup(PinsMenuId);
        }

        Chrome.FocusRing();

        var dl = ImGui.GetWindowDrawList();
        var ink = Theme.U32(hovered ? Theme.Surface.Text : Theme.Surface.TextSecondary);
        ImGui.PushFont(UiBuilder.IconFont);
        Chrome.OutlinedTextAt(dl, start, open ? OpenGlyph : FoldedGlyph, ink);
        ImGui.PopFont();

        // Moon Road (R3 #9): the sigil, the caption outlined and a brass rule to the row's end, faded with the panel's
        // own opacity, instead of the hairline under the caption; Plain keeps the hairline.
        var textMin = new Vector2(start.X + ImGui.GetFontSize(), start.Y);
        var room = layout.RowWidth - ImGui.GetFontSize();
        var ruleAlpha = Ornament.RuleAlpha * ClampOpacity(settings.TodoOverlayOpacity);
        var moonRoad = SectionHeading.DrawInRow(dl, textMin, room, line, section.HeaderText, ink, out var cut, eyebrow: false, outlined: true, ruleAlpha: ruleAlpha);
        if (!moonRoad)
        {
            cut = Chrome.OutlinedEllipsisAt(dl, textMin, room, section.HeaderText, ink);
        }

        if (hovered)
        {
            var hint = pinsMenu ? Strings.TodoPinsMenuHint : null;
            UiMetrics.Tooltip(cut ? section.HeaderText : section.ToggleTooltip, cut ? (hint is null ? section.ToggleTooltip : section.ToggleTooltip + "\n" + hint) : hint);
        }

        if (pinsMenu)
        {
            DrawPinsMenu();
        }

        if (!moonRoad)
        {
            Chrome.Hairline(width: layout.RowWidth);
        }
    }

    private const string PinsMenuId = "##todoPinsMenu";

    /// <summary>The Pinned caption's menu: "Route through my pins" opens the route to every pin still to do.</summary>
    private void DrawPinsMenu()
    {
        if (!ImGui.IsPopupOpen(PinsMenuId))
        {
            return;
        }

        using var style = Theme.PushPopup();
        using var popup = ImRaii.Popup(PinsMenuId);
        if (!popup)
        {
            return;
        }

        if (OpenRoute is not null)
        {
            if (ImGui.MenuItem(Strings.TodoRoutePins) && pinned.Count > 0)
            {
                OpenRoute.Invoke(Core.Route.RouteTarget.ForPins(pinned));
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.TodoRoutePinsTooltip);
            }
        }

        // The same "Send pins to Questionable" as the title's menu.
        AutomationGate.Questionable(Questionable)?.DrawSubmenu(QuestionableHost, Strings.QuestionableSendPins, pinned, static pins => pins);
    }

    /// <summary>The room a row's giver avatar and its gap take before the name (Next stops rows, 1.15); 0 for the others or with portraits off.</summary>
    private static float AvatarRoom(in Row row, float rowHeight) =>
        row.Avatar && GiverPortraits.Enabled ? AvatarSide(rowHeight) + UiMetrics.Px(PortraitPlate.AvatarGap) : 0f;

    /// <summary>The avatar's side: 24 px (spec-1.15 A5), never taller than the row.</summary>
    private static float AvatarSide(float rowHeight) => MathF.Round(MathF.Min(UiMetrics.Px(PortraitPlate.AvatarSize), rowHeight));

    private void DrawRow(Row row, int index, in RowLayout layout, bool compact)
    {
        using var id = ImRaii.PushId(index);
        var style = ImGui.GetStyle();
        var start = ImGui.GetCursorScreenPos();
        var rowStart = ImGui.GetWindowDrawList().VtxBuffer.Size;
        var line = ImGui.GetTextLineHeight();
        var textY = start.Y + (layout.RowHeight - line) * 0.5f;
        targets.Add(new ScreenRect(start, start + new Vector2(layout.RowWidth, layout.RowHeight)));

        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + (layout.RowHeight - layout.Glyph) * 0.5f));
        MoonGlyph.DrawInline(row.State, layout.Glyph);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.StateTooltip(row.State, row.Quest), row.Hint);
        }

        // The selectable spans the text column; the name and hint are painted over it with their outline. A click
        // shows the quest in the main window; only a double-click (or the menu) flags the map, a game action with no
        // undo, so a slip of the mouse never plants a flag. The selectable reports the first press of a double-click
        // as a click, so the reveal waits one double-click window (FireDueReveal) and the second press cancels it.
        var textX = start.X + layout.Glyph + style.ItemSpacing.X;
        ImGui.SetCursorScreenPos(new Vector2(textX, start.Y));
        if (ImGui.Selectable(RowSelectableId, false, ImGuiSelectableFlags.AllowDoubleClick, new Vector2(layout.TextWidth, layout.RowHeight)))
        {
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                pendingReveal = null;
                OnRowDoubleClick(row.Quest);
            }
            else
            {
                pendingReveal = row.Quest;
                pendingRevealTime = ImGui.GetTime();
            }
        }

        var hovered = ImGui.IsItemHovered();
        var openMenu = hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Right);
        var dl = ImGui.GetWindowDrawList();
        var textMax = new Vector2(textX + layout.TextWidth, start.Y + layout.RowHeight);
        dl.PushClipRect(new Vector2(textX, start.Y), textMax, true);

        // A Next stops row opens with its first quest's giver (1.15, F5), so you know who to walk up to.
        var avatarRoom = AvatarRoom(row, layout.RowHeight);
        var avatar = avatarRoom > 0f ? AvatarSide(layout.RowHeight) : 0f;
        var avatarMin = new Vector2(textX, start.Y + MathF.Round((layout.RowHeight - avatar) * 0.5f));
        var portrait = avatar > 0f ? GiverPortraits.For(row.Quest, session.Spoilers) : PortraitRequest.None;
        if (avatar > 0f)
        {
            Chrome.Portrait(dl, avatarMin, avatar, portrait);
        }

        // The name ends in an ellipsis in the text column (Compact's fixed width, a panel narrowed by hand) rather than
        // being cut mid-letter, and the hint takes what the name leaves (R3 #5); a cut name heads the tooltip.
        var nameX = textX + avatarRoom;
        var nameWidth = ImGui.CalcTextSize(row.Name).X;
        var fit = LineFit.Fit(MathF.Max(1f, layout.TextWidth - avatarRoom), nameWidth, UiMetrics.Px(LayoutBudgets.RowNameMinLogical), [], [], style.ItemSpacing.X, UiMetrics.Px(24f));
        // Gold while Questionable works on this quest (1.6.0).
        var nameInk = Questionable?.RunningRowId == row.Quest.RowId ? Theme.AccentU32 : Theme.U32(Theme.Surface.Text);
        Chrome.OutlinedEllipsisAt(dl, new Vector2(nameX, textY), fit.NameRoom, row.Name, nameInk, nameWidth);
        if (!compact && row.Hint.Length > 0 && fit.TailRoom > 0f)
        {
            var hintX = nameX + fit.NameDrawn + style.ItemSpacing.X;
            Chrome.OutlinedEllipsisAt(dl, new Vector2(hintX, textY), fit.TailRoom, row.Hint, Theme.U32(Theme.Surface.TextSecondary));
        }

        dl.PopClipRect();
        if (hovered && avatar > 0f && ImGui.IsMouseHoveringRect(avatarMin, avatarMin + new Vector2(avatar)))
        {
            Chrome.PortraitTooltip(portrait, GiverPortraits.Name(row.Quest, session.Spoilers), GiverPortraits.Place(row.Quest, session.Spoilers));
        }
        else if (hovered)
        {
            UiMetrics.Tooltip(fit.NameCut ? row.Name : row.Tooltip, fit.NameCut ? row.Tooltip : null);
        }

        // The "…" opens the same menu as the right-click, for keyboard, controller and one-handed players (A6). While
        // locked the panel is click-through, so the buttons would only be clutter.
        if (!settings.TodoOverlayLocked)
        {
            ImGui.SetCursorScreenPos(new Vector2(textX + layout.TextWidth + style.ItemSpacing.X, start.Y + (layout.RowHeight - UiMetrics.MinTarget) * 0.5f));
            var buttonStart = dl.VtxBuffer.Size;
            openMenu |= Chrome.IconButtonRound("##more", MoreGlyph, Strings.TodoRowMoreTooltip);
            if (moreShown < 1f)
            {
                Chrome.FadeVertices(dl, buttonStart, moreShown);
            }
        }

        // A row that just arrived fades in where it stands (M3).
        if (arrivedAt.Count > 0 && arrivedAt.TryGetValue(row.Quest.RowId, out var arrived))
        {
            var progress = (float)((ImGui.GetTime() - arrived) / MotionTokens.Reveal);
            if (progress < 1f && Motion.WorldEnabled)
            {
                Chrome.FadeVertices(dl, rowStart, MotionMath.EaseOutCubic(progress));
            }
        }

        if (openMenu)
        {
            ImGui.OpenPopup(RowMenuId);
        }

        DrawRowMenu(row);
        // Close the row with an item at its bottom edge, so the next one starts one item spacing below it.
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + layout.RowHeight));
        ImGui.Dummy(Vector2.Zero);
    }

    /// <summary>
    /// A finished quest's ghost row (the completion beat, M3): its moon fills from the half moon to full, one soft halo
    /// swells round it, then the row fades out where it stands (<see cref="TodoBeat.Look"/>). Not clickable; once the beat
    /// is over the rows are rebuilt without it, with no collapse animation.
    /// </summary>
    private void DrawGhostRow(Row row, int index, in RowLayout layout)
    {
        using var id = ImRaii.PushId(index);
        var start = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var rowStart = dl.VtxBuffer.Size;
        var progress = ghostSince.TryGetValue(row.Quest.RowId, out var since) ? (float)((ImGui.GetTime() - since) / MotionTokens.Beat) : 1f;
        if (progress >= 1f)
        {
            // Over: the next frame drops the row.
            dirty = true;
        }

        var (lit, halo, alpha) = TodoBeat.Look(progress);
        var center = new Vector2(start.X + (layout.Glyph * 0.5f), start.Y + (layout.RowHeight * 0.5f));
        var radius = layout.Glyph * MoonGlyph.InlineRadiusFraction;
        MoonGlyph.DrawFilling(dl, center, radius, lit);
        if (halo >= 0f)
        {
            MoonWax.DrawHalo(dl, center, radius, halo);
        }

        var line = ImGui.GetTextLineHeight();
        var textX = start.X + layout.Glyph + ImGui.GetStyle().ItemSpacing.X;
        var textMin = new Vector2(textX, start.Y + ((layout.RowHeight - line) * 0.5f));
        dl.PushClipRect(new Vector2(textX, start.Y), new Vector2(textX + layout.TextWidth, start.Y + layout.RowHeight), true);
        Chrome.OutlinedEllipsisAt(dl, textMin, layout.TextWidth, row.Name, Theme.U32(Theme.Surface.TextSecondary));
        dl.PopClipRect();
        Chrome.FadeVertices(dl, rowStart, alpha);

        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + layout.RowHeight));
        ImGui.Dummy(Vector2.Zero);
    }

    /// <summary>
    /// The single-click reveal, once a double-click window has passed since the click without a second press:
    /// a plain click still opens the main window, a beat later than the press.
    /// </summary>
    private void FireDueReveal()
    {
        if (pendingReveal is not { } quest || ImGui.GetTime() - pendingRevealTime <= ImGui.GetIO().MouseDoubleClickTime)
        {
            return;
        }

        pendingReveal = null;
        reveal(quest);
    }

    /// <summary>Double-click flags the giver; a quest without a mappable giver is shown in the main window instead.</summary>
    private void OnRowDoubleClick(QuestRecord quest)
    {
        if (links.CanFlagMap(quest))
        {
            links.FlagMap(quest);
        }
        else
        {
            reveal(quest);
        }
    }

    private void DrawRowMenu(Row row)
    {
        if (!ImGui.IsPopupOpen(RowMenuId))
        {
            return;
        }

        using var style = Theme.PushPopup();
        using var popup = ImRaii.Popup(RowMenuId);
        if (!popup)
        {
            return;
        }

        var quest = row.Quest;
        if (ImGui.MenuItem(Strings.TodoRevealInTsukimichi))
        {
            reveal(quest);
        }

        if (ImGui.MenuItem(Strings.FlagOnMap, enabled: links.CanFlagMap(quest)))
        {
            links.FlagMap(quest);
        }

        // Teleport, Walk and Go to giver; disabled, with the reason on hover (naming Lifestream or vnavmesh when missing).
        TravelControls.MenuItems(links, quest, Strings.TeleportToGiver);

        if (ImGui.MenuItem(Strings.LinkInChat))
        {
            links.PrintQuestLink(quest, Strings.StateName(row.State, quest));
        }
    }

    /// <summary>The "Clear my blues" plan (P3) the pinned-expansion section reads; null leaves the section out.</summary>
    public PlanSource? Plan { get; set; }

    /// <summary>The events ending soon (1.19.0, C10): their quests in the journal lead the seasonal section. Null keeps the events' order.</summary>
    public EventWarningSource? EventWarnings { get; set; }

    private readonly NewGamePlusText newGamePlusText = new();

    // The journal count line (1.19.0, C9), as of the session version and language it was built for.
    private (int Version, int Language) journalCountKey = (-1, -1);
    private string journalCountText = string.Empty;
    private bool journalCountFull;

    /// <summary>
    /// "Journal 25/30" (spec-1.19 C9, "The Todo overlay shows the same item from 25, if the player turns that on"): the
    /// status bar's words, the copper dot before them when the journal is full. Off by default
    /// (<see cref="Configuration.TodoShowJournalCount"/>).
    /// </summary>
    private void DrawJournalCount()
    {
        if (!settings.TodoShowJournalCount)
        {
            return;
        }

        if (journalCountKey != (session.Version, Localization.Loc.Version))
        {
            journalCountKey = (session.Version, Localization.Loc.Version);
            journalCountText = string.Empty;
            journalCountFull = false;
            if (session.ViewedSnapshot is { } snapshot && session.Bundle is { } bundle
                && Core.Journal.JournalSlots.Of(snapshot, bundle.Catalog) is { Bar: not Core.Journal.JournalBar.Hidden } slots)
            {
                journalCountFull = slots.Bar == Core.Journal.JournalBar.Full;
                journalCountText = string.Format(CultureInfo.CurrentCulture, journalCountFull ? Strings.JournalBarFullFormat : Strings.JournalBarFormat, slots.Used, slots.Cap);
            }
        }

        if (journalCountText.Length == 0)
        {
            return;
        }

        if (journalCountFull)
        {
            var line = ImGui.GetTextLineHeight();
            var pos = ImGui.GetCursorScreenPos();
            var radius = UiMetrics.Px(3f);
            ImGui.GetWindowDrawList().AddCircleFilled(new Vector2(pos.X + radius, pos.Y + (line * 0.5f)), radius, Theme.U32(Theme.Copper), 12);
            ImGui.SetCursorScreenPos(new Vector2(pos.X + UiMetrics.Px(12f), pos.Y));
        }

        Chrome.OutlinedText(journalCountText, Theme.Surface.Text);
    }

    /// <summary>Opens the main window on every pin (the Journal filtered to Pinned); the Pinned section's "+N more" line calls it.</summary>
    public Action? ShowPins { get; set; }

    /// <summary>The followed route (1.6.0); null leaves the route section out.</summary>
    public ActiveRouteService? ActiveRoutes { get; set; }

    /// <summary>Next stops (1.6.0); null leaves the section out.</summary>
    public NextStopsSource? NextStops { get; set; }

    /// <summary>Opens the route window on a target (the Pinned caption's "Route through my pins"); null hides that menu.</summary>
    public Action<Core.Route.RouteTarget>? OpenRoute { get; set; }

    /// <summary>Opens the route window on the followed route; the route section's "+N more" line calls it.</summary>
    public Action? ShowFollowedRoute { get; set; }

    /// <summary>
    /// The "Why it stopped" card (1.18, A2), shown in the title's line while it is up, and Stop all there while a
    /// hand-off other than Questionable runs. Set by the plugin.
    /// </summary>
    public Game.RunStops? RunStops { get; set; }

    private void OnSessionChanged() => dirty = true;

    private void OnTerritoryChanged(uint territory) => dirty = true;

    /// <summary>Section toggles as one integer, compared per frame so a change in the settings window rebuilds at once.</summary>
    private int SettingsSignature() =>
        (settings.TodoShowPins ? 1 : 0) | (settings.TodoShowNearbyFeature ? 2 : 0) | (settings.TodoShowMsq ? 4 : 0) | (settings.TodoShowJobQuests ? 8 : 0)
        | (settings.TodoShowSeasonal ? 16 : 0) | (settings.TodoShowPlan ? 32 : 0) | ((settings.TodoPlanExpansion + 1) << 6)
        | (settings.TodoShowRoute ? 1 << 20 : 0) | (settings.TodoShowNextStops ? 1 << 21 : 0);

    /// <summary>
    /// The followed route's, Next stops' and the unlock index's revisions (each read through its source, which rebuilds
    /// first when due), so a nearby unlock quest's "Opens …" hint follows the index once it is built.
    /// </summary>
    private (int Route, int Stops, int Unlocks) SourceRevisions()
    {
        var route = 0;
        if (settings.TodoShowRoute && ActiveRoutes is { } routes)
        {
            _ = routes.ViewedRoute;
            route = routes.Revision;
        }

        var stops = 0;
        if (settings.TodoShowNextStops && NextStops is { } source)
        {
            _ = source.Stops;
            stops = source.Revision;
        }

        var unlocks = settings.TodoShowNearbyFeature && pins.Unlocks is { } index ? index.Revision : 0;
        return (route, stops, unlocks);
    }

    private (int Route, int Stops, int Unlocks) builtSources = (-1, -1, -1);

    /// <summary>Once per frame: rebuilds when any input moved, the viewed character's pins included.</summary>
    private void Refresh()
    {
        // The runner follows the viewed character, its own pin toggles and other clients' saves (PinsVersion); no
        // file is read here.
        pins.SyncPins();
        var pinsVersion = pins.PinsVersion;
        var version = session.Version;
        var territory = clientState.TerritoryType;
        var signature = SettingsSignature();
        var sources = SourceRevisions();
        if (!dirty && version == builtVersion && pinsVersion == builtPins && territory == builtTerritory && signature == builtSettings && sources == builtSources)
        {
            return;
        }

        dirty = false;
        builtVersion = version;
        builtPins = pinsVersion;
        builtTerritory = territory;
        builtSettings = signature;
        builtSources = sources;
        Rebuild(territory);
    }

    private void Rebuild(uint territory)
    {
        var bundle = session.Bundle;
        catalogReady = bundle is not null;
        enabledSections = (settings.TodoShowPins ? 1 : 0) + (settings.TodoShowNearbyFeature ? 1 : 0) + (settings.TodoShowMsq ? 1 : 0) + (settings.TodoShowJobQuests ? 1 : 0)
                          + (settings.TodoShowSeasonal ? 1 : 0) + (settings.TodoShowPlan && settings.TodoPlanExpansion >= 0 && Plan is not null ? 1 : 0)
                          + (settings.TodoShowNextStops && NextStops is not null ? 1 : 0);

        if (bundle is null || session.ViewedSnapshot is not { } snapshot)
        {
            sections = [];
            builtCharacter = null;
            return;
        }

        CopyPins();
        if (!ReferenceEquals(ladderBundle, bundle))
        {
            ladderBundle = bundle;
            ladder = bundle.BuildJobLadder();
        }

        // Seasonal events running on the server (SessionState.ServerFestivals: the live flags, or the viewed snapshot's
        // less those a passed curated end shows to be stale, the set the states were resolved with); ends from curated data only.
        var now = DateTime.UtcNow;
        IReadOnlyList<RunningFestival> running = settings.TodoShowSeasonal
            ? SeasonalNow.Running(bundle.Catalog, session.ServerFestivals, session.States, session.Curated.Festivals, now, session.EnteredFestivalEnds)
            : [];
        var model = TodoList.Build(new TodoInputs(
            bundle.Catalog,
            session.States,
            pinned,
            session.FeatureQuestIds,
            territory,
            snapshot.CurrentJob,
            snapshot.JobLevels,
            ladder,
            bundle.Names.ClassJobAbbreviations,
            settings.TodoShowPins,
            settings.TodoShowNearbyFeature,
            settings.TodoShowMsq,
            settings.TodoShowJobQuests,
            // The session's names: blocker hints name quests, masked ones by their placeholder (T19).
            session.Names,
            running,
            settings.TodoShowSeasonal,
            now,
            // Built only when the Clear my blues block can show: the plan costs a sheet read and a pass over every
            // unlock quest, and nothing is pinned by default.
            settings.TodoShowPlan && settings.TodoPlanExpansion >= 0 ? Plan?.Plan : null,
            settings.TodoPlanExpansion,
            settings.TodoShowPlan,
            Route: settings.TodoShowRoute ? ActiveRoutes?.ViewedRoute : null,
            ShowRoute: settings.TodoShowRoute,
            Stops: settings.TodoShowNextStops ? NextStops?.Stops : null,
            ShowNextStops: settings.TodoShowNextStops,
            EndingSoon: settings.TodoShowSeasonal ? EventWarnings?.Current : null,
            // My blues (1.21.0, P4): the tier word after the level, and the quests set aside left out.
            TierOf: Plan is { } tiers ? tiers.TierOf : null,
            SetAside: session.ViewedSetAside));

        enabledSections = model.EnabledSections;
        if (model.Sections.Count == 0)
        {
            sections = [];
            builtCharacter = session.ViewedContentId;
            return;
        }

        var views = new SectionView[model.Sections.Count];
        for (var i = 0; i < views.Length; i++)
        {
            var section = model.Sections[i];
            var rows = new List<Row>(section.Rows.Count);
            foreach (var row in section.Rows)
            {
                if (bundle.Catalog.GetByRowId(row.RowId) is not { } quest)
                {
                    continue;
                }

                // An unlock quest startable here names what it opens after its hint (feature plan v6 K4): one quiet
                // line, never for a quest the shield masks.
                var hint = row.Hint;
                if (section.Section == TodoSection.NearbyFeature && pins.Unlocks is { } unlocks && !session.Spoilers.IsMasked(quest)
                    && unlocks.Current.Headline(quest.RowId) is { Group: <= Core.Unlocks.UnlockGroup.Feature } headline)
                {
                    var opens = Core.Unlocks.UnlockText.Opens(Core.Unlocks.UnlockView.ShieldedName(headline, session.Spoilers));
                    hint = hint.Length > 0 ? hint + Strings.StateReasonSeparator + opens : opens;
                }

                var tooltip = row.Kind == TodoRowKind.Stop
                    ? row.Name + Strings.StateReasonSeparator + row.Hint + "\n" + Strings.TodoStopClickHint
                    : hint.Length > 0
                        ? Strings.StateName(row.State, quest) + Strings.StateReasonSeparator + hint + "\n" + Strings.TodoRowClickHint
                        : Strings.StateName(row.State, quest) + "\n" + Strings.TodoRowClickHint;
                rows.Add(new Row(quest, row.Name, row.State, hint, tooltip, Avatar: row.Kind == TodoRowKind.Stop));
            }

            // A capped section counts every row it holds in its caption ("Pinned (60)") and names the rest on one line.
            var name = section.Title.Length > 0 ? section.Title : Strings.TodoSectionName(section.Section);
            var headerText = string.Format(CultureInfo.CurrentCulture, Strings.TodoSectionFormat, name, rows.Count + section.More);
            var toggle = string.Format(CultureInfo.CurrentCulture, Strings.TodoSectionToggleFormat, name);
            var more = section.More > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.TodoMoreFormat, section.More) : null;
            views[i] = new SectionView(section.Section, headerText, toggle, [.. section.Notes], rows.ToArray(), more);
        }

        // The completion beat and the new rows' fade-in (M3) play for the live character as it plays, never on a
        // character switch, under Reduce motion or in combat.
        var sameCharacter = builtCharacter == session.ViewedContentId;
        builtCharacter = session.ViewedContentId;
        if (sameCharacter && session.IsLive && Motion.WorldEnabled && !condition[ConditionFlag.InCombat])
        {
            Settle(views, ImGui.GetTime());
        }
        else
        {
            ghostSince.Clear();
            arrivedAt.Clear();
        }

        sections = views;
    }

    /// <summary>
    /// Compares the new rows with the ones on screen, section by section: a row that left because its quest is now
    /// completed comes back as a ghost where it stood until its beat is over (<see cref="TodoBeat.Ghosts"/>), and a row
    /// that was not there before starts its fade-in. Beats and fade-ins that are over are forgotten.
    /// </summary>
    private void Settle(SectionView[] views, double now)
    {
        Forget(ghostSince, now, MotionTokens.Beat);
        Forget(arrivedAt, now, MotionTokens.Reveal);
        var states = session.States;
        var completedRecently = Motion.CompletedRecently;
        for (var v = 0; v < views.Length; v++)
        {
            var view = views[v];
            if (Array.Find(sections, old => old.Section == view.Section) is not { } before)
            {
                continue;
            }

            // A ghost whose beat is over stands for no quest (0), so it is never brought back.
            var oldIds = new uint[before.Rows.Length];
            for (var i = 0; i < oldIds.Length; i++)
            {
                var old = before.Rows[i];
                oldIds[i] = old.Ghost && !ghostSince.ContainsKey(old.Quest.RowId) ? 0u : old.Quest.RowId;
            }

            var newIds = new uint[view.Rows.Length];
            for (var i = 0; i < newIds.Length; i++)
            {
                newIds[i] = view.Rows[i].Quest.RowId;
                if (Array.IndexOf(oldIds, newIds[i]) < 0)
                {
                    arrivedAt[newIds[i]] = now;
                }
            }

            ghostScratch.Clear();
            // Only a quest the live character just completed gets a beat (not one unpinned after it was done long ago).
            var found = TodoBeat.Ghosts(oldIds, newIds, id => id != 0 && (ghostSince.ContainsKey(id) || (completedRecently && states.GetValueOrDefault(id)?.State == QuestState.Completed)), ghostScratch);
            if (found == 0)
            {
                continue;
            }

            var rows = new List<Row>(view.Rows);
            foreach (var (index, rowId) in ghostScratch)
            {
                if (!ghostSince.ContainsKey(rowId))
                {
                    ghostSince[rowId] = now;
                }

                rows.Insert(Math.Min(index, rows.Count), before.Rows[index] with { Ghost = true });
            }

            views[v] = view with { Rows = rows.ToArray() };
        }
    }

    /// <summary>Drops the entries of <paramref name="started"/> whose one-shot of <paramref name="seconds"/> is over.</summary>
    private static void Forget(Dictionary<uint, double> started, double now, float seconds)
    {
        foreach (var (id, at) in started)
        {
            if (now - at >= seconds)
            {
                started.Remove(id);
            }
        }
    }

    /// <summary>
    /// The viewed character's pins in the order they were pinned, copied from the query runner when they changed: its
    /// list is its own and changes in place, while the rows built from this copy live until the next rebuild.
    /// </summary>
    private void CopyPins()
    {
        pins.SyncPins();
        if (pinsCopied == pins.PinsVersion)
        {
            return;
        }

        pinsCopied = pins.PinsVersion;
        pinned.Clear();
        pinned.AddRange(pins.PinnedInOrder);
    }

    private void Save() => settings.Save(pluginInterface);
}
