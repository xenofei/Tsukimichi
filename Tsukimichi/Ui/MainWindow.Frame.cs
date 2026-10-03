using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// What floats over the main window's body (feature plan v6 U2): the filter drawer over the Journal tree and the notice
/// dock, placed with the Undo toast by the one slot manager (<see cref="FloatingLayers"/>). Nothing here takes a line of
/// the layout, so a notice, a prompt or the open filters never move the panes under the player.
/// </summary>
public sealed partial class MainWindow
{
    /// <summary>The dock's width and its padding, logical; it never runs wider than the body less its margins.</summary>
    private const float DockWidthLogical = 360f;
    private const float DockPadLogical = 12f;

    /// <summary>A folded notice's chip padding, logical.</summary>
    private const float DockChipPadLogical = 4f;

    /// <summary>The drawer's least width, logical: it covers the tree, and more when the tree is narrower.</summary>
    private const float DrawerWidthLogical = 300f;

    /// <summary>How long a floating layer fades in (opacity only; at once under Reduce motion).</summary>
    private const float FadeSeconds = 0.16f;

    /// <summary>The pager's "1/3"; numbers only, so it is the same in every language.</summary>
    private const string DockPagerFormat = "{0}/{1}";

    private static readonly string PrevIcon = FontAwesomeIcon.AngleLeft.ToIconString();
    private static readonly string NextIcon = FontAwesomeIcon.AngleRight.ToIconString();
    private static readonly string PinIcon = FontAwesomeIcon.Thumbtack.ToIconString();
    private static readonly string FoldIcon = FontAwesomeIcon.AngleDown.ToIconString();
    private static readonly string UnfoldIcon = FontAwesomeIcon.AngleUp.ToIconString();

    private readonly NoticeQueue notices = new();

    // This frame's body (under the toolbar, down to the status bar) and the Journal tree's column, for the floating layers.
    private Vector2 bodyMin;
    private float bodyHeight;
    private Vector2 leftMin;
    private float leftWidth;
    private float statusTop;

    // The dock: the notice it last drew and when that one appeared (its fade), and the pager's text.
    private NoticeKind? dockShown;
    private double dockShownAt;
    private (int Position, int Count) pagerKey = (-1, -1);
    private string pagerText = string.Empty;

    // The card notices' line, rebuilt when the card or the language changes.
    private (NoticeKind Kind, string Title, int Language) cardNoticeKey = ((NoticeKind)(-1), string.Empty, -1);
    private string cardNotice = string.Empty;

    // When the drawer opened (its fade); negative while it is shut.
    private double drawerOpenedAt = -1.0;

    /// <summary>
    /// After the status bar: the drawer and the dock, with the Undo toast's place handed out beside the dock's. The
    /// selected Journal row is kept clear on the Journal tab.
    /// </summary>
    private void DrawFloating(SessionState session, CatalogBundle bundle)
    {
        var windowPos = ImGui.GetWindowPos();
        var right = windowPos.X + ImGui.GetWindowContentRegionMax().X;
        var body = new ScreenRect(bodyMin, new Vector2(right, MathF.Max(bodyMin.Y, statusTop)));

        UpdateNotices(session);
        var dockSize = notices.Current is { } kind ? MeasureDock(kind, body.Width) : Vector2.Zero;
        if (dockSize.X > 0f)
        {
            FloatingLayers.Want(FloatingLayer.Dock, dockSize);
        }

        UndoToast.WantSlot();
        // The detail pane's sticky action bar is kept clear too: the dock and the Undo toast sit above it.
        FloatingLayers.Frame(in body, ui.Tab == NavTab.Journal ? tablePane.SelectedRowRect : default, ui.SelectedRowId is not null ? detailPane.ActionBarRect : default);

        DrawDrawer(session, bundle, right);
        DrawDock(session);
    }

    // ------------------------------------------------------------------ notice dock

