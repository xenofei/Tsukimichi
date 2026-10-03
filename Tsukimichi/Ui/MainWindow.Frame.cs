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
    private bool leftStrip;
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

    // The drawer's body content as last measured (px; 0 until it first draws), and its header's and footer's labels.
    private float drawerContent;
    private readonly CountText drawerOnText = new(static () => Strings.FilterDrawerOnFormat);
    private (int Shown, int Total, int Language) showingKey = (-1, -1, -1);
    private (string Before, string Number, string After) showing = (string.Empty, string.Empty, string.Empty);
    private string showingWhole = string.Empty;

    // Whether the navigation column skipped the tree under the opaque drawer this frame (its sky is then the drawer's to draw).
    private bool treeHiddenThisFrame;

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
        NoticeKind.RebuildFailed => Theme.Danger,
        NoticeKind.Freshness => Theme.Brass.Body,
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

    /// <summary>Plain's "·" before the drawer header's count: the Undo toast's separator without its trailing space, made once.</summary>
    private static readonly string DrawerCountDot = Strings.UndoToastSeparator.TrimEnd();

    // ------------------------------------------------------------------ filter drawer

    /// <summary>
    /// The filter drawer (feature plan v6 U2, plan v7 UI-2): one opaque sheet exactly over the Journal tree's column
    /// (<see cref="DrawerLayout.Width"/>, never over the list), as tall as its content (measured on the last frame and
    /// capped at the body: past that its body scrolls and its header and footer stay). Header: the filter glyph,
    /// Filters in the Title role, a neutral "3 on" pill, pin and ×. Footer: "Showing N of M" and a quiet Reset with the
    /// floating Undo. It fades in, and closes on Esc, on its ×, on the Filters button, or on a click elsewhere in the
    /// window unless it is pinned. The tree under it fades out with it and is not drawn once it is opaque
    /// (<see cref="DrawNavigation"/>), so nothing of the tree shows at its sides or over its title. Each Decoration level
    /// has its own sheet (<see cref="DrawDrawerSheet"/>).
    /// </summary>
    private void DrawDrawer(SessionState session, CatalogBundle bundle, float right)
    {
        var treeHidden = treeHiddenThisFrame;
        treeHiddenThisFrame = false;
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

        var flair = Theme.Flair;
        var metrics = DrawerLayout.MetricsFor(flair);
        var tones = DrawerTones.For(flair, Theme.Surface);
        var width = DrawerLayout.Width(leftWidth, leftStrip, UiMetrics.Px(DrawerLayout.StripFloorLogical), right - leftMin.X);
        var (header, row) = DrawerHeaderHeight(flair, metrics);
        var footer = MathF.Max(UiMetrics.Px(metrics.Footer), ImGui.GetTextLineHeight() + UiMetrics.Px(8f));
        var heights = DrawerLayout.Heights(header, drawerContent, footer, bodyHeight);
        var rect = ScreenRect.FromSize(leftMin, new Vector2(width, heights.Sheet));
        if (ClickedOutsideDrawer(in rect))
        {
            ui.FilterPanelOpen = false;
            drawerOpenedAt = -1.0;
            ui.Rects.Remove(UiRects.FilterPanel);
            return;
        }

        // Until its content has been measured once the sheet is drawn unseen, so it never shows at the wrong height.
        var fade = DrawerFadeNow(now);
        ImGui.SetCursorScreenPos(leftMin);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 0f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        var open = ImGui.BeginChild("##filterDrawer", rect.Size, false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoSavedSettings);
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);
        if (open)
        {
            using var alpha = ImRaii.PushStyle(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * fade);
            DrawDrawerSheet(in rect, new Vector2(right, MathF.Max(bodyMin.Y, statusTop)), flair, in metrics, in tones);
            DrawDrawerHeader(in rect, header, row, flair, in metrics, in tones);

            // The body scrolls on its own; its side padding is the level's, and nothing it opens (a combo, the
            // per-category popup) inherits it.
            ImGui.SetCursorScreenPos(new Vector2(rect.Min.X, rect.Min.Y + header));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(UiMetrics.Px(metrics.PadX), 0f));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
            var bodyFlags = ImGuiWindowFlags.NoSavedSettings | (heights.Scrolls ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoScrollbar);
            var body = ImGui.BeginChild("##filterDrawerBody", new Vector2(width, MathF.Max(1f, heights.Body)), false, bodyFlags);
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();
            if (body)
            {
                drawerContent = filterPanel.DrawSheet(bundle, session.ViewedSnapshot, plugin.Settings);
            }

            ImGui.EndChild();
            DrawDrawerFooter(in rect, rect.Min.Y + header + heights.Body, footer, flair, in metrics, in tones);
        }

        ImGui.EndChild();
        ui.RecordRect(UiRects.FilterPanel, rect.Min, rect.Max);

        // Full: once the sheet is opaque (the tree was not drawn this frame, DrawNavigation), the column's sky shows below
        // it with the tree's own stars where they always are, never under the sheet (plan v7 UI-6, spec §3). Read from
        // what the column did, never recomputed after the sheet measured itself, so one frame never has both skies.
        if (Theme.ShowStars && treeHidden)
        {
            var columnMin = leftMin;
            var columnMax = leftMin + new Vector2(leftWidth, bodyHeight);
            var skyMin = new Vector2(columnMin.X + UiMetrics.Px(8f), rect.Max.Y + UiMetrics.Px(16f));
            var skyMax = new Vector2(columnMax.X - UiMetrics.Px(8f), columnMax.Y - UiMetrics.Px(8f));
            if (skyMax.Y - skyMin.Y > UiMetrics.Px(40f))
            {
                var size = (columnMax - columnMin) / UiMetrics.Scale;
                NightSky.Field(ImGui.GetWindowDrawList(), Core.Ui.SkySite.Tree, 0, TreePane.TreeStars.For(size.X, size.Y), columnMin, columnMax, skyMin, skyMax);
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

    /// <summary>The header's height (its row, plus the moon-road divider's band at Full) and its row alone, at this level and text size.</summary>
    private static (float Header, float Row) DrawerHeaderHeight(Flair flair, in DrawerMetrics metrics)
    {
        float titleLine;
        using (DrawerTitleRole(flair))
        {
            titleLine = ImGui.GetTextLineHeight();
        }

        var row = MathF.Max(UiMetrics.Px(metrics.Header), titleLine + UiMetrics.Px(flair == Flair.Plain ? 6f : 14f));
        return (row + UiMetrics.Px(metrics.Divider), row);
    }

    /// <summary>"Filters" is in the Title role at Full, the display size at Quiet, the body at Plain.</summary>
    private static Typography.Scope DrawerTitleRole(Flair flair) => flair switch
    {
        Flair.Full => Typography.Title(Strings.Filters),
        Flair.Quiet => Typography.Display(),
        _ => default,
    };

    /// <summary>
    /// The sheet under everything else in the drawer, at its level (spec §2.2). Full: a drop shadow straight down
    /// (0 14 30 at .55), an unoffset contact shadow on the list side (0 0 14 at .30), the raised gradient at .995, a
    /// MoonHigh highlight along the top, a brass edge on the right and bottom (the sides that face content), rounded 6 at
    /// the bottom right with a darker corner mark. Quiet: a separation shadow (0 8 20 at .45), a flat sheet one tone
    /// above the tree and a 1 px edge. Plain: the flat sheet and its 1 px line, square, no shadow.
    /// </summary>
    private static void DrawDrawerSheet(in ScreenRect rect, Vector2 bodyMax, Flair flair, in DrawerMetrics metrics, in DrawerTones tones)
    {
        var dl = ImGui.GetWindowDrawList();
        var min = rect.Min;
        var max = rect.Max;
        var alpha = ImGui.GetStyle().Alpha;
        var rounding = UiMetrics.Px(metrics.Rounding);
        var corners = rounding > 0f ? ImDrawFlags.RoundCornersBottomRight : ImDrawFlags.RoundCornersNone;

        // The shadows fall outside the child, to the right and below only: never over the rail or the toolbar, and never
        // past the body (<paramref name="bodyMax"/>: the status bar's top and the content's right edge), so they stay in
        // the window and off the status bar.
        dl.PushClipRect(min, Vector2.Min(max + new Vector2(UiMetrics.Px(48f)), Vector2.Max(max, bodyMax)), false);
        switch (flair)
        {
            case Flair.Full:
            {
                Ornament.DropShadow(dl, min, max, rounding, UiMetrics.Px(14f), UiMetrics.Px(30f), 0.55f * alpha);
                var contact = UiMetrics.Px(14f);
                var dark = Theme.CastShadow(0.30f * alpha);
                var clear = Theme.CastShadow(0f);
                dl.AddRectFilledMultiColor(new Vector2(max.X, min.Y), new Vector2(max.X + contact, max.Y - rounding), dark, clear, clear, dark);
                GradientFill(dl, min, max, tones.SheetTop with { W = DrawerTones.FullSheetAlpha }, tones.SheetFoot with { W = DrawerTones.FullSheetAlpha }, rounding, corners);
                dl.AddRectFilled(min, new Vector2(max.X - rounding, min.Y + UiMetrics.Hairline), ImGui.GetColorU32(Theme.Scene.TopHighlight with { W = 0.06f }));
                var first = dl.VtxBuffer.Size;
                EdgePath(dl, min, max, rounding, 0xFFFFFFFFu, UiMetrics.Hairline);
                BrassVertices(dl, first, min, max, alpha);
                CornerMark(dl, max, rounding, MathF.Round(UiMetrics.Px(9f)), alpha);
                break;
            }

            case Flair.Quiet:
                Ornament.DropShadow(dl, min, max, rounding, UiMetrics.Px(8f), UiMetrics.Px(20f), 0.45f * alpha);
                dl.AddRectFilled(min, max, ImGui.GetColorU32(tones.SheetTop), rounding, corners);
                EdgePath(dl, min, max, rounding, ImGui.GetColorU32(tones.Edge), UiMetrics.Hairline);
                break;

            default:
                dl.AddRectFilled(min, max, ImGui.GetColorU32(tones.SheetTop));
                EdgePath(dl, min, max, 0f, ImGui.GetColorU32(tones.Edge), UiMetrics.Hairline);
                break;
        }

        dl.PopClipRect();
    }

    /// <summary>A rectangle filled from <paramref name="top"/> down to <paramref name="foot"/>, rounded at <paramref name="corners"/>, at the style's alpha.</summary>
    private static void GradientFill(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector4 top, Vector4 foot, float rounding, ImDrawFlags corners)
    {
        var first = dl.VtxBuffer.Size;
        dl.AddRectFilled(min, max, 0xFFFFFFFFu, rounding, corners);
        var vertices = dl.VtxBuffer;
        var height = MathF.Max(1f, max.Y - min.Y);
        for (var i = first; i < vertices.Size; i++)
        {
            var vertex = vertices[i];
            var c = Vector4.Lerp(top, foot, Math.Clamp((vertex.Pos.Y - min.Y) / height, 0f, 1f));
            c.W *= (vertex.Col >> 24) / 255f;
            vertex.Col = ImGui.GetColorU32(c);
            vertices[i] = vertex;
        }
    }

    /// <summary>The sheet's edge on its right and bottom (the sides that face content), round the bottom-right corner.</summary>
    private static void EdgePath(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, uint color, float thickness)
    {
        var inset = thickness * 0.5f;
        dl.PathLineTo(new Vector2(max.X - inset, min.Y));
        if (rounding > 0f)
        {
            dl.PathArcTo(new Vector2(max.X - rounding, max.Y - rounding), rounding - inset, 0f, MathF.PI * 0.5f, 10);
        }
        else
        {
            dl.PathLineTo(new Vector2(max.X - inset, max.Y - inset));
        }

        dl.PathLineTo(new Vector2(min.X, max.Y - inset));
        dl.PathStroke(color, ImDrawFlags.None, thickness);
    }

    /// <summary>Recolours the vertices drawn since <paramref name="first"/> as the card's lit brass (<see cref="Ornament.Brass"/>, light from the upper left).</summary>
    private static void BrassVertices(ImDrawListPtr dl, int first, Vector2 min, Vector2 max, float alpha)
    {
        var vertices = dl.VtxBuffer;
        var size = max - min;
        var dir = new Vector2(0.139f, 0.990f);
        var span = MathF.Max(1f, MathF.Abs(size.X * dir.X) + MathF.Abs(size.Y * dir.Y));
        for (var i = first; i < vertices.Size; i++)
        {
            var vertex = vertices[i];
            var c = Ornament.Brass(Vector2.Dot(vertex.Pos - min, dir) / span);
            c.W = (vertex.Col >> 24) / 255f * alpha;
            vertex.Col = Theme.U32(c);
            vertices[i] = vertex;
        }
    }

    /// <summary>The 9 px darker brass mark round the sheet's bottom-right corner, overlapping the edge by a hair.</summary>
    private static void CornerMark(ImDrawListPtr dl, Vector2 max, float rounding, float size, float alpha)
    {
        var width = MathF.Max(1.5f, UiMetrics.Px(1.5f));
        var o = 0.25f * width;
        var ink = Theme.WithAlpha(Ornament.CornerShaded, alpha);
        dl.PathLineTo(new Vector2(max.X + o - (width * 0.5f), max.Y - size));
        dl.PathArcTo(new Vector2(max.X - rounding, max.Y - rounding), rounding + o - (width * 0.5f), 0f, MathF.PI * 0.5f, 10);
        dl.PathLineTo(new Vector2(max.X - size, max.Y + o - (width * 0.5f)));
        dl.PathStroke(ink, ImDrawFlags.None, width);
    }

    /// <summary>
    /// The header row: the filter glyph (not at Plain), "Filters" in the level's title face, a neutral count pill ("3 on";
    /// Plain: "· 3 on"), then pin and × at the end; under it the moon-road divider (Full), a hairline (Quiet), or the
    /// header band itself (Plain).
    /// </summary>
    private void DrawDrawerHeader(in ScreenRect rect, float header, float row, Flair flair, in DrawerMetrics metrics, in DrawerTones tones)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var mid = MathF.Round(rect.Min.Y + (row * 0.5f));
        if (flair == Flair.Plain)
        {
            dl.AddRectFilled(rect.Min, new Vector2(rect.Max.X, rect.Min.Y + row), ImGui.GetColorU32(tones.HeaderBand));
        }

        // Pin and close at the end.
        var side = MathF.Max(18f, UiMetrics.Px(metrics.Button));
        var gap = UiMetrics.Px(6f);
        var closeMin = new Vector2(rect.Max.X - UiMetrics.Px(flair == Flair.Plain ? 4f : 10f) - side, MathF.Round(mid - (side * 0.5f)));
        var pinMin = closeMin - new Vector2(side + gap, 0f);
        var pinned = plugin.Settings.FilterDrawerPinned;
        if (DrawerButton("##drawerPin", PinIcon, pinMin, side, pinned, pinned ? Strings.FilterDrawerUnpinTooltip : Strings.FilterDrawerPinTooltip, flair, in tones))
        {
            plugin.Settings.FilterDrawerPinned = !pinned;
            OnDisplayChanged();
        }

        if (DrawerButton("##drawerClose", DismissIcon, closeMin, side, false, Strings.FilterDrawerCloseTooltip, flair, in tones))
        {
            ui.FilterPanelOpen = false;
        }

        var x = rect.Min.X + UiMetrics.Px(flair switch { Flair.Full => 13f, Flair.Quiet => 12f, _ => 8f });
        var end = pinMin.X - UiMetrics.Px(8f);
        if (flair != Flair.Plain)
        {
            // A bare 13 px glyph, no disc: MoonHigh at Full, Mist at Quiet.
            ImGui.PushFont(UiBuilder.IconFont);
            var glyphSize = UiMetrics.Px(13f);
            var glyph = ImGui.CalcTextSize(FilterIcon) * (glyphSize / MathF.Max(1f, ImGui.GetFontSize()));
            dl.AddText(ImGui.GetFont(), glyphSize, new Vector2(x, MathF.Round(mid - (glyph.Y * 0.5f))), ImGui.GetColorU32(flair == Flair.Full ? Theme.GoldHigh : s.TextSecondary), FilterIcon);
            ImGui.PopFont();
            x += glyph.X + UiMetrics.Px(9f);
        }

        float titleWidth;
        using (DrawerTitleRole(flair))
        {
            var title = Strings.Filters;
            titleWidth = MathF.Min(ImGui.CalcTextSize(title).X, MathF.Max(1f, end - x));
            var at = new Vector2(x, MathF.Round(mid - (ImGui.GetTextLineHeight() * 0.5f)));
            if (flair == Flair.Full)
            {
                Chrome.EllipsisTextAt(dl, at + new Vector2(0f, 1f), MathF.Max(1f, end - x), title, ImGui.GetColorU32(s.Deep with { W = 0.5f }));
            }

            var ink = flair == Flair.Full ? Vector4.Lerp(s.Text, Theme.GoldHigh, 0.25f) : s.Text;
            Chrome.EllipsisTextAt(dl, at, MathF.Max(1f, end - x), title, ImGui.GetColorU32(ink));
        }

        x += titleWidth + UiMetrics.Px(9f);
        var count = FilterBadge.Count(ui.Filters);
        if (count > 0)
        {
            using var caption = Typography.Caption();
            var text = drawerOnText.For(count);
            var size = ImGui.CalcTextSize(text);
            if (flair == Flair.Plain)
            {
                var dot = DrawerCountDot;
                var label = new Vector2(x - UiMetrics.Px(5f), MathF.Round(mid - (size.Y * 0.5f)));
                var dotWidth = ImGui.CalcTextSize(dot).X + UiMetrics.Px(3f);
                if (label.X + dotWidth + size.X <= end)
                {
                    dl.AddText(label, ImGui.GetColorU32(s.TextSecondary), dot);
                    dl.AddText(label + new Vector2(dotWidth, 0f), ImGui.GetColorU32(s.TextSecondary), text);
                }
            }
            else
            {
                var height = MathF.Max(UiMetrics.Px(19f), size.Y + UiMetrics.Px(4f));
                var pillWidth = size.X + (2f * UiMetrics.Px(8f));
                if (x + pillWidth <= end)
                {
                    var min = new Vector2(x, MathF.Round(mid - (height * 0.5f)));
                    dl.AddRectFilled(min, min + new Vector2(pillWidth, height), ImGui.GetColorU32(tones.Pill), height * 0.5f);
                    dl.AddText(new Vector2(x + UiMetrics.Px(8f), MathF.Round(mid - (size.Y * 0.5f))), ImGui.GetColorU32(s.Text), text);
                }
            }
        }

        switch (flair)
        {
            case Flair.Full:
            {
                var first = dl.VtxBuffer.Size;
                Ornament.Divider(dl, new Vector2(rect.Center.X, rect.Min.Y + row + (UiMetrics.Px(metrics.Divider) * 0.5f) - UiMetrics.Px(2f)), rect.Width - UiMetrics.Px(24f), UiMetrics.Px(10f));
                Chrome.FadeVertices(dl, first, ImGui.GetStyle().Alpha);
                break;
            }

            case Flair.Quiet:
                dl.AddRectFilled(new Vector2(rect.Min.X, rect.Min.Y + header - UiMetrics.Hairline), new Vector2(rect.Max.X, rect.Min.Y + header), ImGui.GetColorU32(Theme.RuleColor));
                break;
        }
    }

    /// <summary>
    /// A header button: 26 px round at Full (a raised disc with a line border; pinned, a gold ring and tint) and at Quiet
    /// (a 1 px border; pinned, a silver wash), 20 px square with a 3 px radius at Plain. True on click.
    /// </summary>
    private static bool DrawerButton(string id, string icon, Vector2 min, float side, bool active, string tooltip, Flair flair, in DrawerTones tones)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(id, new Vector2(side));
        var hovered = ImGui.IsItemHovered();
        var max = min + new Vector2(side);
        var rounding = flair == Flair.Plain ? UiMetrics.Px(3f) : side * 0.5f;
        var gold = active && flair == Flair.Full;
        switch (flair)
        {
            case Flair.Full:
                dl.AddRectFilled(min, max, ImGui.GetColorU32(gold ? Theme.Gold with { W = 0.10f } : hovered ? s.Hover : Vector4.Lerp(s.Raised, s.Text, 0.04f)), rounding);
                dl.AddRect(min, max, ImGui.GetColorU32(gold ? Theme.Gold with { W = 0.5f } : s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
                break;
            case Flair.Quiet:
                if (hovered || active)
                {
                    dl.AddRectFilled(min, max, ImGui.GetColorU32(active ? s.Text with { W = 0.08f } : s.Hover), rounding);
                }

                dl.AddRect(min, max, ImGui.GetColorU32(tones.Edge), rounding, ImDrawFlags.None, UiMetrics.Hairline);
                break;
            default:
                if (hovered || active)
                {
                    dl.AddRectFilled(min, max, ImGui.GetColorU32(active ? s.Text with { W = 0.12f } : s.Hover), rounding);
                }

                break;
        }

        ImGui.PushFont(UiBuilder.IconFont);
        var iconPx = MathF.Round(side * 0.44f);
        var size = ImGui.CalcTextSize(icon) * (iconPx / MathF.Max(1f, ImGui.GetFontSize()));
        var ink = gold ? Theme.Accent : hovered || active ? s.Text : s.TextSecondary;
        dl.AddText(ImGui.GetFont(), iconPx, min + ((new Vector2(side) - size) * 0.5f), ImGui.GetColorU32(ink), icon);
        ImGui.PopFont();
        Chrome.FocusRing(rounding);
        if (hovered)
        {
            UiMetrics.Tooltip(tooltip);
        }

        return clicked;
    }

    /// <summary>
    /// The footer under the body: "Showing N of M" (the number in the text tone) and Reset at the end, a quiet text
    /// action that clears every filter and the search and offers Undo, shown disabled while there is nothing to clear.
    /// Over a brass rule at Full, a hairline at Quiet, its own band at Plain.
    /// </summary>
    private void DrawDrawerFooter(in ScreenRect rect, float top, float height, Flair flair, in DrawerMetrics metrics, in DrawerTones tones)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var min = new Vector2(rect.Min.X, top);
        var max = new Vector2(rect.Max.X, top + height);
        switch (flair)
        {
            case Flair.Full:
            {
                // Brass at its brightest in the middle, fading toward both ends.
                var line = UiMetrics.Hairline;
                var midX = MathF.Round((min.X + max.X) * 0.5f);
                var faint = ImGui.GetColorU32(s.Ornament with { W = 0.15f });
                var bright = ImGui.GetColorU32(s.OrnamentHigh with { W = 0.7f });
                dl.AddRectFilledMultiColor(min, new Vector2(midX, min.Y + line), faint, bright, bright, faint);
                dl.AddRectFilledMultiColor(new Vector2(midX, min.Y), new Vector2(max.X, min.Y + line), bright, faint, faint, bright);
                break;
            }

            case Flair.Quiet:
                dl.AddRectFilled(min, new Vector2(max.X, min.Y + UiMetrics.Hairline), ImGui.GetColorU32(Theme.RuleColor));
                break;
            default:
                dl.AddRectFilled(min, max, ImGui.GetColorU32(tones.FooterBand));
                break;
        }

        var mid = MathF.Round(top + (height * 0.5f));
        var lineHeight = ImGui.GetTextLineHeight();
        var textY = MathF.Round(mid - (lineHeight * 0.5f));
        var x = rect.Min.X + UiMetrics.Px(metrics.PadX);

        // Reset first, so "Showing" knows where it has to end.
        var canReset = FilterSummary.CanReset(ui.Filters, ui.SearchText);
        var plain = flair == Flair.Plain;
        var label = Strings.Reset;
        var labelWidth = ImGui.CalcTextSize(label).X;
        var iconPx = MathF.Round(lineHeight * 0.8f);
        float iconWidth;
        ImGui.PushFont(UiBuilder.IconFont);
        iconWidth = plain ? 0f : ImGui.CalcTextSize(ResetIcon).X * (iconPx / MathF.Max(1f, ImGui.GetFontSize()));
        ImGui.PopFont();
        var padX = plain ? 0f : UiMetrics.Px(11f);
        var resetWidth = (2f * padX) + iconWidth + (plain ? 0f : UiMetrics.Px(6f)) + labelWidth;
        var resetHeight = plain ? lineHeight : MathF.Max(UiMetrics.Px(28f), lineHeight + UiMetrics.Px(6f));
        var resetMin = new Vector2(rect.Max.X - UiMetrics.Px(plain ? metrics.PadX : metrics.PadX - 4f) - resetWidth, MathF.Round(mid - (resetHeight * 0.5f)));
        ImGui.SetCursorScreenPos(resetMin);
        var clicked = ImGui.InvisibleButton("##drawerReset", new Vector2(resetWidth, resetHeight));
        var hovered = canReset && ImGui.IsItemHovered();
        if (hovered && !plain)
        {
            dl.AddRectFilled(resetMin, resetMin + new Vector2(resetWidth, resetHeight), ImGui.GetColorU32(tones.Hover with { W = DrawerTones.HoverAlpha }), resetHeight * 0.5f);
        }

        Chrome.FocusRing(plain ? UiMetrics.Px(2f) : resetHeight * 0.5f);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(canReset ? Strings.ResetTooltip : Strings.FilterDrawerResetNothing);
        }

        var ink = ImGui.GetColorU32(!canReset ? s.TextDisabled : hovered ? s.Text : s.TextSecondary);
        var labelX = resetMin.X + padX;
        if (!plain)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            dl.AddText(ImGui.GetFont(), iconPx, new Vector2(labelX, MathF.Round(mid - (iconPx * 0.5f))), ink, ResetIcon);
            ImGui.PopFont();
            labelX += iconWidth + UiMetrics.Px(6f);
        }

        dl.AddText(new Vector2(labelX, textY), ink, label);
        if (plain && canReset)
        {
            var underline = MathF.Round(textY + lineHeight - UiMetrics.Px(1f));
            dl.AddLine(new Vector2(labelX, underline), new Vector2(labelX + labelWidth, underline), ink, UiMetrics.Hairline);
        }

        if (clicked && canReset)
        {
            filterPanel.ResetAll();
        }

        // "Showing 7 of 26": the number the filters keep in the text tone, the rest secondary.
        var (before, number, after) = ShowingParts(runner.Rows.Length, runner.TotalInScope);
        var room = resetMin.X - UiMetrics.Px(8f) - x;
        var beforeWidth = ImGui.CalcTextSize(before).X;
        var numberWidth = ImGui.CalcTextSize(number).X;
        var afterWidth = ImGui.CalcTextSize(after).X;
        if (beforeWidth + numberWidth + afterWidth > room)
        {
            Chrome.EllipsisTextAt(dl, new Vector2(x, textY), MathF.Max(1f, room), showingWhole, ImGui.GetColorU32(s.TextSecondary));
            return;
        }

        dl.AddText(new Vector2(x, textY), ImGui.GetColorU32(s.TextSecondary), before);
        dl.AddText(new Vector2(x + beforeWidth, textY), ImGui.GetColorU32(s.Text), number);
        dl.AddText(new Vector2(x + beforeWidth + numberWidth, textY), ImGui.GetColorU32(s.TextSecondary), after);
    }

    /// <summary>"Showing {0} of {1}" split round its number, rebuilt when the counts or the language change.</summary>
    private (string Before, string Number, string After) ShowingParts(int shown, int total)
    {
        if (showingKey != (shown, total, Loc.Version))
        {
            showingKey = (shown, total, Loc.Version);
            var culture = CultureInfo.CurrentCulture;
            var format = Strings.FilterDrawerShowingFormat;
            showingWhole = string.Format(culture, format, shown, total);
            var open = format.IndexOf("{0", StringComparison.Ordinal);
            var close = open < 0 ? -1 : format.IndexOf('}', open);
            if (close < 0)
            {
                showing = (showingWhole, string.Empty, string.Empty);
            }
            else
            {
                showing = (
                    string.Format(culture, format[..open], string.Empty, total),
                    string.Format(culture, format[open..(close + 1)], shown),
                    string.Format(culture, format[(close + 1)..], string.Empty, total));
            }
        }

        return showing;
    }

    private static readonly string FilterIcon = FontAwesomeIcon.Filter.ToIconString();
    private static readonly string ResetIcon = FontAwesomeIcon.UndoAlt.ToIconString();
}
