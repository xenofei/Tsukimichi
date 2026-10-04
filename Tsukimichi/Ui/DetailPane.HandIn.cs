using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.HandIn;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Sources;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail pane's Hand in section (feature plan v5 side track, research C4 #2, #4 and C7 A, B): one row per item the
/// quest asks for (<see cref="QuestRecord.HandInItems"/>) with its icon, its name, the amount and quality when the game
/// data says, and how many the logged-in character holds (<see cref="HandInStock"/>: bags, armoury and saddlebag from
/// the game, retainers through Allagan Tools; an item asked for high quality counts only the game's HQ, Allagan Tools'
/// numbers are labelled "NQ+HQ" and never make it enough). Each row ends in two hand-off buttons (decision 1): Craft with Artisan
/// and, for an item a node or fishing hole yields, Gather with GatherBuddy; a button whose plugin is missing stays,
/// disabled, naming it. Under each item's name, its "Where" lines (1.19, N5; <see cref="WhereToGet"/>) say where the game
/// data gets it: the cheapest vendor with its place and price, the crafters and levels, the lowest node or fishing hole,
/// a quartermaster, an exchange, the duties it drops in, or "Market board only"; a placed line flags its place on click.
/// Under the rows, "Copy missing items" puts a Teamcraft import link or a "3x Item" list on the
/// clipboard, for this quest or for every pinned quest. Display only: the quest's state never reads any of it.
/// <para>
/// Spoiler-aware like the quest's name: while the shield masks the name, the section says so and shows no item (the
/// items tell the story). Counts are for the logged-in character only; another character on view gets a line saying
/// so.
/// </para>
/// </summary>
public sealed partial class DetailPane
{
    private const string HandInCopyMenuId = "##handInCopy";

    private const string WhereMenuId = "##whereMenu";

    /// <summary>At most this many other shops or spots a "Where" line's tooltip lists before "+N more".</summary>
    private const int WhereOthersShown = 6;

    /// <summary>The most lines one "Where" line wraps to before it ends in an ellipsis (its tooltip has it whole).</summary>
    private const int WhereMaxLines = 3;

    /// <summary>The least room a hand-in row keeps for the item's name before its pills drop their labels, logical pixels.</summary>
    private const float HandInNameRoomLogical = 120f;

    private static readonly string HandInIcon = FontAwesomeIcon.HandHolding.ToIconString();
    private static readonly string CraftIcon = FontAwesomeIcon.Hammer.ToIconString();
    private static readonly string GatherIcon = FontAwesomeIcon.Leaf.ToIconString();
    private static readonly string FishIcon = FontAwesomeIcon.Fish.ToIconString();
    private static readonly string HandInCopyIcon = FontAwesomeIcon.ClipboardList.ToIconString();

    // Per-row strings, rebuilt with the model (the counts' line only when its numbers change).
    private readonly List<HandInRow> handInRows = [];
    private uint handInRowId = uint.MaxValue;
    private int handInVersion = -1;
    private bool handInMasked;
    private bool handInAmountUnknown;
    private bool handInHqMixed;
    private string handInCaption = string.Empty;
    private bool handInCaptionDirty;

    /// <summary>Live item counts; null until the plugin attaches it, which leaves the counts line out.</summary>
    public HandInStock? Stock { get; set; }

    /// <summary>Artisan's IPC; null until the plugin attaches it (the Craft button then names Artisan as missing).</summary>
    public ArtisanIpc? Artisan { get; set; }

    /// <summary>GatherBuddy's commands; null until the plugin attaches it (the Gather button then names GatherBuddy as missing).</summary>
    public GatherBuddyCommands? GatherBuddy { get; set; }

    /// <summary>One "Where" line as the row draws it (1.19, N5): the line, its text with "+N more", and its tooltip's body.</summary>
    private sealed class WhereRow(WhereLine line, string text, string detail)
    {
        public WhereLine Line { get; } = line;

        public string Text { get; } = text;

        /// <summary>The other shops or spots of its kind, then what a click does; empty when neither.</summary>
        public string Detail { get; } = detail;
    }

    private sealed class HandInRow(HandInItem item, string name, string detail)
    {
        public HandInItem Item { get; } = item;

        /// <summary>The item sources <see cref="Where"/> was read from; it is read again when they land.</summary>
        public ItemSourceIndex? WhereSources { get; set; }

