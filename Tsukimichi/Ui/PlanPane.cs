using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
/// per kind with its count and, at Full and Quiet, the kind's game icon; Ready only, Sprout mode) and the expansion
/// list, each expansion led by its ring; <see cref="DrawMain"/> the "Copy as
/// checklist" button and one card per expansion, folded but for the one opened, with the zone groups and a row per
/// quest: state moon, name (click shows it in the detail pane), kind pills, the status line, Flag, Reveal, Teleport
/// and Walk (Go to giver in the row's right-click and "…" menus). Each
/// card can pin its expansion's block to the todo overlay, and send its quests to Questionable's priority list (the
/// paper-plane button, feature plan v5 1.6.0, <see cref="QuestionableActions"/>).
/// <para>
/// 1.21.0 (feature plan v7, spec-1.21): a switch at the top, Clear my blues · Your story (N9, PlanPane.Story.cs); Sort,
/// Story order · Do first (P4, PlanPane.DoFirst.cs: one card per tier); the tier word as one more chip on each Story
/// order row; and Set aside (PlanPane.SetAside.cs): a quest set aside, or brought back, keeps its row in place as one
/// quiet line with Undo until the list is rebuilt (a tab or filter change), and a whole group asks first.
/// </para>
/// <para>
/// Sprout mode is the plan's own switch, turned on whenever the tab opens while the Journal's Sprout mode quick view
/// is on, so revealing a quest in the Journal (which clears quick views) does not widen the plan. The filtered plan,
/// the chip counts and the card strings are rebuilt only when the plan's revision or a filter changes; rows outside
/// the scrolled view draw a spacer only.
/// </para>
/// </summary>
public sealed partial class PlanPane
{
    private static readonly string FoldedGlyph = Chrome.Icon(FontAwesomeIcon.CaretRight);
    private static readonly string OpenGlyph = Chrome.Icon(FontAwesomeIcon.CaretDown);

    /// <summary>How long "Copied N quests" stays beside the button.</summary>
    private const double CopiedSeconds = 2.5;

    private readonly SessionState session;
    private readonly PlanSource source;
    private readonly GameLinks links;
    private readonly DiscordCopy discordCopy = new();
    private readonly TextFade copiedFade = new();
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
    private (int Revision, ushort Kinds, bool Ready, int MaxExpansion, bool Aside, int Keep) viewKey = (-1, 0, false, -1, false, -1);
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

    /// <summary>The game's textures, for the kind chips' icons and the expansions' rings (UI-5d); null: text only.</summary>
    public Dalamud.Plugin.Services.ITextureProvider? Textures { get; init; }

    /// <summary>The Duty Finder tiles the kind chips wear (UI-5d), once read off the frame; null or unread: the slot stays empty.</summary>
    public Func<IPaneIconSheets?>? IconSheets { get; init; }

    /// <summary>The C7 clear badges (1.19.0): a row whose quest unlocks a duty wears its badges after the kind pills; null wears none.</summary>
    public ClearBadgeSource? Badges { get; init; }

    /// <summary>Set aside, Not for me and Bring back (1.21.0, P4) for the character on view; null offers none.</summary>
    public SetAsideActions? SetAside { get; init; }

    /// <summary>The gap between an icon and the text after it, logical px.</summary>
    private const float IconGapLogical = 5f;

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
        if (pageView == StoryView)
        {
            DrawStoryLeft(ui);
            return;
        }

        using (Typography.Display())
        {
            ImGui.TextUnformatted(Strings.PlanTitle);
        }

        DrawSummary();

        // Sort (1.21.0, P4): Story order · Do first, above the filters.
        ImGui.Spacing();
        SectionHeading.Draw(Strings.BluesSort);
        if (Chrome.Segmented("##sort", ref sort, SortLabels.Value, Chrome.SegmentedWidth(SortLabels.Value, ImGui.GetContentRegionAvail().X), SortTooltips.Value))
        {
            ClearKeep();
        }

        ImGui.Spacing();
        SectionHeading.Draw(Strings.PlanShow);
        var first = true;
        if (FlowChip("##setAside", Strings.BluesSetAsideChip, showSetAside, Strings.BluesSetAsideChipTooltip, ref first, enabled: session.ViewedSnapshot is not null))
        {
            showSetAside = !showSetAside;
            ClearKeep();
        }

        if (FlowChip("##ready", Strings.PlanReadyOnly, readyOnly, Strings.PlanReadyOnlyTooltip, ref first, enabled: session.ViewedSnapshot is not null))
        {
            readyOnly = !readyOnly;
            ClearKeep();
        }

