using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
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
/// behind gets every click; the lock is set from the title's menu (or its "…") and cleared from Settings. The window is
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
/// </summary>
public sealed class TodoOverlay : Window, IDisposable
{
    public const float MinOpacity = 0.6f;
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
    private readonly record struct Row(QuestRecord Quest, string Name, QuestState State, string Hint, string Tooltip);

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

    /// <summary>Not drawn while logged out, bound by a duty or watching a cutscene.</summary>
    public override bool DrawConditions() =>
        clientState.IsLoggedIn
        && !condition[ConditionFlag.BoundByDuty]
        && !condition[ConditionFlag.WatchingCutscene]
        && !condition[ConditionFlag.OccupiedInCutSceneEvent];

    public override void PreDraw()
    {
        Flags = settings.TodoOverlayLocked ? LockedFlags : BaseFlags;
        // The overlay's own opacity setting; the Night chrome keeps this alpha (it never touches BgAlpha).
        BgAlpha = ClampOpacity(settings.TodoOverlayOpacity);
        var compactWidth = CompactWidthLogical * UiMetrics.FontScale;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(settings.TodoOverlayCompact ? compactWidth : MinWidthLogical * UiMetrics.FontScale, 0f),
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
        }
    }

    private void DrawContent()
    {
        FireDueReveal();
        var compact = settings.TodoOverlayCompact;
        var layout = Measure(compact);
        DrawHeader(layout);
        if (!catalogReady)
        {
            Chrome.OutlinedText(session.CatalogLoading ? Strings.CatalogNotReady : Strings.CatalogUnavailable, Theme.Surface.TextSecondary);
            return;
        }

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
                DrawRow(section.Rows[i], i, layout, compact);
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
        using (ImRaii.Disabled(!canFlag))
        {
            if (ImGui.SmallButton(Strings.RouteFlagNextStop))
            {
                routes.FlagNextStop(route);
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(canFlag ? Strings.RouteFlagNextStopTooltip : Strings.RouteFlagNextStopUnavailable);
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.TodoRouteStop))
        {
            routes.Stop();
            dirty = true;
        }

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
            var caption = ImGui.GetFontSize() + (Theme.ShowRules ? UiMetrics.Px(HeadingLayout.SigilLogical + HeadingLayout.SigilGapLogical) : 0f);
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
                    var width = ImGui.CalcTextSize(row.Name).X;
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
        DrawQuestionableStatus(start, titleEnd, textY, layout);
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

        if (Questionable is { } questionable)
        {
            // The viewed character's pins, in the order they were pinned.
            ImGui.Separator();
            questionable.DrawSubmenu(QuestionableHost, Strings.QuestionableSendPins, pinned, static pins => pins);
        }
    }

    /// <summary>
    /// Questionable's live status in the title's line while it runs (1.12.0, U4: never a line of its own), outlined in
    /// gold and cut to the room between the title and the button column, the whole text on hover, and a small Stop
    /// left of the "…" (not while locked: the panel takes no clicks then). The panel is not widened for it.
    /// </summary>
    private void DrawQuestionableStatus(Vector2 start, float titleEnd, float textY, in RowLayout layout)
    {
        if (Questionable?.PollStatusText() is not { } text)
        {
            return;
        }

        var style = ImGui.GetStyle();
        var locked = settings.TodoOverlayLocked;
        var right = start.X + layout.RowWidth - (locked ? 0f : UiMetrics.MinTarget + style.ItemSpacing.X);
        var stopWidth = locked ? 0f : ImGui.CalcTextSize(Strings.QuestionableStopShort).X + (style.FramePadding.X * 2f);
        var textX = titleEnd + style.ItemSpacing.X;
        var room = right - textX - (locked ? 0f : stopWidth + style.ItemSpacing.X);
        if (room < UiMetrics.Px(32f))
        {
            // No room beside the title: the Stop alone still shows Questionable is running.
            room = 0f;
        }

        var line = ImGui.GetTextLineHeight();
        if (room > 0f)
        {
            var dl = ImGui.GetWindowDrawList();
            var textMin = new Vector2(textX, textY);
            Chrome.OutlinedEllipsisAt(dl, textMin, room, text, Theme.AccentU32);
            if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(textMin, textMin + new Vector2(room, line)))
            {
                UiMetrics.Tooltip(text, Strings.QuestionableStatusTooltip);
            }
        }

        if (locked || right - stopWidth < titleEnd)
        {
            return;
        }

        ImGui.SetCursorScreenPos(new Vector2(right - stopWidth, textY));
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(style.FramePadding.X, 0f)))
        {
            Questionable.DrawStopSmallButton(QuestionableHost, "##questionableStop");
        }
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

        var hovered = ImGui.IsItemHovered();
        // The pins' caption has one menu (1.6.0): a route through every pin, and Send pins to Questionable.
        var pinsMenu = section.Section == TodoSection.Pinned && (OpenRoute is not null || Questionable is not null);
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
        Questionable?.DrawSubmenu(QuestionableHost, Strings.QuestionableSendPins, pinned, static pins => pins);
    }

    private void DrawRow(Row row, int index, in RowLayout layout, bool compact)
    {
        using var id = ImRaii.PushId(index);
        var style = ImGui.GetStyle();
        var start = ImGui.GetCursorScreenPos();
        var line = ImGui.GetTextLineHeight();
        var textY = start.Y + (layout.RowHeight - line) * 0.5f;

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

        // The name ends in an ellipsis in the text column (Compact's fixed width, a panel narrowed by hand) rather than
        // being cut mid-letter, and the hint takes what the name leaves (R3 #5); a cut name heads the tooltip.
        var nameWidth = ImGui.CalcTextSize(row.Name).X;
        var fit = LineFit.Fit(layout.TextWidth, nameWidth, UiMetrics.Px(LayoutBudgets.RowNameMinLogical), [], [], style.ItemSpacing.X, UiMetrics.Px(24f));
        // Gold while Questionable works on this quest (1.6.0).
        var nameInk = Questionable?.RunningRowId == row.Quest.RowId ? Theme.AccentU32 : Theme.U32(Theme.Surface.Text);
        Chrome.OutlinedEllipsisAt(dl, new Vector2(textX, textY), fit.NameRoom, row.Name, nameInk, nameWidth);
        if (!compact && row.Hint.Length > 0 && fit.TailRoom > 0f)
        {
            var hintX = textX + fit.NameDrawn + style.ItemSpacing.X;
            Chrome.OutlinedEllipsisAt(dl, new Vector2(hintX, textY), fit.TailRoom, row.Hint, Theme.U32(Theme.Surface.TextSecondary));
        }

        dl.PopClipRect();
        if (hovered)
        {
            UiMetrics.Tooltip(fit.NameCut ? row.Name : row.Tooltip, fit.NameCut ? row.Tooltip : null);
        }

        // The "…" opens the same menu as the right-click, for keyboard, controller and one-handed players (A6). While
        // locked the panel is click-through, so the buttons would only be clutter.
        if (!settings.TodoOverlayLocked)
        {
            ImGui.SetCursorScreenPos(new Vector2(textX + layout.TextWidth + style.ItemSpacing.X, start.Y + (layout.RowHeight - UiMetrics.MinTarget) * 0.5f));
            openMenu |= Chrome.IconButtonRound("##more", MoreGlyph, Strings.TodoRowMoreTooltip);
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

    private void OnSessionChanged() => dirty = true;

    private void OnTerritoryChanged(uint territory) => dirty = true;

    /// <summary>Section toggles as one integer, compared per frame so a change in the settings window rebuilds at once.</summary>
    private int SettingsSignature() =>
        (settings.TodoShowPins ? 1 : 0) | (settings.TodoShowNearbyFeature ? 2 : 0) | (settings.TodoShowMsq ? 4 : 0) | (settings.TodoShowJobQuests ? 8 : 0)
        | (settings.TodoShowSeasonal ? 16 : 0) | (settings.TodoShowPlan ? 32 : 0) | ((settings.TodoPlanExpansion + 1) << 6)
        | (settings.TodoShowRoute ? 1 << 20 : 0) | (settings.TodoShowNextStops ? 1 << 21 : 0);

    /// <summary>The followed route's and Next stops' revisions (each read through its source, which rebuilds first when due).</summary>
    private (int Route, int Stops) SourceRevisions()
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

        return (route, stops);
    }

    private (int Route, int Stops) builtSources = (-1, -1);

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
            ? SeasonalNow.Running(bundle.Catalog, session.ServerFestivals, session.States, session.Curated.Festivals, now)
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
            ShowNextStops: settings.TodoShowNextStops));

        enabledSections = model.EnabledSections;
        if (model.Sections.Count == 0)
        {
            sections = [];
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

                var tooltip = row.Kind == TodoRowKind.Stop
                    ? row.Name + Strings.StateReasonSeparator + row.Hint + "\n" + Strings.TodoStopClickHint
                    : row.Hint.Length > 0
                        ? Strings.StateName(row.State, quest) + Strings.StateReasonSeparator + row.Hint + "\n" + Strings.TodoRowClickHint
                        : Strings.StateName(row.State, quest) + "\n" + Strings.TodoRowClickHint;
                rows.Add(new Row(quest, row.Name, row.State, row.Hint, tooltip));
            }

            // A capped section counts every row it holds in its caption ("Pinned (60)") and names the rest on one line.
            var name = section.Title.Length > 0 ? section.Title : Strings.TodoSectionName(section.Section);
            var headerText = string.Format(CultureInfo.CurrentCulture, Strings.TodoSectionFormat, name, rows.Count + section.More);
            var toggle = string.Format(CultureInfo.CurrentCulture, Strings.TodoSectionToggleFormat, name);
            var more = section.More > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.TodoMoreFormat, section.More) : null;
            views[i] = new SectionView(section.Section, headerText, toggle, [.. section.Notes], rows.ToArray(), more);
        }

        sections = views;
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