    /// <summary>Tells the queue which notices are due this frame; each source decides, as its banner did before 1.12.</summary>
    private void UpdateNotices(SessionState session)
    {
        notices.Set(NoticeKind.RebuildFailed, session.CatalogError is not null);
        notices.Set(NoticeKind.Freshness, freshness is not null && plugin.Settings is { } settings
            && Core.Diagnostics.DataFreshness.StripVisible(freshness.Current, settings.DataFreshnessDismissedFor));

        // The context line (1.7.0): decided on the first draw with a catalog, gone once neither a stored character nor
        // a filter holds.
        var filters = FilterBadge.Count(ui.Filters);
        var stored = ViewingStored(session);
        if (!contextChecked)
        {
            contextChecked = true;
            contextVisible = stored || filters >= 2;
        }

        if (contextVisible && !stored && filters == 0)
        {
            contextVisible = false;
        }

        notices.Set(NoticeKind.Context, contextVisible);

        if (pinPromptVisible && overlayOn?.Invoke() == true)
        {
            pinPromptVisible = false;
        }

        notices.Set(NoticeKind.PinPrompt, pinPromptVisible && toggleOverlay is not null);

        // A card that waits in the no-selection slot while a quest is selected says so here.
        var card = ui.SelectedRowId is null ? null : DueCard();
        notices.Set(NoticeKind.Setup, card == NoticeKind.Setup);
        notices.Set(NoticeKind.WhatsNew, card == NoticeKind.WhatsNew);
        notices.Set(NoticeKind.WelcomeBack, card == NoticeKind.WelcomeBack);
    }

    /// <summary>
    /// The dock's size for <paramref name="kind"/>: its text wrapped at a fixed width, then one row of buttons; a folded
    /// notice (<see cref="NoticeQueue.Collapsed"/>) is one short row, its chip.
    /// </summary>
    private Vector2 MeasureDock(NoticeKind kind, float bodyWidth)
    {
        var margin = UiMetrics.Px(10f);
        var width = MathF.Min(UiMetrics.Px(DockWidthLogical), bodyWidth - (2f * margin));
        if (width < UiMetrics.Px(120f))
        {
            return Vector2.Zero;
        }

        if (notices.Collapsed(kind))
        {
            var chipPad = UiMetrics.Px(DockChipPadLogical);
            var chipWidth = (2f * chipPad) + ChipLabelWidth(kind) + ImGui.GetStyle().ItemSpacing.X + DockEndWidth(kind);
            return new Vector2(MathF.Ceiling(MathF.Min(width, chipWidth)), MathF.Ceiling((2f * chipPad) + ImGui.GetFrameHeight()));
        }

        var pad = UiMetrics.Px(DockPadLogical);
        var text = NoticeText(kind);
        var textHeight = ImGui.CalcTextSize(text, false, width - (2f * pad)).Y;
        var height = (2f * pad) + textHeight + ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeight();
        return new Vector2(width, MathF.Ceiling(height));
    }

    /// <summary>The line a notice says, built only when its inputs change.</summary>
    private string NoticeText(NoticeKind kind)
    {
        switch (kind)
        {
            case NoticeKind.RebuildFailed:
                if (plugin.Session?.CatalogError is { } error && (rebuildFailure is null || error != rebuildFailureError || rebuildFailureLanguage != Loc.Version))
                {
                    rebuildFailureError = error;
                    rebuildFailureLanguage = Loc.Version;
                    rebuildFailure = string.Format(CultureInfo.CurrentCulture, Strings.CatalogRebuildFailedFormat, error);
                }

                return rebuildFailure ?? string.Empty;

            case NoticeKind.Freshness:
                if (freshness is { } source && (freshnessLineCount != source.Current.NewQuests || freshnessLineLanguage != Loc.Version))
                {
                    freshnessLineCount = source.Current.NewQuests;
                    freshnessLineLanguage = Loc.Version;
                    freshnessLine = Strings.FreshnessStrip(source.Current.NewQuests);
                }

                return freshnessLine;

            case NoticeKind.Context:
                return ContextLine();

            case NoticeKind.PinPrompt:
                return Strings.PinOverlayPrompt;

            default:
                var title = kind switch
                {
                    NoticeKind.Setup => Strings.Setup.Title,
                    NoticeKind.WhatsNew => whatsNew?.Title ?? string.Empty,
                    _ => Strings.WelcomeBackTitle,
                };
                if (cardNoticeKey != (kind, title, Loc.Version))
                {
                    cardNoticeKey = (kind, title, Loc.Version);
                    cardNotice = string.Format(CultureInfo.CurrentCulture, Strings.DockCardWaitingFormat, title);
                }

                return cardNotice;
        }
    }