        if (FlowChip("##sprout", Strings.PlanSprout, sprout, Strings.PlanSproutTooltip, ref first))
        {
            sprout = !sprout;
            ClearKeep();
        }

        ImGui.Spacing();
        SectionHeading.Draw(Strings.PlanKinds);
        first = true;
        for (var i = 0; i < UnlockKinds.All.Length; i++)
        {
            var bit = UnlockKinds.Bit(UnlockKinds.All[i]);
            var on = kinds != UnlockKinds.AllMask && (kinds & bit) != 0;
            var icon = KindChipIcon(UnlockKinds.All[i], out var iconSlot);
            if (FlowChip(KindChipId(i), kindLabels[i], on, Strings.PlanKindChipTooltip, ref first, icon: icon, iconSlot: iconSlot))
            {
                // From "all kinds" the first click narrows to that kind; clearing the last kind shows all again.
                ClearKeep();
                kinds = kinds == UnlockKinds.AllMask ? bit : (ushort)(kinds ^ bit);
                if (kinds == 0)
                {
                    kinds = UnlockKinds.AllMask;
                }
            }
        }

        // Always there (1.12.0, U4), held while no kind narrows the list: a chip that came and went with the choice
        // could open or close a row of chips and move the expansions below.
        if (FlowChip("##allKinds", Strings.PlanAllKinds, kinds == UnlockKinds.AllMask, Strings.PlanAllKindsTooltip, ref first))
        {
            ClearKeep();
            kinds = UnlockKinds.AllMask;
        }

        Refresh(ui);
        ImGui.Spacing();

        // Moon Road headings (R3 #9): the heading's own brass rule replaces the hairline that separated the list under Plain.
        if (!Theme.Sectioned)
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

            // The expansion's ring leads the name (UI-5d); the name's room is what the ring leaves.
            var ring = RingWidth(block.Expansion);
            var fit = RowFit.Fit(room - ring, ImGui.CalcTextSize(block.Name).X, UiMetrics.Px(LayoutBudgets.RowNameMinLogical), countPart, countShown);
            if (ImGui.Selectable(text.Label, isOpen, ImGuiSelectableFlags.None, new Vector2(MathF.Max(1f, room), 0f)))
            {
                // The expansions are Story order's cards: a click from Do first goes back to it.
                open[block.Expansion] = true;
                scrollTo = block.Expansion;
                sort = SortStory;
            }

            var hovered = ImGui.IsItemHovered();
            var dl = ImGui.GetWindowDrawList();
            DrawRing(dl, block.Expansion, rowMin);
            var cut = Chrome.EllipsisTextAt(dl, rowMin + new Vector2(ring, 0f), fit.NameRoom, block.Name, ImGui.GetColorU32(ImGuiCol.Text));
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
        DrawViewSwitch();
        if (pageView == StoryView)
        {
            DrawStoryMain(ui);
            return;
        }

