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
using Tsukimichi.Core.Ui;
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

    /// <summary>At most this many other shops a "Where" line's tooltip lists per kind before "+N more".</summary>
    private const int WhereOthersShown = 4;

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

    private sealed class HandInRow(HandInItem item, string name, string detail)
    {
        public HandInItem Item { get; } = item;

        /// <summary>The item sources <see cref="Where"/> was read from; it is read again when they land.</summary>
        public ItemSourceIndex? WhereSources { get; set; }

        /// <summary>The fingerprint of the spoiler shield <see cref="Where"/> was composed through (1.20.0 N6).</summary>
        public int WhereShield { get; set; }

        /// <summary>Where to get the item (1.19, N5): its first two sources; null while the sources are read or when none is known.</summary>
        public WhereSummary? Where { get; set; }

        /// <summary>Every source with its other shops, for the "Where" line's tooltip.</summary>
        public string WhereTooltip { get; set; } = string.Empty;

        /// <summary>Where more of it sits than the inventory (the game counts the inventory only); none otherwise.</summary>
        public HandInPlace Misplaced { get; set; }

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

                // Where the game data gets it (1.19, N5), then, when the inventory alone is short, where the rest sits.
                if (WhereOf(row) is { } where)
                {
                    DrawWhere(row, where, textRoom);
                }

                if (live && row.Misplaced != HandInPlace.None)
                {
                    TextFlow.Wrapped(row.Misplaced == HandInPlace.Saddlebag ? Strings.HandInMisplacedSaddlebag : Strings.HandInMisplacedArmoury, textRoom, Theme.U32(Theme.Surface.TextSecondary));
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
    /// The row's "Where" line (1.19, N5; spec-1.19 "N5"), composed once per item source index
    /// (<see cref="WhereToGet.Summary(ItemSources?)"/>): null until the index lands or when the data knows no source.
    /// The tooltip lists every source with the other shops of its kind.
    /// </summary>
    private WhereSummary? WhereOf(HandInRow row)
    {
        var sources = links.ItemSources;
        // A vendor, place or duty past the story point is named by its placeholder (1.20.0 N6), and has no spot to flag.
        var spoilers = runner.Spoilers;
        if (ReferenceEquals(row.WhereSources, sources) && row.WhereShield == spoilers.Fingerprint)
        {
            return row.Where;
        }

        row.WhereSources = sources;
        row.WhereShield = spoilers.Fingerprint;
        row.Where = sources is null ? null : WhereToGet.Summary(sources.For(row.Item.ItemId), spoilers);
        row.WhereTooltip = string.Empty;
        if (row.Where is not { } where)
        {
            return null;
        }

        var tooltip = new List<string>(where.Sources.Count * 2);
        foreach (var line in where.Sources)
        {
            tooltip.Add(line.Lead);
            for (var i = 0; i < line.Others.Count && i < WhereOthersShown; i++)
            {
                tooltip.Add("  " + line.Others[i]);
            }

            if (line.Others.Count > WhereOthersShown)
            {
                tooltip.Add("  " + string.Format(CultureInfo.CurrentCulture, Strings.HandInWhereMoreFormat, line.Others.Count - WhereOthersShown));
            }
        }

        // Where the vendor the Flag marks stands, as the map prints it.
        if (where.Spot is { } spot)
        {
            tooltip.Add(string.Format(CultureInfo.CurrentCulture, Strings.HandInWhereFlagTooltipFormat, SourceText.Spot(spot, spoilers)));
        }

        row.WhereTooltip = string.Join("\n", tooltip);
        return where;
    }

    /// <summary>
    /// One item's "Where" line in the caption size, its first source's game icon at 14 px before it, then its actions as
    /// small row pills: Flag (and, while the automation level shows Teleport, Teleport to the aetheryte nearest the
    /// vendor) for a shop the data places, Open Gathering Log for a gathered item (the game's own log shows the nodes;
    /// Tsukimichi never names one). Craft with Artisan stays the row's own pill. Nothing here moves on hover.
    /// </summary>
    private void DrawWhere(HandInRow row, WhereSummary where, float width)
    {
        using (Typography.Caption())
        {
            var icon = UiMetrics.Px(14f);
            var gap = UiMetrics.Px(6f);
            var lineHeight = ImGui.GetTextLineHeight();
            if (where.Icon != 0)
            {
                var at = ImGui.GetCursorScreenPos();
                ImGui.SetCursorScreenPos(new Vector2(at.X, at.Y + MathF.Max(0f, (lineHeight - icon) * 0.5f)));
                GameIcon.Draw(textures, where.Icon, icon);
                ImGui.SameLine(0f, gap);
                ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, at.Y));
            }

            TextFlow.Wrapped(where.Text, MathF.Max(1f, width - (where.Icon != 0 ? icon + gap : 0f)), Theme.U32(Theme.Surface.TextSecondary));
            if (ImGui.IsItemHovered() && row.WhereTooltip.Length > 0)
            {
                UiMetrics.Tooltip(where.Text, row.WhereTooltip);
            }
        }

        var any = false;
        if (where.Spot is { } spot)
        {
            any = true;
            DrawSpotActions(spot);
        }

        if (where.GatheringLog)
        {
            if (any)
            {
                ImGui.SameLine();
            }

            var canOpen = links.CanOpenGatheringLog(row.Item.ItemId);
            if (TravelControls.RowButton("##gatheringLog", GameIconRef.Tile(WhereToGet.GatheringLogIcon), Strings.HandInOpenGatheringLog, canOpen)
                && !links.OpenGatheringLog(row.Item.ItemId))
            {
                ShowHandInNote(Strings.HandInGatheringLogFailed);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(Strings.HandInOpenGatheringLogTooltip);
            }
        }
    }

    /// <summary>
    /// Flag and, while the automation level shows Teleport (1.18 rule: hidden above the level, never greyed), Teleport
    /// to the aetheryte nearest a vendor's place, as small row pills on one line (1.19, N5 and C6).
    /// </summary>
    private void DrawSpotActions(WorldSpot spot)
    {
        var canFlag = links.CanFlagWorldSpot(spot);
        if (TravelControls.FlagButton(Strings.AutomationPillFlag, canFlag, "##spotFlag") && !links.FlagWorldSpot(spot))
        {
            ShowHandInNote(Strings.HandInWhereFlagFailed);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(canFlag ? string.Format(CultureInfo.CurrentCulture, Strings.HandInWhereFlagTooltipFormat, SourceText.Spot(spot, runner.Spoilers)) : Strings.HandInWhereFlagFailed);
        }

        if (!links.TeleportShown || links.AetheryteNear(spot) is not { } aetheryte)
        {
            return;
        }

        ImGui.SameLine();
        var canTeleport = links.CanTeleportTo(aetheryte.RowId);
        if (Chrome.ActionPill("##spotTeleport", ActionIcons.TeleportIcon, Strings.ActionTeleport, PillTone.Normal, canTeleport, size: PillLayout.Row))
        {
            links.TeleportTo(aetheryte.RowId, runner.Spoilers.Name(Core.Query.SpoilerKind.Aetheryte, aetheryte.Name));
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(links.TeleportToBlocked(aetheryte.RowId) ?? string.Format(CultureInfo.CurrentCulture, Strings.UnlocksMenuTeleportFormat, runner.Spoilers.Name(Core.Query.SpoilerKind.Aetheryte, aetheryte.Name)));
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
        row.Misplaced = count.MisplacedIn(row.Item);
        var text = row.Item.IsHq ? HqCountText(count)
            : count.HeldHq is not null ? InventoryCountText(row.Item, count)
            : (count.Held, count.Retainers) switch
            {
                (null, null) => Strings.HandInCountUnknown,
                ({ } held, null) => string.Format(CultureInfo.CurrentCulture, Strings.HandInHaveFormat, held),
                (null, { } retainers) => string.Format(CultureInfo.CurrentCulture, Strings.HandInRetainersOnlyFormat, retainers),
                ({ } held, { } retainers) => string.Format(CultureInfo.CurrentCulture, Strings.HandInHaveWithRetainersFormat, held, retainers),
            };

        // "0 / 1 · 1 in your saddlebag" (spec-1.19 "N5"): the game counts the inventory only, so the rest is named.
        row.CountText = row.Misplaced switch
        {
            HandInPlace.Saddlebag => text + Core.Evaluation.BlockerText.Separator + string.Format(CultureInfo.CurrentCulture, Strings.HandInInSaddlebagFormat, count.CountIn(HandInPlace.Saddlebag)),
            HandInPlace.Armoury => text + Core.Evaluation.BlockerText.Separator + string.Format(CultureInfo.CurrentCulture, Strings.HandInInArmouryFormat, count.CountIn(HandInPlace.Armoury)),
            _ => text,
        };
    }

    /// <summary>
    /// The counts line from the game's inventory read (1.19, N5): "0 / 1" when the amount is known, "none in your
    /// inventory" or "2 in your inventory" when it is not; the retainers' count after it when Allagan Tools gives one.
    /// </summary>
    private static string InventoryCountText(HandInItem item, HandInCount count)
    {
        var held = count.Held ?? 0;
        var text = item.AmountKnown
            ? string.Format(CultureInfo.CurrentCulture, Strings.HandInOfNeededFormat, held, item.Needed)
            : held == 0 ? Strings.HandInNoneInInventory : string.Format(CultureInfo.CurrentCulture, Strings.HandInInInventoryFormat, held);
        return count.Retainers is { } retainers && retainers > 0
            ? text + Core.Evaluation.BlockerText.Separator + string.Format(CultureInfo.CurrentCulture, Strings.HandInRetainersOnlyFormat, retainers)
            : text;
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
