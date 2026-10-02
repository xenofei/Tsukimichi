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
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail pane's Hand in section (feature plan v5 side track, research C4 #2, #4 and C7 A, B): one row per item the
/// quest asks for (<see cref="QuestRecord.HandInItems"/>) with its icon, its name, the amount and quality when the game
/// data says, and how many the logged-in character holds (<see cref="HandInStock"/>: bags, armoury and saddlebag from
/// the game, retainers through Allagan Tools; an item asked for high quality counts only the game's HQ, Allagan Tools'
/// numbers are labelled "NQ+HQ" and never make it enough). Each row ends in two hand-off buttons (decision 1): Craft with Artisan
/// and, for an item a node or fishing hole yields, Gather with GatherBuddy; a button whose plugin is missing stays,
/// disabled, naming it. Under the rows, "Copy missing items" puts a Teamcraft import link or a "3x Item" list on the
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
    private const double HandInNoteSeconds = 5.0;

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
    private string handInNote = string.Empty;
    private double handInNoteUntil;
    private uint handInNoteRowId;

    /// <summary>Live item counts; null until the plugin attaches it, which leaves the counts line out.</summary>
    public HandInStock? Stock { get; set; }

    /// <summary>Artisan's IPC; null until the plugin attaches it (the Craft button then names Artisan as missing).</summary>
    public ArtisanIpc? Artisan { get; set; }

    /// <summary>GatherBuddy's commands; null until the plugin attaches it (the Gather button then names GatherBuddy as missing).</summary>
    public GatherBuddyCommands? GatherBuddy { get; set; }

    private sealed class HandInRow(HandInItem item, string name, string detail)
    {
        public HandInItem Item { get; } = item;

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

        BeginSection("##handIn", Strings.HandInSection, HandInIcon, handInMasked ? string.Empty : handInCaption, Theme.Surface.TextTertiary);
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
        // name, else the icons alone (still pills), their labels in the tooltips.
        var labelled = HandInPillsLabelled(iconSize);
        var craftWidth = Chrome.ActionPillWidth(CraftIcon, labelled ? Strings.HandInCraftShort : null);
        var gatherWidth = labelled
            ? MathF.Max(Chrome.ActionPillWidth(GatherIcon, Strings.HandInGatherShort), Chrome.ActionPillWidth(FishIcon, Strings.HandInFishShort))
            : Chrome.ActionPillWidth(GatherIcon, null);
        var buttons = craftWidth + ImGui.GetStyle().ItemSpacing.X + gatherWidth;
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
                TextFlow.Wrapped(row.Line, MathF.Max(1f, RoomTo(cardRight) - buttons - UiMetrics.Px(8f)));
                if (live && row.CountText.Length > 0)
                {
                    using (Theme.PushText(row.Enough ? Theme.Moon : Theme.Surface.TextSecondary))
                    {
                        ImGui.TextUnformatted(row.CountText);
                    }
                }
            }

            // The two hand-offs at the row's right edge, beside the name while they fit.
            SameLineOrWrap(buttons, cardRight);
            ImGui.SetCursorScreenPos(new Vector2(MathF.Max(ImGui.GetCursorScreenPos().X, cardRight - buttons), MathF.Max(ImGui.GetCursorScreenPos().Y, start.Y)));
            DrawCraftButton(session, row, artisanLoaded, artisanBusy, labelled);
            ImGui.SameLine();
            DrawGatherButton(row, gatherPlugin, labelled, gatherWidth);
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
    /// Whether the hand-off pills carry their labels: while the item's icon, the labelled pills and
    /// <see cref="HandInNameRoomLogical"/> of name fit the card.
    /// </summary>
    private bool HandInPillsLabelled(float iconSize)
    {
        var labelled = Chrome.ActionPillWidth(CraftIcon, Strings.HandInCraftShort)
            + ImGui.GetStyle().ItemSpacing.X
            + MathF.Max(Chrome.ActionPillWidth(GatherIcon, Strings.HandInGatherShort), Chrome.ActionPillWidth(FishIcon, Strings.HandInFishShort));
        return iconSize + ImGui.GetStyle().ItemSpacing.X + UiMetrics.Px(HandInNameRoomLogical) + labelled <= RoomTo(cardRight);
    }

    private void DrawCraftButton(SessionState session, HandInRow row, bool artisanLoaded, bool artisanBusy, bool labelled)
    {
        // Enough only from the live character's count: another character on view has no count to go by.
        var state = HandInActions.Craft(row.Item, artisanLoaded, artisanBusy, enough: session.IsLive && row.Enough);
        var recipe = state == HandOffState.Ready
            ? HandInActions.ChooseRecipe(row.Item, session.LiveSnapshot?.CurrentJob ?? 0, session.LiveSnapshot?.JobLevels)
            : null;
        var tooltip = state switch
        {
            HandOffState.Ready => CraftTooltip(session, row, recipe),
            HandOffState.Enough => Strings.HandInHaveEnough,
            // The registry's reason when it has one ("Artisan is installed but turned off…"), as every hand-off button says it.
            HandOffState.PluginMissing => CompanionPlugins.DisabledReason(CompanionPlugin.Artisan) ?? Strings.HandInNeedsArtisan,
            HandOffState.Busy => Strings.HandInArtisanBusy,
            _ => Strings.HandInNoRecipe,
        };
        var pressed = Chrome.ActionPill("##craft", CraftIcon, labelled ? Strings.HandInCraftShort : null, PillTone.Normal, state == HandOffState.Ready);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(tooltip);
        }

        if (!pressed)
        {
            return;
        }

        var amount = CraftAmountFor(session, row, recipe);
        var sent = recipe is not null && amount > 0 && Artisan is { } artisan && artisan.Craft(recipe.RecipeId, amount);
        ShowHandInNote(sent ? string.Format(CultureInfo.CurrentCulture, Strings.HandInSentToArtisanFormat, row.Name) : Strings.HandInArtisanFailed);
    }

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
            ? string.Format(CultureInfo.CurrentCulture, Strings.HandInGatherTooltipFormat, command)
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

    /// <summary>"Copy missing items" with its menu, and the note a hand-off leaves for a few seconds.</summary>
    private void DrawHandInFooter(SessionState session, QuestRecord quest)
    {
        if (handInNote.Length > 0 && handInNoteRowId == quest.RowId && ImGui.GetTime() < handInNoteUntil)
        {
            TextFlow.Wrapped(handInNote, RoomTo(cardRight), Theme.U32(Theme.Surface.TextSecondary));
        }

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

    private void ShowHandInNote(string note)
    {
        handInNote = note;
        handInNoteUntil = ImGui.GetTime() + HandInNoteSeconds;
        handInNoteRowId = handInRowId;
    }
}