        // The plan's tags are built off the frame once the catalog lands (feature plan v6 A11).
        if (session.Bundle is null || !source.IsReady)
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
            Chrome.SameLineOrWrap(DiscordCopy.ButtonWidth());
            discordCopy.Draw("plan", view, (Pane: this, View: view), static (s, addLinks) => s.Pane.PlanDiscordText(s.View, addLinks), note: false);
        }

        // "Flag next stop" (1.6.0, C3 C): the first quest the list shows that can be started now.
        var next = NextStop();
        Chrome.SameLineOrWrap(TravelControls.ToolbarButtonWidth(ActionIcons.FlagIcon, Strings.RouteFlagNextStop));
        if (TravelControls.ToolbarButton("##flagNextStop", ActionIcons.FlagIcon, Strings.RouteFlagNextStop, next is not null) && next is not null)
        {
            links.FlagMap(next);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(next is not null ? Strings.PlanFlagNextStopTooltip : Strings.PlanFlagNextStopUnavailable);
        }

        // One slot for "Showing 12 of 40" and either copy's "Copied" (1.12.0, U4), measured for the widest of them so
        // the toolbar wraps the same way whichever shows; the confirmation fades in where the count was.
        var justCopied = ImGui.GetTime() - copiedAt < CopiedSeconds;
        var confirmation = justCopied ? copied : discordCopy.JustCopied ? Strings.LinksDiscordCopied : null;
        var slot = MathF.Max(ImGui.CalcTextSize(showing).X, MathF.Max(ImGui.CalcTextSize(copied).X, ImGui.CalcTextSize(Strings.LinksDiscordCopied).X));
        Chrome.SameLineOrWrap(slot);
        var alpha = copiedFade.Alpha(confirmation);
        if (confirmation is not null)
        {
            var ink = Theme.Surface.Text;
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.WithAlpha(ink, ink.W * alpha)))
            {
                ImGui.TextUnformatted(confirmation);
            }
        }
        else
        {
            ImGui.TextDisabled(showing);
        }

        if (view.IsEmpty)
        {
            // The shared empty state (1.7.0, onboarding proposal 9): a full moon when nothing is left, else a reset.
            ImGui.Spacing();
            if (showSetAside)
            {
                EmptyState.DrawWithAction(Strings.BluesSetAsideEmpty, Strings.BluesSetAsideEmptyBody, null, moon: QuestState.Completed);
            }
            else if (source.Plan.IsEmpty && session.ViewedSnapshot is not null)
            {
                EmptyState.DrawWithAction(Strings.PlanEmptyAllDoneHeading, Strings.PlanEmptyAllDone, null, moon: QuestState.Completed);
            }
            else if (EmptyState.DrawWithAction(Strings.PlanEmptyFiltered, Strings.PlanEmptyFilteredBody, Strings.ResetFilters, moon: QuestState.Blocked) == EmptyState.ActionClicked)
            {
                readyOnly = false;
                sprout = false;
                kinds = UnlockKinds.AllMask;
                ClearKeep();
            }

            return;
        }

        using var list = ImRaii.Child("##planCards", Vector2.Zero);
        if (!list)
        {
            return;
        }

        ui.RecordWindow(UiRects.PlanCards);
        DrawGroupConfirm();
        if (sort == SortDoFirst)
        {
            DrawDoFirst(ui);
            return;
        }

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
            if (entry.IsReady && !entry.IsSetAside && links.CanFlagMap(entry.Quest))
            {
                nextStop = entry.Quest;
                break;
            }
        }

        return nextStop;
    }

    private (int Revision, ushort Kinds, bool Ready, int MaxExpansion, bool Aside, int Keep) nextStopKey = (-2, 0, false, -1, false, -1);
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
        var pinWidth = TravelControls.ToolbarButtonWidth(ActionGlyphs.Pin, pinLabel);
        var routeWidth = TravelControls.ToolbarButtonWidth(ActionGlyphs.Route, Strings.PlanRouteBlues);
        var headerStart = ImGui.GetCursorPos();
        var questionableWidth = AutomationGate.Questionable(Questionable) is null ? 0f : UiMetrics.MinTarget + UiMetrics.Px(4f);
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

        // The expansion's ring after the caret, at the title's height (UI-5d, as the Flight tab and the Journal tree wear it).
        var x = min.X + UiMetrics.Px(16f);
        x += DrawRing(dl, block.Expansion, new Vector2(x, textY));

        // The title ends in an ellipsis short of Pin (UI audit §3); the Ready count after it gives way first.
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

        if (AutomationGate.Questionable(Questionable) is { } questionable)
        {
            // Send to Questionable: the expansion's quests as the card lists them (filters applied), in story order.
            ImGui.SetCursorPos(new Vector2(right - pinWidth - routeWidth - UiMetrics.Px(6f) - questionableWidth, headerStart.Y + ((ImGui.GetFrameHeight() - UiMetrics.MinTarget) * 0.5f)));
            questionable.DrawIconButton(MainWindow.QuestionableHost, "##questionable", block, static b => RowIdsOf(b), Strings.QuestionableSendExpansionTooltip);
        }

        // "Route": every quest the card lists (the filters apply) in one route through their prerequisites (1.6.0).
        ImGui.SetCursorPos(new Vector2(right - pinWidth - routeWidth - UiMetrics.Px(6f), headerStart.Y));
        if (TravelControls.ToolbarButton("##routeBlues", ActionGlyphs.Route, Strings.PlanRouteBlues))
        {
            ui.OpenRoute(Core.Route.RouteTarget.ForBlues(block));
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.PlanRouteBluesTooltip);
        }

        ImGui.SetCursorPos(new Vector2(right - pinWidth, headerStart.Y));
        if (TravelControls.ToolbarButton("##pinBlues", ActionGlyphs.Pin, pinLabel))
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
                DrawZoneLabel(zone);

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
        var flagReveal = TravelControls.FlagWidth(Strings.PlanFlag) + TravelControls.RowButtonWidth(ActionGlyphs.Reveal, Strings.PlanReveal) + style.ItemSpacing.X;
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

        // Set aside (P4): a quest just set aside (or, in the Set aside view, every row) is one quiet line in the
        // row's own height, so nothing below it moves.
        if (showSetAside || entry.IsSetAside)
        {
            DrawQuietRow(ui, entry, start, width, height, firstLine);
            return;
        }

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
        // The clear badges (1.19.0, C7) of the duty the quest unlocks follow the pills, as one last part: a narrow row
        // drops them before any kind pill.
        Span<UnlockKind> kinds = stackalloc UnlockKind[MaxKinds];
        Span<float> parts = stackalloc float[MaxKinds + 2];
        Span<bool> shown = stackalloc bool[MaxKinds + 2];
        var kindCount = Kinds(entry, tier == PlanRowTier.OneLine || PaneFit.PlanAllKinds(logical) ? MaxKinds : 1, kinds);
        var badges = Badges?.ForQuest(quest, Core.Companions.DutyBadgeSurface.MyBlues);
        var partCount = kindCount;
        using (Typography.Caption())
        {
            for (var i = 0; i < kindCount; i++)
            {
                parts[i] = PillSize(kinds[i], Strings.PlanKindName(kinds[i])).X + (i == 0 ? gap : UiMetrics.Px(4f));
            }
        }

        // The tier word (1.21.0, P4) is one more chip after the kinds, before the clear badges.
        var tierLabel = UnlockTiers.Name(entry.Tier);
        float tierPart;
        using (Typography.Caption())
        {
            tierPart = TierChipSize(tierLabel).X + (kindCount == 0 ? gap : UiMetrics.Px(4f));
        }

        parts[partCount++] = tierPart;
        if (badges is { Length: > 0 })
        {
            parts[partCount++] = DutyBadges.RunWidth(badges) + DutyBadges.RunGap;
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
            var fit = RowFit.Fit(actionsX - gap - nameX, nameWidth, UiMetrics.Px(LayoutBudgets.RowNameMinLogical), parts[..partCount], shown);
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
                SetAsideMenuItems(entry);
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

        // The tier chip, then the badges, only after every kind pill and while they fit the pills' room; each explains
        // itself on hover.
        var kindsWidth = 0f;
        for (var i = 0; i < kindCount; i++)
        {
            kindsWidth += parts[i];
        }

        if (pillsEnd >= pillX + kindsWidth - 0.5f && pillsEnd + tierPart <= pillEnd + 0.5f)
        {
            pillsEnd = DrawTierChip(dl, tierLabel, pillsEnd + tierPart, start.Y, firstLine);
            if (badges is { Length: > 0 })
            {
                DutyBadges.DrawRun(badges, pillsEnd + DutyBadges.RunGap, start.Y, firstLine, pillEnd, Textures);
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

    /// <summary>
    /// A kind pill's size: its label, led by the kind's icon at the caption's height where the chips wear one (UI-5d);
    /// measure in the caption role.
    /// </summary>
    private Vector2 PillSize(UnlockKind kind, string label)
    {
        var size = ImGui.CalcTextSize(label) + new Vector2(UiMetrics.Px(7f) * 2f, UiMetrics.Px(2f) * 2f);
        KindChipIcon(kind, out var slot);
        if (slot)
        {
            size.X += PillIconPart() - UiMetrics.Px(7f - PillIconPadLogical);
        }

        return size;
    }

    /// <summary>The left pad of a pill led by an icon, logical px (the label's side keeps 7).</summary>
    private const float PillIconPadLogical = 4f;

    /// <summary>The icon and its gap in a kind pill, at the caption's line height.</summary>
    private static float PillIconPart() => MathF.Round(ImGui.GetTextLineHeight()) + MathF.Round(UiMetrics.Px(3f));

    /// <summary>
    /// Paints the kind pills from <paramref name="x"/>, each after its gap (<paramref name="parts"/> holds gap and
    /// pill), while they end by <paramref name="end"/>. Returns where the last pill drawn ends.
    /// </summary>
    private float DrawPills(ImDrawListPtr dl, ReadOnlySpan<UnlockKind> kinds, ReadOnlySpan<float> parts, float x, float end, float top, float height)
    {
        using var caption = Typography.Caption();
        var tone = Theme.Surface.TextSecondary;
        var line = ImGui.GetTextLineHeight();
        var iconSize = MathF.Round(line);
        for (var i = 0; i < kinds.Length && x + parts[i] <= end + 0.5f; i++)
        {
            var label = Strings.PlanKindName(kinds[i]);
            var pill = PillSize(kinds[i], label);
            var min = new Vector2(x + parts[i] - pill.X, top + ((height - pill.Y) * 0.5f));
            var icon = KindChipIcon(kinds[i], out var slot);
            if (!slot)
            {
                Chrome.PillAt(dl, min, pill, label, Theme.WithAlpha(tone, 0.12f), Theme.WithAlpha(tone, 0.45f), Theme.U32(tone));
                x += parts[i];
                continue;
            }

            // The pill, then the icon from the narrower left pad and the label after it, both centred on the pill.
            Chrome.PillAt(dl, min, pill, string.Empty, Theme.WithAlpha(tone, 0.12f), Theme.WithAlpha(tone, 0.45f), Theme.U32(tone));
            var iconX = min.X + UiMetrics.Px(PillIconPadLogical);
            if (icon != 0 && Textures is { } textures)
            {
                var iconMin = new Vector2(iconX, min.Y + MathF.Round((pill.Y - iconSize) * 0.5f));
                Orbit.DrawIcon(dl, textures, NodeIcon.Game(icon), iconMin, iconMin + new Vector2(iconSize, iconSize));
            }

            dl.AddText(new Vector2(iconX + PillIconPart(), min.Y + ((pill.Y - line) * 0.5f)), Theme.U32(tone), label);
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
        if (TravelControls.FlagButton(Strings.PlanFlag, links.CanFlagMap(quest)))
        {
            links.FlagMap(quest);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.PlanFlagTooltip);
        }

        ImGui.SameLine();
        if (TravelControls.RowButton("##reveal", ActionGlyphs.Reveal, Strings.PlanReveal))
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
        SetAsideMenuItems(entry);
    }

    private static string UnlocksTooltip(PlanEntry entry)
    {
        var text = PlanChecklist.UnlocksText(entry);
        return text.Length > 0 ? text : Strings.PlanKindName(entry.PrimaryKind);
    }

    /// <summary>The room an expansion's ring and its gap take before a name at the text's height; 0 for an expansion without one.</summary>
    private float RingWidth(byte expansion) =>
        Textures is null || PaneIcons.Expansion(expansion) == 0 ? 0f : MathF.Round(ImGui.GetTextLineHeight()) + MathF.Round(UiMetrics.Px(IconGapLogical));

    /// <summary>
    /// The expansion's ring (061875 A Realm Reborn …) at <paramref name="min"/>, a text line across, unrounded (a ring is
    /// a circle); returns the room it took with its gap (<see cref="RingWidth"/>).
    /// </summary>
    private float DrawRing(ImDrawListPtr dl, byte expansion, Vector2 min)
    {
        var width = RingWidth(expansion);
        if (width > 0f && Textures is { } textures)
        {
            var size = MathF.Round(ImGui.GetTextLineHeight());
            Orbit.DrawIcon(dl, textures, NodeIcon.Game(PaneIcons.Expansion(expansion)), min, min + new Vector2(size, size));
        }

        return width;
    }

    /// <summary>
    /// The icon of a kind chip (UI-5d): the kind's Duty Finder tile, emblem or marker; 0 for Other, without textures and
    /// at Plain, whose chips stay text only as its action buttons do. <paramref name="reserved"/> says whether the chip
    /// keeps the icon's slot, also while the sheets are still read, so the chip never widens under the pointer.
    /// </summary>
    private uint KindChipIcon(UnlockKind kind, out bool reserved)
    {
        reserved = Textures is not null && Theme.Flair != Flair.Plain && kind != UnlockKind.Other;
        return reserved && IconSheets?.Invoke() is { } sheets ? PaneIcons.UnlockKind(kind, sheets) : 0u;
    }

    /// <summary>
    /// A toggle chip flowing onto the next line when the row is full: a pill painted silver-tinted when on, raised when
    /// off, with the focus ring; returns true on the click that flips it. With <paramref name="iconSlot"/> the label is
    /// led by <paramref name="icon"/> at the text's height (the slot kept empty while the icon is 0).
    /// </summary>
    private bool FlowChip(string id, string label, bool on, string tooltip, ref bool first, bool enabled = true, uint icon = 0, bool iconSlot = false)
    {
        var padX = UiMetrics.Px(9f);
        var height = MathF.Max(ImGui.GetTextLineHeight() + UiMetrics.Px(6f), UiMetrics.Px(22f));
        var iconSize = MathF.Round(ImGui.GetTextLineHeight());
        var iconPart = iconSlot ? iconSize + MathF.Round(UiMetrics.Px(IconGapLogical)) : 0f;
        var size = new Vector2(ImGui.CalcTextSize(label).X + iconPart + padX * 2f, height);
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

        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetItemRectMin();
        if (iconSlot)
        {
            // The pill without its centred label, then the icon and the label from the left pad, both centred on the pill.
            Chrome.PillAt(dl, min, size, string.Empty, fill, border, ink);
            if (icon != 0 && Textures is { } textures)
            {
                var iconMin = new Vector2(min.X + padX, min.Y + MathF.Round((height - iconSize) * 0.5f));
                Orbit.DrawIcon(dl, textures, NodeIcon.Game(icon), iconMin, iconMin + new Vector2(iconSize, iconSize), enabled ? 1f : 0.5f);
            }

            dl.AddText(new Vector2(min.X + padX + iconPart, min.Y + ((height - ImGui.GetTextLineHeight()) * 0.5f)), ink, label);
        }
        else
        {
            Chrome.PillAt(dl, min, size, label, fill, border, ink);
        }

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

    /// <summary>
    /// The zone's place name from its giver's map, memoized; empty when unknown. A zone the story has not reached reads
    /// as its placeholder (1.20.0 N6), on screen and in both copies.
    /// </summary>
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

        return session.Spoilers.Name(Core.Query.SpoilerKind.Area, name);
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

            // The tab came back into view: the list is rebuilt, so set-aside rows kept in place go (P4).
            if (frame != lastDrawFrame + 1)
            {
                ClearKeep();
            }

            lastDrawFrame = frame;
        }

        if (session.ViewedSnapshot is null)
        {
            readyOnly = false;
            showSetAside = false;
        }

        var plan = source.Plan;
        var maxExpansion = sprout ? source.Reach : byte.MaxValue;

        // The free-trial view (1.9.0): the expansions after the trial's fold into "Beyond your trial" below the list.
        var trial = settings.FreeTrialView;
        if (trial)
        {
            maxExpansion = Math.Min(maxExpansion, FreeTrial.LastExpansion);
        }

        var key = (source.Revision, kinds, readyOnly, (int)maxExpansion, showSetAside, keepVersion);
        if (key == viewKey && trial == viewTrial)
        {
            return;
        }

        viewKey = key;
        viewTrial = trial;
        tierLines.Clear();

        // The Set aside filter (P4) lists the quests set aside instead; either view keeps the rows of the other side
        // that were moved since it was built in place (keepInPlace).
        var basePlan = showSetAside ? plan.SetAside : plan;
        var reachFilter = new PlanFilter(UnlockKinds.AllMask, readyOnly, sprout || trial ? maxExpansion : null);
        var reached = basePlan.Filter(reachFilter);
        view = basePlan.Filter(reachFilter with { Kinds = kinds }, keepInPlace);

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
            : showSetAside
                ? string.Format(CultureInfo.CurrentCulture, Strings.BluesSetAsideSummaryFormat, plan.SetAsideCount)
                : string.Format(CultureInfo.CurrentCulture, Strings.BluesSummaryFormat, plan.Count, plan.ReadyCount);
        setAsideLink = !showSetAside && plan.SetAsideCount > 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.BluesSetAsideLinkFormat, plan.SetAsideCount)
            : string.Empty;
        if (setAsideLink.Length > 0)
        {
            summary += Core.Evaluation.BlockerText.Separator.TrimEnd();
        }

        showing = showSetAside
            ? string.Format(CultureInfo.CurrentCulture, Strings.PlanShowingFormat, view.Entries.Count(static e => e.IsSetAside), plan.SetAsideCount)
            : string.Format(CultureInfo.CurrentCulture, Strings.PlanShowingFormat, view.Count, plan.Count);

        cardText.Clear();
        foreach (var block in view.Expansions)
        {
            var count = showSetAside ? block.SetAsideCount : block.Count;
            var ready = showSetAside ? 0 : block.ReadyCount;
            cardText[block.Expansion] = (
                string.Format(CultureInfo.CurrentCulture, Strings.PlanCardFormat, block.Name, count),
                string.Format(CultureInfo.CurrentCulture, Strings.PlanCardReadyFormat, ready),
                string.Format(CultureInfo.CurrentCulture, Strings.PlanExpansionCountFormat, count, ready),
                "##exp" + block.Expansion.ToString(CultureInfo.InvariantCulture));
        }
    }
}
