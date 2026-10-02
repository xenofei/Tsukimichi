using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Config;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// "Clear my blues" (P3), the My blues tab: every unlock quest the viewed character has left, by expansion and zone
/// in story order (<see cref="UnlockPlan"/>). <see cref="DrawLeft"/> holds the summary, the filters (one toggle chip
/// per kind with its count, Ready only, Sprout mode) and the expansion list; <see cref="DrawMain"/> the "Copy as
/// checklist" button and one card per expansion, folded but for the one opened, with the zone groups and a row per
/// quest: state moon, name (click shows it in the detail pane), kind pills, the status line, Flag, Reveal, Teleport
/// and Walk (Go to giver in the row's right-click and "…" menus). Each
/// card can pin its expansion's block to the todo overlay, and send its quests to Questionable's priority list (the
/// paper-plane button, feature plan v5 1.6.0, <see cref="QuestionableActions"/>).
/// <para>
/// Sprout mode is the plan's own switch, turned on whenever the tab opens while the Journal's Sprout mode quick view
/// is on, so revealing a quest in the Journal (which clears quick views) does not widen the plan. The filtered plan,
/// the chip counts and the card strings are rebuilt only when the plan's revision or a filter changes; rows outside
/// the scrolled view draw a spacer only.
/// </para>
/// </summary>
public sealed class PlanPane
{
    private static readonly string FoldedGlyph = Chrome.Icon(FontAwesomeIcon.CaretRight);
    private static readonly string OpenGlyph = Chrome.Icon(FontAwesomeIcon.CaretDown);

    /// <summary>How long "Copied N quests" stays beside the button.</summary>
    private const double CopiedSeconds = 2.5;

    private readonly SessionState session;
    private readonly PlanSource source;
    private readonly GameLinks links;
    private readonly DiscordCopy discordCopy = new();
    private readonly Configuration settings;
    private readonly Action save;

    // Filters (session only).
    private ushort kinds = UnlockKinds.AllMask;
    private bool readyOnly;
    private bool sprout;
    private int lastDrawFrame = -2;

    // Folded state per expansion (true = open) and the card the left list asked to scroll to.
    private readonly Dictionary<byte, bool> open = [];
    private byte? scrollTo;

    // The view built from the plan and the filters.
    private (int Revision, ushort Kinds, bool Ready, int MaxExpansion) viewKey = (-1, 0, false, -1);
    private UnlockPlan view = UnlockPlan.Empty;
    private readonly int[] kindCounts = new int[UnlockKinds.All.Length];
    private readonly string[] kindLabels = new string[UnlockKinds.All.Length];
    private int hiddenExpansions;
    private string hiddenText = string.Empty;

    // The free-trial view (1.9.0): whether the view was built under it, and "N unlock quests in later expansions".
    private bool viewTrial;
    private string beyondTrialText = string.Empty;
    private string summary = string.Empty;
    private string showing = string.Empty;
    // Per expansion: the card title, its Ready count, the left list's count and the left list row's id.
    private readonly Dictionary<byte, (string Header, string Ready, string Count, string Label)> cardText = [];
    private readonly Dictionary<uint, string> zoneNames = [];

    private string copied = string.Empty;
    private double copiedAt = double.NegativeInfinity;

    /// <summary>The shared Questionable hand-offs (1.6.0); null hides the cards' Send to Questionable button.</summary>
    public QuestionableActions? Questionable { get; init; }