        /// <summary>Where to get the item (1.19, N5), one line per kind of source; empty while the sources are read.</summary>
        public List<WhereRow> Where { get; } = [];

        /// <summary>The item's name.</summary>
        public string Name { get; } = name;

        /// <summary>"×3 · HQ · Miner" after the name; empty when the data says none of them.</summary>
        public string Detail { get; } = detail;

        /// <summary>The name and the detail as the row prints them.</summary>
        public string Line { get; } = detail.Length > 0 ? name + "  " + detail : name;

        /// <summary>The last count read for the row; null before the first read.</summary>
        public HandInCount? Count { get; set; }

        public string CountText { get; set; } = string.Empty;
        public bool Enough { get; set; }
    }

    /// <summary>Rebuilds the rows when the quest or the session version changed; otherwise free.</summary>
    private void RefreshHandIn(SessionState session, QuestRecord quest)
    {
        if (handInRowId == quest.RowId && handInVersion == session.Version)
        {
            return;
        }

        handInRowId = quest.RowId;
        handInVersion = session.Version;
        handInRows.Clear();
        handInCaption = string.Empty;
        handInCaptionDirty = false;
        handInMasked = session.Spoilers.IsMasked(quest);
        handInAmountUnknown = false;
        foreach (var item in quest.HandInItems)
        {
            handInAmountUnknown |= !item.AmountKnown;
            handInRows.Add(new HandInRow(item, item.Name, RowDetail(item)));
        }
    }