    /// <summary>"Viewing Alt · 3 filters on", rebuilt when the name, the count or the language changes.</summary>
    private string ContextLine()
    {
        var session = plugin.Session;
        var stored = session is not null && ViewingStored(session);
        var filters = FilterBadge.Count(ui.Filters);
        var name = stored ? session!.ViewedSnapshot!.Name : string.Empty;
        if (contextKey != (name, filters, Loc.Version))
        {
            contextKey = (name, filters, Loc.Version);
            var viewing = stored ? string.Format(CultureInfo.CurrentCulture, Strings.ContextViewingFormat, name) : string.Empty;
            var filtersOn = filters > 0 ? Strings.ContextFilters(filters) : string.Empty;
            contextLine = viewing.Length > 0 && filtersOn.Length > 0 ? viewing + Strings.ContextSeparator + filtersOn : viewing + filtersOn;
        }

        return contextLine;
    }

    /// <summary>Whether the window shows a stored character the player chose, while another may be the one logged in.</summary>
    private static bool ViewingStored(SessionState session) =>
        !session.IsFollowingLive && session.ViewedSnapshot is not null && !session.IsLive;

    /// <summary>
    /// The notice dock (feature plan v6 U2, decision 6): one notice at a time in a card floating at its slot (bottom
    /// right of the body), with a "1/3" pager when more wait. It fades in (opacity only, at once under Reduce motion). A
    /// one-time prompt closes by itself after about 15 seconds on screen and the pointer resting on the dock stops that
    /// clock; a notice that needs the player (a failed rebuild, the game-update line) stays until its cause goes.
    /// </summary>
    private void DrawDock(SessionState session)
    {
        var now = ImGui.GetTime();
        if (notices.Current is not { } kind)
        {
            dockShown = null;
            notices.Suspend();
            return;
        }

        if (!FloatingLayers.TryGet(FloatingLayer.Dock, FloatingLayers.OwnerWindowId(), out var place))
        {
            // No room for the dock this frame: the prompt is not on screen, so its clock stands still.
            dockShown = null;
            notices.Tick(now, paused: true);
            return;
        }

        if (dockShown != kind)
        {
            dockShown = kind;
            dockShownAt = now;
        }

        var fade = UiMetrics.ReduceMotion ? 1f : Math.Clamp((float)((now - dockShownAt) / FadeSeconds), 0f, 1f);
        var folded = notices.Collapsed(kind);
        var pad = UiMetrics.Px(folded ? DockChipPadLogical : DockPadLogical);
        var s = Theme.Surface;
        var hovered = false;
        ImGui.SetCursorScreenPos(place.Min);
        using (ImRaii.PushColor(ImGuiCol.ChildBg, s.Raised with { W = 1f })
                   .Push(ImGuiCol.Border, Theme.MoonRoadArt ? s.Ornament with { W = Theme.OrnamentAlpha(0.55f) } : s.Line))
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(pad))
                   .Push(ImGuiStyleVar.ChildRounding, UiMetrics.Px(6f))
                   .Push(ImGuiStyleVar.ChildBorderSize, 1f)
                   .Push(ImGuiStyleVar.Alpha, fade))
        using (var dock = ImRaii.Child("##noticeDock", place.Size, true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoSavedSettings))
        {
            if (dock)
            {
                hovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
                if (folded)
                {
                    DrawNoticeChip(kind);
                }
                else
                {
                    DrawNotice(session, kind);
                }
            }
        }

        notices.Tick(now, hovered);
    }

    private static Vector4 NoticeTone(NoticeKind kind) => kind switch
    {
        NoticeKind.RebuildFailed => Theme.Eclipse,
        NoticeKind.Freshness => Theme.Gilt,
        _ => Theme.Surface.Text,
    };

    /// <summary>A folded notice's short name ("Rebuild failed", "Game updated").</summary>
    private static string ChipLabel(NoticeKind kind) => kind == NoticeKind.RebuildFailed ? Strings.DockChipRebuildFailed : Strings.DockChipFreshness;

    /// <summary>The chip's own width: the unfold arrow, a gap and the short name.</summary>
    private static float ChipLabelWidth(NoticeKind kind)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var icon = ImGui.CalcTextSize(UnfoldIcon).X;
        ImGui.PopFont();
        var style = ImGui.GetStyle();
        return (2f * style.FramePadding.X) + icon + style.ItemInnerSpacing.X + ImGui.CalcTextSize(ChipLabel(kind)).X;
    }

    /// <summary>
    /// A folded notice that stays: one small chip in its tone, a click opens it again; the pager still reaches the
    /// others.
    /// </summary>
    private void DrawNoticeChip(NoticeKind kind)
    {
        var height = ImGui.GetFrameHeight();
        var width = ChipLabelWidth(kind);
        var clicked = ImGui.InvisibleButton("##dockUnfold", new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        var min = ImGui.GetItemRectMin();
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        if (hovered)
        {
            dl.AddRectFilled(min, min + new Vector2(width, height), Theme.U32(s.Hover), height * 0.5f);
        }

        var style = ImGui.GetStyle();
        var color = Theme.U32(NoticeTone(kind));
        ImGui.PushFont(UiBuilder.IconFont);
        var iconSize = ImGui.CalcTextSize(UnfoldIcon);
        dl.AddText(min + new Vector2(style.FramePadding.X, (height - iconSize.Y) * 0.5f), color, UnfoldIcon);
        ImGui.PopFont();
        var label = ChipLabel(kind);
        var textY = (height - ImGui.GetTextLineHeight()) * 0.5f;
        dl.AddText(min + new Vector2(style.FramePadding.X + iconSize.X + style.ItemInnerSpacing.X, textY), color, label);
        Chrome.FocusRing(height * 0.5f);
        if (hovered)
        {
            UiMetrics.Tooltip(NoticeText(kind) + "\n\n" + Strings.DockUnfoldTooltip);
        }

        if (clicked)
        {
            notices.SetCollapsed(kind, false);
        }

        DrawDockEnd(kind);
    }

    /// <summary>One notice: its line, wrapped, then its buttons, and the pager and × at the right end.</summary>
    private void DrawNotice(SessionState session, NoticeKind kind)
    {
        var tone = NoticeTone(kind);
        using (Theme.PushText(tone))
        {
            ImGui.TextWrapped(NoticeText(kind));
        }

        switch (kind)
        {
            case NoticeKind.RebuildFailed:
                if (ImGui.SmallButton(Strings.Retry + "##dockRetry"))
                {
                    retryTask = retryCatalog();
                }

                break;

            case NoticeKind.Freshness:
                DrawFreshnessActions();
                break;

            case NoticeKind.Context:
                DrawContextActions(session);
                break;

            case NoticeKind.PinPrompt:
                if (ImGui.SmallButton(Strings.PinOverlayTurnOn + "##dockPinOn"))
                {
                    toggleOverlay?.Invoke();
                    pinPromptVisible = false;
                }

                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.PinOverlayTurnOnTooltip);
                }

                ImGui.SameLine();
                if (ImGui.SmallButton(Strings.PinOverlayNotNow + "##dockPinNo"))
                {
                    pinPromptVisible = false;
                }

                break;

            default:
                if (ImGui.SmallButton(Strings.DockCardShow + "##dockCardShow"))
                {
                    // The card lives in the no-selection slot.
                    ui.SelectedRowId = null;
                }

                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.DockCardShowTooltip);
                }

                break;
        }

        DrawDockEnd(kind);
    }

    /// <summary>"Show them" sets the Added in filter to New since data; Dismiss hides the line until the next game update.</summary>
    private void DrawFreshnessActions()
    {
        if (ImGui.SmallButton(Strings.FreshnessShowNew + "##freshnessShow"))
        {
            ui.Scope = QuestScope.None;
            ui.Filters.AddedIn = FilterSet.NewSinceData;
            OnFiltersChanged();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.FreshnessShowNewTooltip);
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.FreshnessDismiss + "##freshnessDismiss") && freshness is { } source && plugin.Settings is { } settings)
        {
            settings.DataFreshnessDismissedFor = Core.Diagnostics.DataFreshness.DismissKey(source.Current);
            try
            {
                settings.Save(pluginInterface);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Settings could not be saved");
            }
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.FreshnessDismissTooltip);
        }
    }

    /// <summary>Follow me (while a character is logged in) and Clear (while a filter narrows the table).</summary>
    private void DrawContextActions(SessionState session)
    {
        var any = false;
        if (ViewingStored(session) && session.LiveContentId is not null)
        {
            if (ImGui.SmallButton(Strings.ContextFollowMe + "##contextFollow"))
            {
                session.ViewedContentId = null;
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.ContextFollowMeTooltip);
            }

            any = true;
        }

        if (FilterBadge.Count(ui.Filters) > 0)
        {
            if (any)
            {
                ImGui.SameLine();
            }

            if (ImGui.SmallButton(Strings.ContextClear + "##contextClear"))
            {
                filterPanel.ResetAll();
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.ContextClearTooltip);
            }

            any = true;
        }

        if (!any)
        {
            // Keeps the button row's height when neither applies, so the dock does not change shape.
            ImGui.Dummy(new Vector2(1f, ImGui.GetFrameHeight()));
        }
    }

    private static bool Closable(NoticeKind kind) => kind is NoticeKind.Context or NoticeKind.Setup or NoticeKind.WhatsNew or NoticeKind.WelcomeBack;

    /// <summary>The pager's "1/3", rebuilt when the place or the count changes.</summary>
    private string PagerText()
    {
        if (pagerKey != (notices.Position, notices.Count))
        {
            pagerKey = (notices.Position, notices.Count);
            pagerText = string.Format(CultureInfo.InvariantCulture, DockPagerFormat, notices.Position, notices.Count);
        }

        return pagerText;
    }

    /// <summary>The width <see cref="DrawDockEnd"/> takes for <paramref name="kind"/>; 0 when it draws nothing.</summary>
    private float DockEndWidth(NoticeKind kind)
    {
        var count = notices.Count;
        // The one square button at the end: × on a prompt, the fold arrow on an open notice that stays.
        var square = Closable(kind) || (NoticeQueue.Stays(kind) && !notices.Collapsed(kind));
        var side = ImGui.GetFrameHeight();
        var gap = ImGui.GetStyle().ItemSpacing.X;
        return (square ? side : 0f) + (count > 1 ? (2f * side) + ImGui.CalcTextSize(PagerText()).X + (2f * gap) + (square ? gap : 0f) : 0f);
    }

    /// <summary>
    /// The right end of the button row: the pager when more than one notice waits, the × on a notice the player may
    /// close, and on a notice that stays (it closes through its own buttons) the arrow that folds it to its chip.
    /// </summary>
    private void DrawDockEnd(NoticeKind kind)
    {
        var count = notices.Count;
        var closable = Closable(kind);
        var foldable = NoticeQueue.Stays(kind) && !notices.Collapsed(kind);
        if (count < 2 && !closable && !foldable)
        {
            return;
        }

        var side = ImGui.GetFrameHeight();
        var width = DockEndWidth(kind);
        ImGui.SameLine(MathF.Max(0f, ImGui.GetWindowContentRegionMax().X - width));
        if (count > 1)
        {
            if (IconButton(PrevIcon, "##dockPrev", side, Strings.DockPreviousTooltip))
            {
                notices.Step(-1);
            }

            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled(PagerText());
            ImGui.SameLine();
            if (IconButton(NextIcon, "##dockNext", side, Strings.DockNextTooltip))
            {
                notices.Step(1);
            }

            if (closable || foldable)
            {
                ImGui.SameLine();
            }
        }

        if (foldable && IconButton(FoldIcon, "##dockFold", side, Strings.DockFoldTooltip))
        {
            notices.SetCollapsed(kind, true);
        }

        if (closable && IconButton(DismissIcon, "##dockClose", side, Strings.DockCloseTooltip))
        {
            if (kind == NoticeKind.Context)
            {
                contextVisible = false;
            }

            notices.Close(kind);
        }
    }

    /// <summary>A square icon button of <paramref name="side"/> with a tooltip; true on click.</summary>
    private static bool IconButton(string icon, string id, float side, string tooltip)
    {
        var clicked = ImGui.InvisibleButton(id, new Vector2(side));
        var hovered = ImGui.IsItemHovered();
        var min = ImGui.GetItemRectMin();
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        if (hovered)
        {
            dl.AddRectFilled(min, min + new Vector2(side), Theme.U32(s.Hover), side * 0.5f);
        }

        ImGui.PushFont(UiBuilder.IconFont);
        var size = ImGui.CalcTextSize(icon);
        dl.AddText(min + ((new Vector2(side) - size) * 0.5f), Theme.U32(hovered ? s.Text : s.TextSecondary), icon);
        ImGui.PopFont();
        Chrome.FocusRing(side * 0.5f);
        if (hovered)
        {
            UiMetrics.Tooltip(tooltip);
        }

        return clicked;
    }

    private static readonly string DismissIcon = FontAwesomeIcon.Times.ToIconString();

    // ------------------------------------------------------------------ filter drawer

    /// <summary>
    /// The filter panel as a drawer over the Journal tree (feature plan v6 U2): it opens over the tree's column (at least
    /// <see cref="DrawerWidthLogical"/> wide) instead of pushing the tree down, fades in, scrolls on its own, and closes
    /// on Esc, on its ×, on the Filters button, or on a click elsewhere in the window unless it is pinned open.
    /// </summary>
    private void DrawDrawer(SessionState session, CatalogBundle bundle, float right)
    {
        if (!ui.FilterPanelOpen || ui.Tab != NavTab.Journal || bodyHeight <= 0f)
        {
            drawerOpenedAt = -1.0;
            ui.Rects.Remove(UiRects.FilterPanel);
            return;
        }

        var now = ImGui.GetTime();
        if (drawerOpenedAt < 0.0)
        {
            drawerOpenedAt = now;
        }

        var size = new Vector2(MathF.Max(1f, MathF.Min(MathF.Max(leftWidth, UiMetrics.Px(DrawerWidthLogical)), right - leftMin.X)), bodyHeight);
        var rect = ScreenRect.FromSize(leftMin, size);
        if (ClickedOutsideDrawer(in rect))
        {
            ui.FilterPanelOpen = false;
            drawerOpenedAt = -1.0;
            ui.Rects.Remove(UiRects.FilterPanel);
            return;
        }

        var fade = UiMetrics.ReduceMotion ? 1f : Math.Clamp((float)((now - drawerOpenedAt) / FadeSeconds), 0f, 1f);
        var s = Theme.Surface;
        ImGui.SetCursorScreenPos(leftMin);
        using (ImRaii.PushColor(ImGuiCol.ChildBg, s.Raised with { W = 1f }).Push(ImGuiCol.Border, s.Line))
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(UiMetrics.Px(10f), UiMetrics.Px(8f)))
                   .Push(ImGuiStyleVar.ChildBorderSize, 1f)
                   .Push(ImGuiStyleVar.Alpha, fade))
        using (var drawer = ImRaii.Child("##filterDrawer", size, true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoSavedSettings))
        {
            if (!drawer)
            {
                return;
            }

            DrawDrawerEdge(rect, fade);
            DrawDrawerHeader();
            using var panel = ImRaii.Child("##filterDrawerBody", Vector2.Zero, false, ImGuiWindowFlags.NoSavedSettings);
            if (panel)
            {
                filterPanel.Draw(bundle, session.ViewedSnapshot, plugin.Settings);
            }
        }
    }

    /// <summary>
    /// A left click in this window outside the drawer that should close it: not while it is pinned, not on the Filters
    /// button (which toggles it itself), and not the click that closed a popup.
    /// </summary>
    private bool ClickedOutsideDrawer(in ScreenRect drawer)
    {
        if (plugin.Settings.FilterDrawerPinned || popupDepthAtEnd > 0 || !ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            || !ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
        {
            return false;
        }

        var mouse = ImGui.GetMousePos();
        if (drawer.Contains(mouse))
        {
            return false;
        }

        return !(ui.Rects.TryGetValue(UiRects.FiltersButton, out var button) && new ScreenRect(button.Min, button.Max).Contains(mouse));
    }

    /// <summary>The drawer's title, its pin and its ×, on one line.</summary>
    private void DrawDrawerHeader()
    {
        ImGui.AlignTextToFramePadding();
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextUnformatted(Strings.Filters);
        }

        var side = ImGui.GetFrameHeight();
        var pinned = plugin.Settings.FilterDrawerPinned;
        ImGui.SameLine(MathF.Max(0f, ImGui.GetWindowContentRegionMax().X - (2f * side) - ImGui.GetStyle().ItemSpacing.X));
        var pinMin = ImGui.GetCursorScreenPos();
        if (pinned)
        {
            ImGui.GetWindowDrawList().AddRectFilled(pinMin, pinMin + new Vector2(side), Theme.WithAlpha(Theme.Surface.Text, 0.12f), side * 0.5f);
        }

        if (IconButton(PinIcon, "##drawerPin", side, pinned ? Strings.FilterDrawerUnpinTooltip : Strings.FilterDrawerPinTooltip))
        {
            plugin.Settings.FilterDrawerPinned = !pinned;
            OnDisplayChanged();
        }

        ImGui.SameLine();
        if (IconButton(DismissIcon, "##drawerClose", side, Strings.FilterDrawerCloseTooltip))
        {
            ui.FilterPanelOpen = false;
        }

        ImGui.Spacing();
    }

    /// <summary>The drawer's right edge: a brass rule where the flair draws rules, and a soft shadow over the list beside it.</summary>
    private static void DrawDrawerEdge(in ScreenRect rect, float fade)
    {
        var dl = ImGui.GetWindowDrawList();
        var shadow = UiMetrics.Px(10f);
        dl.PushClipRect(rect.Min, rect.Max + new Vector2(shadow, 0f), false);
        var dark = Theme.WithAlpha(System.Numerics.Vector4.UnitW, 0.22f * fade);
        var clear = Theme.WithAlpha(System.Numerics.Vector4.UnitW, 0f);
        dl.AddRectFilledMultiColor(new Vector2(rect.Max.X, rect.Min.Y), new Vector2(rect.Max.X + shadow, rect.Max.Y), dark, clear, clear, dark);
        if (Theme.MoonRoadArt)
        {
            var line = UiMetrics.Hairline;
            dl.AddLine(new Vector2(rect.Max.X - (line * 0.5f), rect.Min.Y), new Vector2(rect.Max.X - (line * 0.5f), rect.Max.Y), Theme.WithAlpha(Theme.Surface.Ornament, Theme.OrnamentAlpha(0.6f) * fade), line);
        }

        dl.PopClipRect();
    }
}