    public PlanPane(SessionState session, PlanSource source, GameLinks links, Configuration settings, Action save)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
    }

    /// <summary>Left column: summary, filters and the expansion list.</summary>
    public void DrawLeft(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("planLeft");
        Refresh(ui);

        using (Typography.Display())
        {
            ImGui.TextUnformatted(Strings.PlanTitle);
        }

        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextWrapped(summary);
        }

        ImGui.Spacing();
        SectionHeading.Draw(Strings.PlanShow);
        var first = true;
        if (FlowChip("##ready", Strings.PlanReadyOnly, readyOnly, Strings.PlanReadyOnlyTooltip, ref first, enabled: session.ViewedSnapshot is not null))
        {
            readyOnly = !readyOnly;
        }

        if (FlowChip("##sprout", Strings.PlanSprout, sprout, Strings.PlanSproutTooltip, ref first))
        {
            sprout = !sprout;
        }

        ImGui.Spacing();
        SectionHeading.Draw(Strings.PlanKinds);
        first = true;
        for (var i = 0; i < UnlockKinds.All.Length; i++)
        {
            var bit = UnlockKinds.Bit(UnlockKinds.All[i]);
            var on = kinds != UnlockKinds.AllMask && (kinds & bit) != 0;
            if (FlowChip(KindChipId(i), kindLabels[i], on, Strings.PlanKindChipTooltip, ref first))
            {
                // From "all kinds" the first click narrows to that kind; clearing the last kind shows all again.
                kinds = kinds == UnlockKinds.AllMask ? bit : (ushort)(kinds ^ bit);
                if (kinds == 0)
                {
                    kinds = UnlockKinds.AllMask;
                }
            }
        }

        if (kinds != UnlockKinds.AllMask && FlowChip("##allKinds", Strings.PlanAllKinds, false, Strings.PlanAllKindsTooltip, ref first))
        {
            kinds = UnlockKinds.AllMask;
        }

        Refresh(ui);
        ImGui.Spacing();

        // Moon Road headings (R3 #9): the heading's own brass rule replaces the hairline that separated the list under Plain.
        if (!Theme.ShowRules)
        {
            Chrome.Hairline();
        }

        SectionHeading.Draw(Strings.PlanExpansions);
        // Name, then the count at the right edge (UI audit §3): the name ends in an ellipsis before the count, and the
        // count gives way (to the tooltip) only when the name would keep less than a few letters.
        var gap = ImGui.GetStyle().ItemSpacing.X;
        Span<float> countPart = stackalloc float[1];
        Span<bool> countShown = stackalloc bool[1];
        for (var i = 0; i < view.Expansions.Count; i++)
        {
            var block = view.Expansions[i];
            var text = cardText[block.Expansion];
            var isOpen = IsOpen(block);
            var rowMin = ImGui.GetCursorScreenPos();
            var room = Chrome.RoomX();
            var countWidth = ImGui.CalcTextSize(text.Count).X;
            countPart[0] = countWidth + gap;
            var fit = RowFit.Fit(room, ImGui.CalcTextSize(block.Name).X, UiMetrics.Px(LayoutBudgets.RowNameMinLogical), countPart, countShown);
            if (ImGui.Selectable(text.Label, isOpen, ImGuiSelectableFlags.None, new Vector2(MathF.Max(1f, room), 0f)))
            {
                open[block.Expansion] = true;
                scrollTo = block.Expansion;
            }

            var hovered = ImGui.IsItemHovered();
            var dl = ImGui.GetWindowDrawList();
            var cut = Chrome.EllipsisTextAt(dl, rowMin, fit.NameRoom, block.Name, ImGui.GetColorU32(ImGuiCol.Text));
            if (countShown[0])
            {
                dl.AddText(new Vector2(rowMin.X + room - countWidth, rowMin.Y), ImGui.GetColorU32(ImGuiCol.TextDisabled), text.Count);
            }

            if (hovered)
            {
                if (!countShown[0])
                {
                    UiMetrics.Tooltip(block.Name, text.Count);
                }
                else if (cut)
                {
                    UiMetrics.Tooltip(block.Name, Strings.PlanExpansionClickHint);
                }
                else
                {
                    UiMetrics.Tooltip(Strings.PlanExpansionClickHint);
                }
            }
        }

        if (hiddenExpansions > 0)
        {
            using var note = Theme.PushText(Theme.Surface.TextTertiary);
            ImGui.TextWrapped(hiddenText);
        }

        if (beyondTrialText.Length > 0)
        {
            ImGui.Spacing();
            SectionHeading.Draw(Strings.TrialBeyondHeading);
            using var note = Theme.PushText(Theme.Surface.TextTertiary);
            ImGui.TextWrapped(beyondTrialText);
        }
    }

    /// <summary>Centre column: the copy button, then one card per expansion.</summary>
    public void DrawMain(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("planMain");
        Refresh(ui);

        if (session.Bundle is null)
        {
            ImGui.TextDisabled(Strings.PlanLoading);
            return;
        }

        using (ImRaii.Disabled(view.IsEmpty))
        {
            if (ImGui.Button(Strings.PlanCopy))
            {
                CopyChecklist();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.PlanCopyTooltip);
        }

        // Copy for Discord (1.8.0): bullets instead of task boxes, optional links (never on a masked name), 2,000-character parts.
        if (!view.IsEmpty)
        {
            Chrome.SameLineOrWrap(ImGui.CalcTextSize(Strings.LinksCopyDiscord).X + (ImGui.GetStyle().FramePadding.X * 2f));
            discordCopy.Draw("plan", view, (Pane: this, View: view), static (s, addLinks) => s.Pane.PlanDiscordText(s.View, addLinks));
        }

        // "Flag next stop" (1.6.0, C3 C): the first quest the list shows that can be started now.
        var next = NextStop();
        Chrome.SameLineOrWrap(ImGui.CalcTextSize(Strings.RouteFlagNextStop).X + (ImGui.GetStyle().FramePadding.X * 2f));
        using (ImRaii.Disabled(next is null))
        {
            if (ImGui.Button(Strings.RouteFlagNextStop) && next is not null)
            {
                links.FlagMap(next);
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(next is not null ? Strings.PlanFlagNextStopTooltip : Strings.PlanFlagNextStopUnavailable);
        }

        var justCopied = ImGui.GetTime() - copiedAt < CopiedSeconds;
        Chrome.SameLineOrWrap(ImGui.CalcTextSize(justCopied ? copied : showing).X);
        if (justCopied)
        {
            using var confirmation = Theme.PushText(Theme.Surface.Text);
            ImGui.TextUnformatted(copied);
        }
        else
        {
            ImGui.TextDisabled(showing);
        }

        if (view.IsEmpty)
        {
            // The shared empty state (1.7.0, onboarding proposal 9): a full moon when nothing is left, else a reset.
            ImGui.Spacing();
            if (source.Plan.IsEmpty && session.ViewedSnapshot is not null)
            {
                EmptyState.DrawWithAction(Strings.PlanEmptyAllDoneHeading, Strings.PlanEmptyAllDone, null, moon: QuestState.Completed);
            }
            else if (EmptyState.DrawWithAction(Strings.PlanEmptyFiltered, Strings.PlanEmptyFilteredBody, Strings.ResetFilters, moon: QuestState.Blocked) == EmptyState.ActionClicked)
            {
                readyOnly = false;
                sprout = false;
                kinds = UnlockKinds.AllMask;
            }

            return;
        }

        using var list = ImRaii.Child("##planCards", Vector2.Zero);
        if (!list)
        {
            return;
        }

        ui.RecordWindow(UiRects.PlanCards);

        for (var b = 0; b < view.Expansions.Count; b++)
        {
            var block = view.Expansions[b];
            if (scrollTo == block.Expansion)
            {
                ImGui.SetScrollHereY(0f);
                scrollTo = null;
            }

            DrawCard(ui, block);
            ImGui.Spacing();
        }
    }

    /// <summary>The first quest of the view, in plan order, that can be started now and whose giver can be flagged; null when none.</summary>
    private QuestRecord? NextStop()
    {
        if (nextStopKey == viewKey)
        {
            return nextStop;
        }

        nextStopKey = viewKey;
        nextStop = null;
        foreach (var entry in view.Entries)
        {
            if (entry.IsReady && links.CanFlagMap(entry.Quest))
            {
                nextStop = entry.Quest;
                break;
            }
        }

        return nextStop;
    }

    private (int Revision, ushort Kinds, bool Ready, int MaxExpansion) nextStopKey = (-2, 0, false, -1);
    private QuestRecord? nextStop;

    /// <summary>
    /// "Route to this" for a row (1.6.0): the route to the duty or system it opens (every quest opening it is a way
    /// in), else to the quest itself.
    /// </summary>
    private void OpenRouteTo(UiState ui, PlanEntry entry)
    {
        if (session.Bundle is not { } bundle)
        {
            return;
        }

        var label = entry.Unlocks.Count > 0 && entry.Unlocks[0] is { Inherited: false, Name.Length: > 0 } unlock ? unlock.Name : entry.Name;
        ui.OpenRoute(Core.Route.RouteTarget.ForUnlockQuest(entry.Quest, bundle.Catalog, RewardEntries?.Invoke(), label));
    }

    /// <summary>The Moonlit catalog's entries (curated duty and system unlocks included), for "Route to this"; null routes to the quest alone.</summary>
    public Func<IEnumerable<Core.Model.UniqueRewardEntry>>? RewardEntries { get; set; }

    private const string RowContextId = "##planRowContext";

    private void DrawCard(UiState ui, PlanExpansion block)
    {
        var text = cardText[block.Expansion];
        var isOpen = IsOpen(block);
        Chrome.BeginCard(block.Expansion);

        // Header: fold caret and title (one click target), the Ready count in gold, Route and Pin right-aligned.
        var pinned = settings.TodoPlanExpansion == block.Expansion;
        var pinLabel = pinned ? Strings.PlanUnpin : Strings.PlanPin;
        var right = ImGui.GetWindowContentRegionMax().X - UiMetrics.Px(10f);
        var pinWidth = ImGui.CalcTextSize(pinLabel).X + ImGui.GetStyle().FramePadding.X * 2f;
        var routeWidth = ImGui.CalcTextSize(Strings.PlanRouteBlues).X + ImGui.GetStyle().FramePadding.X * 2f;
        var headerStart = ImGui.GetCursorPos();
        var questionableWidth = Questionable is null ? 0f : UiMetrics.MinTarget + UiMetrics.Px(4f);
        var titleWidth = MathF.Max(1f, right - pinWidth - routeWidth - questionableWidth - UiMetrics.Px(14f) - headerStart.X);
        if (ImGui.InvisibleButton("##fold", new Vector2(titleWidth, ImGui.GetFrameHeight())))
        {
            open[block.Expansion] = !isOpen;
            isOpen = !isOpen;
        }

        var hovered = ImGui.IsItemHovered();
        Chrome.FocusRing();

        var min = ImGui.GetItemRectMin();
        var dl = ImGui.GetWindowDrawList();
        var textY = min.Y + (ImGui.GetFrameHeight() - ImGui.GetTextLineHeight()) * 0.5f;
        ImGui.PushFont(UiBuilder.IconFont);
        dl.AddText(new Vector2(min.X, textY), Theme.U32(Theme.Surface.TextSecondary), isOpen ? OpenGlyph : FoldedGlyph);
        ImGui.PopFont();

        // The title ends in an ellipsis short of Pin (UI audit §3); the Ready count after it gives way first.
        var x = min.X + UiMetrics.Px(16f);
        Span<float> readyPart = stackalloc float[1];
        Span<bool> readyShown = stackalloc bool[1];
        readyPart[0] = block.ReadyCount > 0 ? ImGui.CalcTextSize(text.Ready).X + UiMetrics.Px(10f) : 0f;
        var headerWidth = ImGui.CalcTextSize(text.Header).X;
        var fit = RowFit.Fit(min.X + titleWidth - x, headerWidth, UiMetrics.Px(LayoutBudgets.RowNameMinLogical), readyPart, readyShown);
        var cut = Chrome.EllipsisTextAt(dl, new Vector2(x, textY), fit.NameRoom, text.Header, Theme.U32(Theme.Surface.Text), headerWidth);
        if (block.ReadyCount > 0 && readyShown[0])
        {
            dl.AddText(new Vector2(x + MathF.Min(headerWidth, fit.NameRoom) + UiMetrics.Px(10f), textY), Theme.AccentU32, text.Ready);
        }

        if (hovered)
        {
            if (block.ReadyCount > 0 && !readyShown[0])
            {
                UiMetrics.Tooltip(text.Header, text.Ready);
            }
            else if (cut)
            {
                UiMetrics.Tooltip(text.Header, Strings.PlanCardToggleTooltip);
            }
            else
            {
                UiMetrics.Tooltip(Strings.PlanCardToggleTooltip);
            }
        }

        if (Questionable is { } questionable)
        {
            // Send to Questionable: the expansion's quests as the card lists them (filters applied), in story order.
            ImGui.SetCursorPos(new Vector2(right - pinWidth - routeWidth - UiMetrics.Px(6f) - questionableWidth, headerStart.Y + ((ImGui.GetFrameHeight() - UiMetrics.MinTarget) * 0.5f)));
            questionable.DrawIconButton(MainWindow.QuestionableHost, "##questionable", block, static b => RowIdsOf(b), Strings.QuestionableSendExpansionTooltip);
        }

        // "Route": every quest the card lists (the filters apply) in one route through their prerequisites (1.6.0).
        ImGui.SetCursorPos(new Vector2(right - pinWidth - routeWidth - UiMetrics.Px(6f), headerStart.Y));
        if (ImGui.Button(Strings.PlanRouteBlues))
        {
            ui.OpenRoute(Core.Route.RouteTarget.ForBlues(block));
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.PlanRouteBluesTooltip);
        }

        ImGui.SetCursorPos(new Vector2(right - pinWidth, headerStart.Y));
        if (ImGui.Button(pinLabel))
        {
            if (pinned)
            {
                settings.TodoPlanExpansion = -1;
            }
            else
            {
                settings.TodoPlanExpansion = block.Expansion;
                settings.TodoShowPlan = true;
                settings.TodoOverlayEnabled = true;
            }

            save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(pinned ? Strings.PlanUnpinTooltip : Strings.PlanPinTooltip);
        }

        if (isOpen)
        {
            for (var z = 0; z < block.Zones.Count; z++)
            {
                var zone = block.Zones[z];
                ImGui.Spacing();
                Chrome.FitText(ZoneLabel(zone), Theme.U32(Theme.Surface.TextTertiary));

                for (var i = 0; i < zone.Entries.Count; i++)
                {
                    DrawRow(ui, zone.Entries[i]);
                }
            }
        }

        Chrome.EndCard();
    }

    private static IEnumerable<uint> RowIdsOf(PlanExpansion block)
    {
        foreach (var entry in block.Entries)
        {
            yield return entry.Quest.RowId;
        }
    }

    /// <summary>The "…" menu of a row whose buttons folded (under <see cref="LayoutBudgets.PlanMenuLogical"/>).</summary>
    private const string RowMenuId = "##planRowMenu";

    /// <summary>Kinds a plan row can show at most (every unlock kind).</summary>
    private static readonly int MaxKinds = UnlockKinds.All.Length;

    /// <summary>
    /// One quest (design v4 §7.8, the mockup's "My blues at 360 px"): from <see cref="LayoutBudgets.PlanOneLineLogical"/>
    /// plus the travel buttons' width, the moon, name, kind pills, status, Flag, Reveal, Teleport and Walk on one line;
    /// under it two lines, the status (state word never cut, the reason ellipsised) under the name, Flag and Reveal
    /// on the right of the first line and Teleport and Walk under them; under <see cref="LayoutBudgets.PlanMenuLogical"/>
    /// the buttons fold into one "…" menu. Go to giver is in the row's menus (right-click, "…"). Pills are sized to
    /// their text and, once two-line and under <see cref="PaneFit.PlanAllKindsLogical"/>, only the primary kind shows.
    /// </summary>
    private void DrawRow(UiState ui, PlanEntry entry)
    {
        var quest = entry.Quest;
        var line = ImGui.GetTextLineHeight();
        var glyph = UiMetrics.InlineGlyphSize(line);
        var frame = ImGui.GetFrameHeight();
        var firstLine = MathF.Max(frame, glyph) + UiMetrics.Px(2f);
        var start = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X - UiMetrics.Px(10f) - start.X);
        var logical = width / UiMetrics.Scale;
        var tier = PaneFit.PlanTier(logical);
        var style = ImGui.GetStyle();
        var flagReveal = ImGui.CalcTextSize(Strings.PlanFlag).X + ImGui.CalcTextSize(Strings.PlanReveal).X + (style.FramePadding.X * 4f) + style.ItemSpacing.X;
        var travel = TravelControls.ButtonsWidth(links, Strings.PlanTeleport);

        // The travel buttons keep the one-line row's middle as wide as before they came: one line only from the old
        // breakpoint plus their width, else they go under Flag and Reveal.
        if (tier == PlanRowTier.OneLine && logical < LayoutBudgets.PlanOneLineLogical + (travel / UiMetrics.Scale))
        {
            tier = PlanRowTier.TwoLine;
        }

        var height = tier == PlanRowTier.OneLine ? firstLine : firstLine + line + UiMetrics.Px(4f);
        var size = new Vector2(width, height);
        if (!ImGui.IsRectVisible(size))
        {
            ImGui.Dummy(size);
            return;
        }

        using var id = ImRaii.PushId((int)quest.RowId);
        var gap = UiMetrics.Px(8f);
        var menu = tier == PlanRowTier.TwoLineMenu;
        var moreSize = MathF.Min(UiMetrics.MinTarget, height);

        // ButtonsWidth counts the item spacing before Teleport: on a line of their own it is not drawn.
        var actionsWidth = menu ? moreSize
            : tier == PlanRowTier.OneLine ? flagReveal + travel
            : MathF.Max(flagReveal, travel - style.ItemSpacing.X);
        var actionsX = start.X + width - actionsWidth;
        var nameX = start.X + glyph + gap;
        var textY = start.Y + ((firstLine - line) * 0.5f);
        var dl = ImGui.GetWindowDrawList();

        // State moon, on the first line.
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + ((firstLine - glyph) * 0.5f)));
        MoonGlyph.DrawInline(entry.State, glyph);
        if (ImGui.IsItemHovered())
        {
            session.States.TryGetValue(quest.RowId, out var evaluation);
            UiMetrics.StateTooltip(entry.State, evaluation, quest, session.Names, session.States);
        }

        // Kind pills, sized to their text: every distinct kind, or the primary one alone once two-line and narrow.
        Span<UnlockKind> kinds = stackalloc UnlockKind[MaxKinds];
        Span<float> parts = stackalloc float[MaxKinds];
        Span<bool> shown = stackalloc bool[MaxKinds];
        var kindCount = Kinds(entry, tier == PlanRowTier.OneLine || PaneFit.PlanAllKinds(logical) ? MaxKinds : 1, kinds);
        using (Typography.Caption())
        {
            for (var i = 0; i < kindCount; i++)
            {
                parts[i] = PillSize(Strings.PlanKindName(kinds[i])).X + (i == 0 ? gap : UiMetrics.Px(4f));
            }
        }

        float nameRoom, pillEnd, statusX, statusY, statusWidth;
        if (tier == PlanRowTier.OneLine)
        {
            // Name, pills and status in columns that line up from row to row; the status keeps its state word.
            var stateWord = entry.StatusText.AsSpan(0, TableGeometry.StateWordLength(entry.StatusText));
            var statusMin = ImGui.CalcTextSize(stateWord).X + UiMetrics.Px(24f);
            var (name, pills, status) = PaneFit.PlanOneLine(actionsX - nameX - (gap * 3f), statusMin);
            nameRoom = name;
            pillEnd = nameX + name + gap + pills;
            statusX = pillEnd + gap;
            statusY = textY;
            statusWidth = status;
        }
        else
        {
            // The name and the pills after it share the first line (RowFit drops pills before the name gets short);
            // the status has the second line, up to the buttons.
            var nameWidth = ImGui.CalcTextSize(entry.Name).X;
            var fit = RowFit.Fit(actionsX - gap - nameX, nameWidth, UiMetrics.Px(LayoutBudgets.RowNameMinLogical), parts[..kindCount], shown);
            nameRoom = MathF.Min(nameWidth, fit.NameRoom);
            pillEnd = nameX + nameRoom + fit.PartsWidth;
            statusX = nameX;
            statusY = start.Y + firstLine + UiMetrics.Px(1f);
            statusWidth = actionsX - gap - nameX;
        }

        // Name: selects the quest for the detail pane; the tooltip carries the whole name.
        ImGui.SetCursorScreenPos(new Vector2(nameX, textY));
        if (Chrome.EllipsisSelectable(entry.Name, ui.SelectedRowId == quest.RowId, MathF.Max(1f, nameRoom), out _))
        {
            ui.SelectedRowId = quest.RowId;
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(entry.Name, Strings.PlanRowClickHint);
        }

        using (var context = ImRaii.ContextPopupItem(RowContextId))
        {
            if (context)
            {
                // Opened from the cards' child window (own font scale 1), so the menu scales itself.
                UiMetrics.ApplyFontScale();
                if (ImGui.MenuItem(Strings.PlanRouteToThis))
                {
                    OpenRouteTo(ui, entry);
                }

                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.PlanRouteToThisTooltip);
                }

                // Teleport, Walk to giver and Go to giver, visible and disabled with the reason when they cannot run.
                ImGui.Separator();
                TravelControls.MenuItems(links, quest, Strings.PlanTeleport);

                // "Open on…" (1.8.0): the quest's page on the Lodestone, Garland Tools, the wiki or Teamcraft.
                links.DrawOpenOnMenu(quest, session.Spoilers.IsMasked(quest), entry.Name);
            }
        }

        // The painted pills get an invisible item, so their tooltip honours popups, window hover and keyboard focus.
        var pillX = nameX + nameRoom;
        var pillsEnd = DrawPills(dl, kinds[..kindCount], parts, pillX, pillEnd, start.Y, firstLine);
        if (pillsEnd > pillX)
        {
            ImGui.SetCursorScreenPos(new Vector2(pillX, start.Y));
            ImGui.InvisibleButton("##pills", new Vector2(pillsEnd - pillX, firstLine));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(UnlocksTooltip(entry));
            }
        }

        // Status: the state word never cut, the reason ellipsised, the whole line on hover when cut.
        if (entry.StatusText.Length > 0 && statusWidth > 0f)
        {
            var s = Theme.Surface;
            var live = session.ViewedSnapshot is not null;
            ImGui.SetCursorScreenPos(new Vector2(statusX, statusY));
            Chrome.StatusText(entry.StatusText, statusWidth, live ? s.Text : s.TextTertiary, live ? s.TextSecondary : s.TextTertiary);
        }

        if (menu)
        {
            DrawRowMenu(ui, entry, new Vector2(actionsX, start.Y + ((height - moreSize) * 0.5f)), moreSize);
        }
        else if (tier == PlanRowTier.OneLine)
        {
            DrawRowButtons(ui, quest, new Vector2(actionsX, start.Y + ((height - frame) * 0.5f)), travelBelow: null);
        }
        else
        {
            // Flag and Reveal right-aligned on the first line, Teleport and Walk right-aligned under them.
            var right = start.X + width;
            DrawRowButtons(
                ui,
                quest,
                new Vector2(right - flagReveal, start.Y + ((firstLine - frame) * 0.5f)),
                travelBelow: new Vector2(right - (travel - style.ItemSpacing.X), statusY));
        }

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(size);
    }

    /// <summary>The row's distinct kinds in precedence order, at most <paramref name="max"/>, into <paramref name="kinds"/>; returns the count.</summary>
    private static int Kinds(PlanEntry entry, int max, Span<UnlockKind> kinds)
    {
        var count = 0;
        var unlocks = entry.Unlocks;
        for (var i = 0; i < unlocks.Count && count < max && count < kinds.Length; i++)
        {
            var kind = unlocks[i].Kind;
            if (count > 0 && kinds[count - 1] == kind)
            {
                continue;
            }

            kinds[count++] = kind;
        }

        return count;
    }

    /// <summary>A kind pill's size; measure in the caption role.</summary>
    private static Vector2 PillSize(string label) =>
        ImGui.CalcTextSize(label) + new Vector2(UiMetrics.Px(7f) * 2f, UiMetrics.Px(2f) * 2f);

    /// <summary>
    /// Paints the kind pills from <paramref name="x"/>, each after its gap (<paramref name="parts"/> holds gap and
    /// pill), while they end by <paramref name="end"/>. Returns where the last pill drawn ends.
    /// </summary>
    private static float DrawPills(ImDrawListPtr dl, ReadOnlySpan<UnlockKind> kinds, ReadOnlySpan<float> parts, float x, float end, float top, float height)
    {
        using var caption = Typography.Caption();
        var tone = Theme.Surface.TextSecondary;
        for (var i = 0; i < kinds.Length && x + parts[i] <= end + 0.5f; i++)
        {
            var label = Strings.PlanKindName(kinds[i]);
            var pill = PillSize(label);
            Chrome.PillAt(dl, new Vector2(x + parts[i] - pill.X, top + ((height - pill.Y) * 0.5f)), pill, label,
                Theme.WithAlpha(tone, 0.12f), Theme.WithAlpha(tone, 0.45f), Theme.U32(tone));
            x += parts[i];
        }

        return x;
    }

    /// <summary>
    /// Flag the giver and Reveal in the Journal, side by side from <paramref name="min"/>, then Teleport and Walk: on
    /// the same line, or from <paramref name="travelBelow"/> when given (the two-line row).
    /// </summary>
    private void DrawRowButtons(UiState ui, QuestRecord quest, Vector2 min, Vector2? travelBelow)
    {
        ImGui.SetCursorScreenPos(min);
        using (ImRaii.Disabled(!links.CanFlagMap(quest)))
        {
            if (ImGui.SmallButton(Strings.PlanFlag))
            {
                links.FlagMap(quest);
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.PlanFlagTooltip);
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.PlanReveal))
        {
            ui.Reveal(quest);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.PlanRevealTooltip);
        }

        if (travelBelow is { } below)
        {
            // TravelControls.Buttons opens with SameLine: an empty item one spacing to the left puts Teleport at below.
            ImGui.SetCursorScreenPos(new Vector2(below.X - ImGui.GetStyle().ItemSpacing.X, below.Y));
            ImGui.Dummy(new Vector2(0f, ImGui.GetTextLineHeight()));
        }

        TravelControls.Buttons(links, quest, Strings.PlanTeleport);
    }

    /// <summary>Flag, Teleport, Walk, Go to giver, Reveal and Route to this folded into one "…" button at <paramref name="min"/> and its menu (a narrow pane).</summary>
    private void DrawRowMenu(UiState ui, PlanEntry entry, Vector2 min, float size)
    {
        var quest = entry.Quest;
        Keyboard.MoreButton("##more", RowMenuId, min, size);
        using var popup = ImRaii.Popup(RowMenuId);
        if (!popup)
        {
            return;
        }

        // Opened from the cards' child window (own font scale 1), so the menu scales itself.
        UiMetrics.ApplyFontScale();
        if (ImGui.MenuItem(Strings.PlanFlag, enabled: links.CanFlagMap(quest)))
        {
            links.FlagMap(quest);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.PlanFlagTooltip);
        }

        TravelControls.MenuItems(links, quest, Strings.PlanTeleport);
        if (ImGui.MenuItem(Strings.PlanReveal))
        {
            ui.Reveal(quest);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.PlanRevealTooltip);
        }

        if (ImGui.MenuItem(Strings.PlanRouteToThis))
        {
            OpenRouteTo(ui, entry);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.PlanRouteToThisTooltip);
        }

        links.DrawOpenOnMenu(quest, session.Spoilers.IsMasked(quest), entry.Name);
    }

    private static string UnlocksTooltip(PlanEntry entry)
    {
        var text = PlanChecklist.UnlocksText(entry);
        return text.Length > 0 ? text : Strings.PlanKindName(entry.PrimaryKind);
    }

    /// <summary>
    /// A toggle chip flowing onto the next line when the row is full: a pill painted silver-tinted when on, raised when
    /// off, with the focus ring; returns true on the click that flips it.
    /// </summary>
    private static bool FlowChip(string id, string label, bool on, string tooltip, ref bool first, bool enabled = true)
    {
        var padX = UiMetrics.Px(9f);
        var height = MathF.Max(ImGui.GetTextLineHeight() + UiMetrics.Px(6f), UiMetrics.Px(22f));
        var size = new Vector2(ImGui.CalcTextSize(label).X + padX * 2f, height);
        if (!first)
        {
            ImGui.SameLine(0f, UiMetrics.Px(4f));
            if (ImGui.GetCursorPosX() + size.X > ImGui.GetWindowContentRegionMax().X)
            {
                ImGui.NewLine();
            }
        }

        first = false;
        bool clicked;
        using (ImRaii.Disabled(!enabled))
        {
            clicked = ImGui.InvisibleButton(id, size);
        }

        var hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
        var hover = Motion.Lerp(ImGuiP.GetItemID(), hovered && enabled ? 1f : 0f);
        var s = Theme.Surface;
        uint fill, border, ink;
        if (on)
        {
            // A selection, not a call to action: silver, like the other active segments.
            fill = Theme.WithAlpha(s.Text, 0.14f + 0.08f * hover);
            border = Theme.WithAlpha(s.Text, 0.55f);
            ink = Theme.U32(s.Text);
        }
        else
        {
            fill = Theme.U32(Vector4.Lerp(s.Raised, s.Hover, hover));
            border = Theme.U32(s.Line);
            ink = Theme.U32(enabled ? (hovered ? s.Text : s.TextSecondary) : s.TextDisabled);
        }

        Chrome.PillAt(ImGui.GetWindowDrawList(), ImGui.GetItemRectMin(), size, label, fill, border, ink);
        Chrome.FocusRing(height * 0.5f);
        if (hovered)
        {
            UiMetrics.Tooltip(enabled ? tooltip : tooltip + "\n" + Strings.NeedsSnapshot);
        }

        return clicked && enabled;
    }

    private static readonly string[] KindChipIds = BuildKindChipIds();

    private static string KindChipId(int index) => KindChipIds[index];

    private static string[] BuildKindChipIds()
    {
        var ids = new string[UnlockKinds.All.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            ids[i] = "##kind" + i.ToString(CultureInfo.InvariantCulture);
        }

        return ids;
    }

    /// <summary>Whether a card is unfolded: the first expansion shown opens by default, the rest start folded.</summary>
    private bool IsOpen(PlanExpansion block) =>
        open.TryGetValue(block.Expansion, out var isOpen) ? isOpen : view.Expansions.Count > 0 && view.Expansions[0].Expansion == block.Expansion;

    private string ZoneLabel(PlanZone zone)
    {
        var name = ZoneName(zone);
        return name.Length > 0 ? name : Strings.PlanUnknownZone;
    }

    /// <summary>The zone's place name from its giver's map, memoized; empty when unknown.</summary>
    private string ZoneName(PlanZone zone)
    {
        if (zone.MapId == 0)
        {
            return string.Empty;
        }

        if (!zoneNames.TryGetValue(zone.MapId, out var name))
        {
            name = links.Map(zone.MapId)?.PlaceName ?? string.Empty;
            zoneNames[zone.MapId] = name;
        }

        return name;
    }

    /// <summary>The plan view as Copy for Discord copies it; built only on a click.</summary>
    private string PlanDiscordText(UnlockPlan shown, bool addLinks)
    {
        var spoilers = session.Spoilers;
        return PlanChecklist.WriteDiscord(shown, ZoneName, addLinks ? e => spoilers.IsMasked(e.Quest) ? null : links.PreferredLink(e.Quest) : null);
    }

    private void CopyChecklist()
    {
        ImGui.SetClipboardText(PlanChecklist.Write(view, ZoneName));
        copied = string.Format(CultureInfo.CurrentCulture, Strings.PlanCopiedFormat, view.Count);
        copiedAt = ImGui.GetTime();
    }

    /// <summary>Sprout sync on becoming visible, then the filtered view, the chip counts and the strings when an input moved.</summary>
    private void Refresh(UiState ui)
    {
        var frame = ImGui.GetFrameCount();
        if (frame != lastDrawFrame)
        {
            if (frame != lastDrawFrame + 1 && ui.Filters.Preset == Preset.Sprout)
            {
                sprout = true;
            }

            lastDrawFrame = frame;
        }

        if (session.ViewedSnapshot is null)
        {
            readyOnly = false;
        }

        var plan = source.Plan;
        var maxExpansion = sprout ? source.Reach : byte.MaxValue;

        // The free-trial view (1.9.0): the expansions after the trial's fold into "Beyond your trial" below the list.
        var trial = settings.FreeTrialView;
        if (trial)
        {
            maxExpansion = Math.Min(maxExpansion, FreeTrial.LastExpansion);
        }

        var key = (source.Revision, kinds, readyOnly, (int)maxExpansion);
        if (key == viewKey && trial == viewTrial)
        {
            return;
        }

        viewKey = key;
        viewTrial = trial;
        var reachFilter = new PlanFilter(UnlockKinds.AllMask, readyOnly, sprout || trial ? maxExpansion : null);
        var reached = plan.Filter(reachFilter);
        view = reached.Filter(reachFilter with { Kinds = kinds });

        Array.Clear(kindCounts);
        foreach (var entry in reached.Entries)
        {
            var mask = entry.KindMask;
            for (var i = 0; i < kindCounts.Length; i++)
            {
                if ((mask & UnlockKinds.Bit(UnlockKinds.All[i])) != 0)
                {
                    kindCounts[i]++;
                }
            }
        }

        for (var i = 0; i < kindLabels.Length; i++)
        {
            kindLabels[i] = string.Format(CultureInfo.CurrentCulture, Strings.PlanKindChipFormat, Strings.PlanKindName(UnlockKinds.All[i]), kindCounts[i]);
        }

        hiddenExpansions = 0;
        var beyondTrialQuests = 0;
        if (sprout || trial)
        {
            foreach (var block in plan.Expansions)
            {
                if (trial && block.Expansion > FreeTrial.LastExpansion)
                {
                    beyondTrialQuests += block.Count;
                }
                else if (block.Expansion > maxExpansion)
                {
                    hiddenExpansions++;
                }
            }
        }

        hiddenText = hiddenExpansions > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.PlanSproutHiddenFormat, hiddenExpansions) : string.Empty;
        beyondTrialText = beyondTrialQuests > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.PlanBeyondTrialFormat, beyondTrialQuests) : string.Empty;
        summary = session.ViewedSnapshot is null
            ? Strings.PlanSummaryBrowse
            : string.Format(CultureInfo.CurrentCulture, Strings.PlanSummaryFormat, plan.Count, plan.ReadyCount);
        showing = string.Format(CultureInfo.CurrentCulture, Strings.PlanShowingFormat, view.Count, plan.Count);

        cardText.Clear();
        foreach (var block in view.Expansions)
        {
            cardText[block.Expansion] = (
                string.Format(CultureInfo.CurrentCulture, Strings.PlanCardFormat, block.Name, block.Count),
                string.Format(CultureInfo.CurrentCulture, Strings.PlanCardReadyFormat, block.ReadyCount),
                string.Format(CultureInfo.CurrentCulture, Strings.PlanExpansionCountFormat, block.Count, block.ReadyCount),
                "##exp" + block.Expansion.ToString(CultureInfo.InvariantCulture));
        }
    }
}