    /// <summary>"×3 · HQ · MIN": the amount, the quality and the jobs it is for, each only when the data says.</summary>
    private string RowDetail(HandInItem item)
    {
        var parts = new List<string>(3);
        if (item.AmountKnown)
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.HandInAmountFormat, item.Amount));
        }

        if (item.IsHq)
        {
            parts.Add(Strings.HandInHq);
        }

        if (item.ClassJobCategories.Length > 0)
        {
            var jobs = string.Join(Strings.HandInJobSeparator, item.ClassJobCategories.Select(links.ClassJobCategoryName).Where(n => n.Length > 0));
            if (jobs.Length > 0)
            {
                parts.Add(jobs);
            }
        }

        return string.Join(Core.Evaluation.BlockerText.Separator, parts);
    }

    /// <summary>The section itself, between Rewards and the Moonlit verdict; nothing for a quest that asks for no item.</summary>
    private void DrawHandInSection(SessionState session, QuestRecord quest)
    {
        if (quest.HandInItems.Count == 0)
        {
            return;
        }

        RefreshHandIn(session, quest);
        Gap();
        if (handInCaptionDirty)
        {
            handInCaptionDirty = false;
            handInCaption = HandInCaption();
        }

        BeginSection("##handIn", Strings.HandInSection, HandInIcon, handInMasked ? string.Empty : handInCaption, Theme.Surface.TextSecondary);
        if (handInMasked)
        {
            TextFlow.Wrapped(Strings.HandInMasked, RoomTo(cardRight), Theme.U32(Theme.Surface.TextDisabled));
        }
        else
        {
            DrawHandInRows(session);
            DrawHandInFooter(session, quest);
        }

        EndSection();
    }

    /// <summary>"2 of 3 in hand" once counts are known for the live character; empty otherwise.</summary>
    private string HandInCaption()
    {
        var known = 0;
        var enough = 0;
        foreach (var row in handInRows)
        {
            if (row.Count is not { Known: true })
            {
                continue;
            }

            known++;
            enough += row.Enough ? 1 : 0;
        }

        return known == 0 ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.HandInCaptionFormat, enough, handInRows.Count);
    }

    private void DrawHandInRows(SessionState session)
    {
        var live = session.IsLive && Stock is not null;
        var iconSize = UiMetrics.Icon(28f);
        var artisanLoaded = Artisan?.Available == true;
        var artisanBusy = artisanLoaded && Artisan!.IsBusy;
        var gatherPlugin = GatherBuddy?.Plugin ?? GatherPlugin.None;
        handInHqMixed = false;

        // The two hand-offs are pills like the action bar's (1.10): labelled while the row keeps room for the item's
        // name, else the icons alone (still pills), their labels in the tooltips. Each shows only while the automation
        // level does (1.18, A10); Craft's slot is sized for its Stop too (A7), so nothing moves when it turns. A craft
        // Tsukimichi handed Artisan keeps its slot while it runs, holding only the Stop, even with Craft above the level.
        var stopOnly = !AutomationGate.Shows(AutomationButtons.Artisan) && Artisan is { HandOffClaimed: true };
        var showCraft = AutomationGate.Shows(AutomationButtons.Artisan) || stopOnly;
        var showGather = AutomationGate.Shows(AutomationButtons.Gather);
        var labelled = HandInPillsLabelled(iconSize, showCraft, showGather);
        var craftWidth = showCraft ? CraftSlotWidth(labelled) : 0f;
        var gatherWidth = showGather ? GatherSlotWidth(labelled) : 0f;
        var buttons = craftWidth + (showCraft && showGather ? ImGui.GetStyle().ItemSpacing.X : 0f) + gatherWidth;
        for (var i = 0; i < handInRows.Count; i++)
        {
            var row = handInRows[i];
            using var id = ImRaii.PushId(i);
            if (live)
            {
                UpdateCount(row, Stock!.For(row.Item.ItemId));
                handInHqMixed |= row.Item.IsHq && row.Count is { HasMixed: true };
            }

            var start = ImGui.GetCursorScreenPos();
            GameIcon.Draw(textures, row.Item.Icon, iconSize);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(row.Name, row.Detail.Length > 0 ? row.Detail : null);
            }

            ImGui.SameLine();
            using (ImRaii.Group())
            {
                var textRoom = MathF.Max(1f, RoomTo(cardRight) - buttons - UiMetrics.Px(8f));
                TextFlow.Wrapped(row.Line, textRoom);
                if (live && row.CountText.Length > 0)
                {
                    using (Theme.PushText(row.Enough ? Theme.Accent : Theme.Surface.TextSecondary))
                    {
                        ImGui.TextUnformatted(row.CountText);
                    }
                }

                var where = WhereOf(row);
                for (var w = 0; w < where.Count; w++)
                {
                    using var whereId = ImRaii.PushId(w);
                    DrawWhereLine(where[w], textRoom);
                }
            }

            // The two hand-offs at the row's right edge, beside the name while they fit.
            if (buttons <= 0f)
            {
                continue;
            }

            SameLineOrWrap(buttons, cardRight);
            ImGui.SetCursorScreenPos(new Vector2(MathF.Max(ImGui.GetCursorScreenPos().X, cardRight - buttons), MathF.Max(ImGui.GetCursorScreenPos().Y, start.Y)));
            if (showCraft)
            {
                DrawCraftButton(session, row, artisanLoaded, artisanBusy, labelled, craftWidth, stopOnly);
            }

            if (showGather)
            {
                if (showCraft)
                {
                    ImGui.SameLine();
                }

                DrawGatherButton(row, gatherPlugin, labelled, gatherWidth);
            }
        }

        if (!live)
        {
            TextFlow.Wrapped(Stock is null ? Strings.HandInCountsUnavailable : Strings.HandInCountsLiveOnly, RoomTo(cardRight), Theme.U32(Theme.Surface.TextDisabled));
        }

        if (live && handInHqMixed)
        {
            TextFlow.Wrapped(Strings.HandInHqMixedNote, RoomTo(cardRight), Theme.U32(Theme.Surface.TextDisabled));
        }

        if (handInAmountUnknown)
        {
            TextFlow.Wrapped(Strings.HandInAmountUnknownNote, RoomTo(cardRight), Theme.U32(Theme.Surface.TextDisabled));
        }
    }

    /// <summary>
    /// The row's "Where" lines (1.19, N5), composed once per item source index (<see cref="WhereToGet.Lines"/>): empty
    /// until the index lands, then one per kind of source, best first.
    /// </summary>
    private List<WhereRow> WhereOf(HandInRow row)
    {
        var sources = links.ItemSources;
        if (ReferenceEquals(row.WhereSources, sources))
        {
            return row.Where;
        }

        row.WhereSources = sources;
        row.Where.Clear();
        if (sources is null)
        {
            return row.Where;
        }

        foreach (var line in WhereToGet.Lines(sources.For(row.Item.ItemId)))
        {
            var text = line.More > 0 ? line.Text + Core.Evaluation.BlockerText.Separator + WhereToGet.MoreText(line.More) : line.Text;
            var detail = new List<string>(WhereOthersShown + 2);
            for (var i = 0; i < line.Others.Count && i < WhereOthersShown; i++)
            {
                detail.Add(line.Others[i]);
            }

            if (line.Others.Count > WhereOthersShown)
            {
                detail.Add(WhereToGet.MoreText(line.Others.Count - WhereOthersShown));
            }

            if (line.Spot is not null)
            {
                detail.Add(Strings.HandInWhereClickHint);
            }

            row.Where.Add(new WhereRow(line, text, string.Join("\n", detail)));
        }

        return row.Where;
    }

    /// <summary>
    /// One "Where" line, in the caption size: a click (or Enter) flags its place on the map, the right-click menu (or
    /// the Menu key) offers Flag, Teleport to the nearest aetheryte while the automation level shows Teleport, and Copy
    /// coordinates; the tooltip lists the other shops or spots of its kind. A line with no place is text alone. The
    /// line's height never changes on hover.
    /// </summary>
    private void DrawWhereLine(WhereRow where, float width)
    {
        using var caption = Typography.Caption();
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var size = new Vector2(width, TextFlow.Height(where.Text, width));
        ImGui.InvisibleButton("##where", size);
        var hovered = ImGui.IsItemHovered();
        var spot = where.Line.Spot;
        var clicked = spot is not null && (ImGui.IsItemClicked(ImGuiMouseButton.Left) || (ImGui.IsItemFocused() && ImGui.IsKeyPressed(ImGuiKey.Enter, false)));
        if (spot is not null)
        {
            Keyboard.OpenMenuOnKey(WhereMenuId);
        }

        var rounding = UiMetrics.Px(4f);
        if (hovered && spot is not null)
        {
            dl.AddRectFilled(start, start + size, Theme.U32(Theme.Surface.Hover), rounding);
        }

        Chrome.FocusRing(rounding);
        TextFlow.DrawClamped(dl, start, where.Text, width, WhereMaxLines, Theme.U32(Theme.Surface.TextSecondary));
        if (hovered || (ImGui.GetIO().NavVisible && ImGui.IsItemFocused()))
        {
            UiMetrics.Tooltip(where.Line.Text, where.Detail);
        }

        if (spot is null)
        {
            return;
        }

        if (clicked && !links.FlagWorldSpot(spot))
        {
            ShowHandInNote(Strings.HandInWhereFlagFailed);
        }

        DrawWhereMenu(spot);
    }

    /// <summary>A "Where" line's menu: Flag on map, Teleport to the aetheryte nearest the place (automation level), Copy coordinates.</summary>
    private void DrawWhereMenu(Core.Sources.WorldSpot spot)
    {
        using var menu = ImRaii.ContextPopupItem(WhereMenuId);
        if (!menu)
        {
            return;
        }

        if (ImGui.MenuItem(Strings.FlagOnMap, string.Empty, false, links.CanFlagWorldSpot(spot)) && !links.FlagWorldSpot(spot))
        {
            ShowHandInNote(Strings.HandInWhereFlagFailed);
        }

        if (links.TeleportShown && links.AetheryteNear(spot) is { } aetheryte)
        {
            var teleport = string.Format(CultureInfo.CurrentCulture, Strings.UnlocksMenuTeleportFormat, aetheryte.Name);
            if (ImGui.MenuItem(teleport, string.Empty, false, links.CanTeleportTo(aetheryte.RowId)))
            {
                links.TeleportTo(aetheryte.RowId, aetheryte.Name);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && links.TeleportToBlocked(aetheryte.RowId) is { } why)
            {
                UiMetrics.Tooltip(why);
            }
        }

        if (ImGui.MenuItem(Strings.UnlocksMenuCopyCoordinates))
        {
            ImGui.SetClipboardText(Core.Sources.SourceText.Spot(spot));
        }
    }

    /// <summary>Recomposes the counts line (and, next frame, the caption) only when its numbers moved.</summary>
    private void UpdateCount(HandInRow row, HandInCount count)
    {
        if (row.Count == count)
        {
            return;
        }

        handInCaptionDirty = true;
        row.Count = count;
        row.Enough = count.IsEnough(row.Item);
        row.CountText = row.Item.IsHq ? HqCountText(count) : (count.Held, count.Retainers) switch
        {
            (null, null) => Strings.HandInCountUnknown,
            ({ } held, null) => string.Format(CultureInfo.CurrentCulture, Strings.HandInHaveFormat, held),
            (null, { } retainers) => string.Format(CultureInfo.CurrentCulture, Strings.HandInRetainersOnlyFormat, retainers),
            ({ } held, { } retainers) => string.Format(CultureInfo.CurrentCulture, Strings.HandInHaveWithRetainersFormat, held, retainers),
        };
    }

    /// <summary>
    /// The counts line of an item asked for high quality: the game's HQ count, and Allagan Tools' numbers (which
    /// cannot tell NQ from HQ) labelled "NQ+HQ".
    /// </summary>
    private static string HqCountText(HandInCount count) => (count.HeldHq, count.Held, count.Retainers) switch
    {
        ({ } hq, _, null) => string.Format(CultureInfo.CurrentCulture, Strings.HandInHaveHqFormat, hq),
        ({ } hq, _, { } retainers) => string.Format(CultureInfo.CurrentCulture, Strings.HandInHaveHqWithRetainersFormat, hq, retainers),
        (null, { } held, null) => string.Format(CultureInfo.CurrentCulture, Strings.HandInHaveMixedFormat, held),
        (null, { } held, { } retainers) => string.Format(CultureInfo.CurrentCulture, Strings.HandInHaveMixedWithRetainersFormat, held, retainers),
        (null, null, { } retainers) => string.Format(CultureInfo.CurrentCulture, Strings.HandInRetainersOnlyMixedFormat, retainers),
        _ => Strings.HandInCountUnknown,
    };

    /// <summary>
    /// Whether the hand-off pills carry their labels: while the item's icon, the labelled pills shown and
    /// <see cref="HandInNameRoomLogical"/> of name fit the card.
    /// </summary>
    private bool HandInPillsLabelled(float iconSize, bool showCraft, bool showGather)
    {
        var labelled = (showCraft ? CraftSlotWidth(true) : 0f)
            + (showCraft && showGather ? ImGui.GetStyle().ItemSpacing.X : 0f)
            + (showGather ? GatherSlotWidth(true) : 0f);
        return iconSize + ImGui.GetStyle().ItemSpacing.X + UiMetrics.Px(HandInNameRoomLogical) + labelled <= RoomTo(cardRight);
    }

    /// <summary>Craft's slot: the wider of Craft and the Stop it turns into while its run is under way (1.18, A7).</summary>
    private static float CraftSlotWidth(bool labelled) => MathF.Max(
        Chrome.ActionPillWidth(CraftIcon, labelled ? Strings.HandInCraftShort : null),
        Chrome.ActionPillWidth(StopIcon, labelled ? Strings.ActionStopShort : null));

    /// <summary>Gather's slot: the wider of Gather and Fish.</summary>
    private static float GatherSlotWidth(bool labelled) => labelled
        ? MathF.Max(Chrome.ActionPillWidth(GatherIcon, Strings.HandInGatherShort), Chrome.ActionPillWidth(FishIcon, Strings.HandInFishShort))
        : Chrome.ActionPillWidth(GatherIcon, null);

    /// <summary>
    /// Craft with Artisan, or, while Artisan crafts the run this row handed it (1.18, A7), a labelled Stop in the same
    /// slot that ends it through Artisan's IPC (<c>SetEnduranceStatus(false)</c>; Artisan finishes the craft in hand).
    /// The pill keeps <paramref name="slotWidth"/> either way, so Gather beside it never moves. With
    /// <paramref name="stopOnly"/> (Craft above the automation level, its run still under way) only the Stop draws.
    /// </summary>
    private void DrawCraftButton(SessionState session, HandInRow row, bool artisanLoaded, bool artisanBusy, bool labelled, float slotWidth, bool stopOnly)
    {
        var start = ImGui.GetCursorScreenPos();
        var stop = Artisan is { HandOffClaimed: true } claimed && HandInActions.CraftShowsStop(row.Item, artisanBusy, claimed.ClaimedRecipeId);
        if (!stop && stopOnly)
        {
            // Craft is above the automation level: the other rows keep the empty slot, so nothing moves.
            ImGui.Dummy(new Vector2(slotWidth, Chrome.ActionPillHeight));
            return;
        }

        if (stop)
        {
            var stopPressed = Chrome.ActionPill("##craft", StopIcon, labelled ? Strings.ActionStopShort : null, PillTone.Danger, true);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(labelled ? Strings.ArtisanStopTooltip : Strings.ActionStopShort, labelled ? null : Strings.ArtisanStopTooltip);
            }

            KeepSlot(start, slotWidth);
            if (stopPressed)
            {
                StopActivity(StopTarget.Artisan);
            }

            return;
        }

        // Enough only from the live character's count: another character on view has no count to go by.
        var state = HandInActions.Craft(row.Item, artisanLoaded, artisanBusy, enough: session.IsLive && row.Enough);
        var recipe = state == HandOffState.Ready
            ? HandInActions.ChooseRecipe(row.Item, session.LiveSnapshot?.CurrentJob ?? 0, session.LiveSnapshot?.JobLevels)
            : null;
        var pressed = Chrome.ActionPill("##craft", CraftIcon, labelled ? Strings.HandInCraftShort : null, PillTone.Normal, state == HandOffState.Ready);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(state switch
            {
                HandOffState.Ready => WithSetupNote(CraftTooltip(session, row, recipe), CompanionPlugin.Artisan),
                HandOffState.Enough => Strings.HandInHaveEnough,
                // The registry's reason when it has one ("Artisan is installed but turned off…"), as every hand-off button says it.
                HandOffState.PluginMissing => CompanionPlugins.DisabledReason(CompanionPlugin.Artisan) ?? Strings.HandInNeedsArtisan,
                HandOffState.Busy => Strings.HandInArtisanBusy,
                _ => Strings.HandInNoRecipe,
            });
        }

        KeepSlot(start, slotWidth);
        if (!pressed)
        {
            return;
        }

        var amount = CraftAmountFor(session, row, recipe);
        var sent = recipe is not null && amount > 0 && Artisan is { } artisan && artisan.Craft(recipe.RecipeId, amount);
        if (sent)
        {
            // The status bar's activity line names the craft while it runs ("Artisan: crafting Maple Lumber").
            artisanCraftingLine = string.Format(CultureInfo.CurrentCulture, Strings.ArtisanCraftingFormat, row.Name);
        }

        ShowHandInNote(sent ? string.Format(CultureInfo.CurrentCulture, Strings.HandInSentToArtisanFormat, row.Name) : Strings.HandInArtisanFailed);
    }

    /// <summary>
    /// Pads a pill drawn at <paramref name="start"/> out to <paramref name="slotWidth"/>, so the item after it on the
    /// line keeps its place whichever form the pill took. Call after the pill's own hover is read.
    /// </summary>
    private static void KeepSlot(Vector2 start, float slotWidth)
    {
        var drawn = ImGui.GetItemRectMax().X - start.X;
        if (drawn >= slotWidth - 0.5f)
        {
            return;
        }

        ImGui.SameLine(0f, 0f);
        ImGui.Dummy(new Vector2(slotWidth - drawn, Chrome.ActionPillHeight));
    }

    /// <summary>An enabled hand-off's tooltip, with the companion setup note under it when a recommended setting is set otherwise.</summary>
    private static string WithSetupNote(string tooltip, CompanionPlugin plugin) =>
        CompanionPlugins.SetupNote(plugin) is { } note ? tooltip + "\n" + note : tooltip;

    /// <summary>"Craft 2 × Maple Lumber with Artisan", naming what one craft makes when it is more than one.</summary>
    private static string CraftTooltip(SessionState session, HandInRow row, HandInRecipe? recipe)
    {
        var crafts = CraftAmountFor(session, row, recipe);
        return recipe is { Yield: > 1 } several
            ? string.Format(CultureInfo.CurrentCulture, Strings.HandInCraftYieldTooltipFormat, crafts, row.Name, several.Yield)
            : string.Format(CultureInfo.CurrentCulture, Strings.HandInCraftTooltipFormat, crafts, row.Name);
    }

    /// <summary>
    /// Crafts to ask Artisan for: the items missing (what counts toward the row: HQ only for an HQ item; all of it for
    /// another character on view, whose count is unknown) over what one craft makes.
    /// </summary>
    private static int CraftAmountFor(SessionState session, HandInRow row, HandInRecipe? recipe) =>
        HandInActions.CraftAmount(row.Item, recipe, session.IsLive && row.Count is { } count ? count.UsableFor(row.Item) : null);

    private void DrawGatherButton(HandInRow row, GatherPlugin plugin, bool labelled, float slotWidth)
    {
        var state = HandInActions.Gather(row.Item, plugin);
        if (state == HandOffState.NotApplicable)
        {
            // Not gatherable: the slot stays empty so the craft button keeps its place.
            ImGui.Dummy(new Vector2(slotWidth, Chrome.ActionPillHeight));
            return;
        }

        var command = HandInActions.GatherCommandFor(row.Item);
        var tooltip = state == HandOffState.Ready
            ? WithSetupNote(string.Format(CultureInfo.CurrentCulture, Strings.HandInGatherTooltipFormat, command), CompanionPlugin.GatherBuddy)
            : CompanionPlugins.DisabledReason(CompanionPlugin.GatherBuddy) ?? Strings.HandInNeedsGatherBuddy;
        var fish = row.Item.Gather == GatherKind.Fish;
        var icon = fish ? FishIcon : GatherIcon;
        var label = !labelled ? null : fish ? Strings.HandInFishShort : Strings.HandInGatherShort;
        if (Chrome.ActionPill("##gather", icon, label, PillTone.Normal, state == HandOffState.Ready, tooltip) && command is not null && GatherBuddy is { } gather)
        {
            ShowHandInNote(gather.Run(command)
                ? string.Format(CultureInfo.CurrentCulture, Strings.HandInSentToGatherBuddyFormat, row.Name)
                : Strings.HandInGatherBuddyFailed);
        }
    }

    /// <summary>"Copy missing items" with its menu.</summary>
    private void DrawHandInFooter(SessionState session, QuestRecord quest)
    {
        if (Chrome.IconButtonRound("##handInCopy", HandInCopyIcon, Strings.HandInCopyTooltip))
        {
            ImGui.OpenPopup(HandInCopyMenuId);
        }

        using var popup = ImRaii.Popup(HandInCopyMenuId);
        if (!popup)
        {
            return;
        }

        var owned = OwnedLookup(session);
        var mine = MissingItems.For([quest], owned);
        DrawCopyItems(mine, Strings.HandInCopyLinkThis, Strings.HandInCopyListThis);
        ImGui.Separator();
        var pinned = PinnedOpenQuests(session);
        var all = pinned.Count == 0 ? [] : MissingItems.For(pinned, owned);
        DrawCopyItems(all, Strings.HandInCopyLinkPinned, Strings.HandInCopyListPinned);
    }

    private void DrawCopyItems(IReadOnlyList<MissingItem> items, string linkLabel, string listLabel)
    {
        var any = items.Count > 0;
        if (ImGui.MenuItem(linkLabel, enabled: any) && MissingItems.TeamcraftLink(items) is { } link)
        {
            ImGui.SetClipboardText(link);
            ShowHandInNote(Strings.HandInCopied);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(any ? Strings.HandInCopyLinkTooltip : Strings.HandInNothingMissing);
        }

        if (ImGui.MenuItem(listLabel, enabled: any))
        {
            ImGui.SetClipboardText(MissingItems.TextList(items));
            ShowHandInNote(Strings.HandInCopied);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(any ? Strings.HandInCopyListTooltip : Strings.HandInNothingMissing);
        }
    }

    /// <summary>
    /// What counts toward an item (by id and whether it must be HQ) from the logged-in character's stock, when it is the
    /// one on view; otherwise every item counts as missing.
    /// </summary>
    private Func<uint, bool, int?> OwnedLookup(SessionState session)
    {
        if (!session.IsLive || Stock is not { } stock)
        {
            return static (_, _) => null;
        }

        return (id, hq) => stock.For(id).Usable(hq);
    }

    /// <summary>The pinned quests not done yet whose names the shield shows (a masked quest's items are not copied).</summary>
    private List<QuestRecord> PinnedOpenQuests(SessionState session)
    {
        var result = new List<QuestRecord>();
        if (session.Bundle is not { } bundle)
        {
            return result;
        }

        foreach (var rowId in runner.PinnedInOrder)
        {
            if (bundle.Catalog.GetByRowId(rowId) is not { HandInItems.Count: > 0 } quest || session.Spoilers.IsMasked(quest))
            {
                continue;
            }

            var state = session.States.TryGetValue(rowId, out var evaluation) ? evaluation.State : QuestState.Unknown;
            if (state is QuestState.Completed or QuestState.DoneThisCycle or QuestState.Foreclosed)
            {
                continue;
            }

            result.Add(quest);
        }

        return result;
    }

    /// <summary>What a hand-in button just did, said in the status bar (feature plan v6, U4) rather than in a line that would push the card down.</summary>
    private void ShowHandInNote(string note) => ShowCompanionNote(note);
}
