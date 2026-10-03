using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Config;
using Tsukimichi.Core.Discovery;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The filter panel (spec §7), drawn as the sheet of the drawer over the Journal tree (feature plan v6 U2, plan v7 UI-2,
/// <see cref="DrawSheet"/>), plus the toolbar's Quick views control (T14) and the one-line chip lane at the top of the
/// quest list (<see cref="DrawLane"/>). Binds straight to <see cref="UiState.Filters"/>; every change calls
/// <c>changed</c> so the window can mark the query dirty and persist the filters.
/// </summary>
public sealed partial class FilterPanel
{
    private const int LevelCap = 100;
    private const int MaxStateChipNames = 3;

    /// <summary>Reward-kind combo entries in display order.</summary>
    private static readonly TriState[] RewardOptionOrder = [TriState.Hidden, TriState.Show, TriState.Only];

    private static readonly QuestState[] StateOrder =
    [
        QuestState.Ready,
        QuestState.ReadyOnOtherJob,
        QuestState.Accepted,
        QuestState.Blocked,
        QuestState.DoneThisCycle,
        QuestState.Completed,
        QuestState.Foreclosed,
        QuestState.Unknown,
    ];

    // The Show toggles' setters, built once: a lambda capturing the filter set would allocate a closure every frame.
    private static readonly Action<FilterSet, bool> SetHideCompleted = static (f, v) => f.HideCompleted = v;
    private static readonly Action<FilterSet, bool> SetAvailableOnly = static (f, v) => f.AvailableOnly = v;

    /// <summary>Fixed job-category choices: ClassJobCategory row ids (null = all), labelled by <see cref="JobChoiceLabels"/>.</summary>
    private static readonly uint?[] JobChoiceIds = [null, 142u, 33u, 32u];

    /// <summary>The labels of <see cref="JobChoiceIds"/>, in the current language.</summary>
    private static readonly Localization.LocArray JobChoiceLabels = new(static () =>
        [Strings.JobAll, Strings.JobDowDom, Strings.JobDoh, Strings.JobDol]);

    private readonly UiState ui;
    private readonly Action changed;

    private CatalogBundle? bundle;
    private readonly List<(uint Id, string Name)> categories = [];
    private readonly List<(byte Id, string Name)> expansions = [];

    /// <summary>The "Added in" combo's entries (P8): series ("7.5") and label ("7.5x  (61)"), newest first.</summary>
    private readonly List<(string Series, string Label)> patchSeries = [];

    /// <summary>
    /// How many quests are newer than the shipped data (feature plan v5, 1.5.0): above zero, the Added in combo offers
    /// "New since data" first. Null (before the main window attaches it) reads as none.
    /// </summary>
    public Func<int>? NewSinceDataCount { get; set; }

    private string newSinceDataOption = string.Empty;
    private int newSinceDataOptionCount = -1;
    private int newSinceDataOptionLanguage = -1;

    // The "Added in 7.5x" chip label, rebuilt only when the filter's value changes.
    private string addedInChip = string.Empty;
    private string? addedInChipFor;
    private int addedInChipLanguage = -1;

    private byte currentJobCached = byte.MaxValue;
    private uint? currentJobCategory;

    private string stateChip = string.Empty;
    private string stateChipTooltip = string.Empty;
    private QuestStateMask stateChipMask = QuestStateMask.All;
    private int stateChipLanguage = -1;
    private string levelChip = string.Empty;
    private byte levelChipMin = byte.MaxValue;
    private byte levelChipMax;
    private string jobPreview = string.Empty;
    private uint? jobPreviewId;
    private bool jobPreviewValid;
    private int jobPreviewLanguage = -1;

    /// <param name="changed">A filter changed: the window re-runs the query and persists the filters.</param>
    public FilterPanel(UiState ui, Action changed)
    {
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.changed = changed ?? throw new ArgumentNullException(nameof(changed));
    }

    /// <summary>
    /// The Quick views control on the toolbar (T14, game UX panel finding 7): a segmented control whose first segment
    /// is an explicit "All" (no quick view), then Unlocks, My level, Stalled, Story sidequests and Sprout mode. Exactly
    /// one segment is always on. My level and Stalled read the snapshot, so they are disabled in browse mode (an
    /// active one stays shown as active). Records <see cref="UiRects.QuickViews"/>.
    /// </summary>
    public void DrawQuickViews(bool hasSnapshot)
    {
        var f = ui.Filters;
        var selected = Array.IndexOf(QuickViewPresets, f.Preset);
        quickViewTooltips[0] = Strings.QuickViewAllTooltip;
        for (var i = 0; i < QuickViewPresets.Length; i++)
        {
            quickViewEnabled[i] = hasSnapshot || !QuickViewNeedsSnapshot[i];
            quickViewTooltips[i + 1] = quickViewEnabled[i] ? QuickViewTooltips[i] : QuickViewDisabledTooltips[i];
        }

        if (Chrome.SegmentedControl("##quickViews", ref selected, Strings.QuickViewAll, QuickViewLabels, quickViewEnabled, quickViewTooltips))
        {
            f.Preset = selected < 0 ? Preset.None : QuickViewPresets[selected];
            changed();
        }

        ui.RecordItem(UiRects.QuickViews);
    }

    /// <summary>The width <see cref="DrawQuickViews"/> takes at the current font.</summary>
    public static float QuickViewsWidth() => Chrome.SegmentedControlWidth(Strings.QuickViewAll, QuickViewLabels);

    /// <summary>Quick views in toolbar order (the segment after "All" is index 0).</summary>
    private static readonly Preset[] QuickViewPresets = [Preset.FeatureQuests, Preset.LevelBand, Preset.Stalled, Preset.StorySidequests, Preset.Sprout];

    private static string[] QuickViewLabels => quickViewLabelsText.Value;

    private static readonly Localization.LocArray quickViewLabelsText = new(static () =>
        [Strings.PresetFeatureQuests, Strings.PresetLevelBand, Strings.PresetStalled, Strings.PresetStorySidequests, Strings.PresetSprout]);

    private static string[] QuickViewTooltips => quickViewTooltipsText.Value;

    private static readonly Localization.LocArray quickViewTooltipsText = new(static () =>
        [Strings.PresetFeatureQuestsTooltip, Strings.PresetLevelBandTooltip, Strings.PresetStalledTooltip, Strings.PresetStorySidequestsTooltip, Strings.PresetSproutTooltip]);

    // Sprout mode (T19): without a character only A Realm Reborn is in reach, which is still a useful view.
    private static readonly bool[] QuickViewNeedsSnapshot = [false, true, true, false, false];

    private static string[] QuickViewDisabledTooltips => quickViewDisabledTooltipsText.Value;

    private static readonly Localization.LocArray quickViewDisabledTooltipsText = new(BuildDisabledTooltips);

    private readonly bool[] quickViewEnabled = new bool[QuickViewPresets.Length];

    /// <summary>Index 0 is the All segment's; the rest follow <see cref="QuickViewPresets"/>, swapped for the disabled text in browse mode.</summary>
    private readonly string[] quickViewTooltips = BuildTooltipSlots();

    private static string[] BuildDisabledTooltips()
    {
        var tips = new string[QuickViewTooltips.Length];
        for (var i = 0; i < tips.Length; i++)
        {
            tips[i] = QuickViewTooltips[i] + "\n" + Strings.NeedsSnapshot;
        }

        return tips;
    }

    private static string[] BuildTooltipSlots()
    {
        var tips = new string[QuickViewPresets.Length + 1];
        tips[0] = Strings.QuickViewAllTooltip;
        Array.Copy(QuickViewTooltips, 0, tips, 1, QuickViewTooltips.Length);
        return tips;
    }

    private static void Tip(string text)
    {
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(text);
        }
    }

    /// <summary>The lane's chips, most useful first; the order they show and spill behind "+N" in.</summary>
    private enum LaneChip
    {
        HiddenSelection,
        HideCompleted,
        Available,
        States,
        Expansion,
        AddedIn,
        Level,
        Job,
        Rewards,
        Repeatable,
        Seasonal,
        Pinned,
        Abandoned,
        OnceOnly,
    }

    private const string LaneMorePopup = "##chipLaneMore";

    /// <summary>"+1" … "+16": the "+N" chip's labels, built once.</summary>
    private static readonly string[] MoreLabels = BuildMoreLabels();

    // This frame's chips (reused, so collecting them allocates nothing once the list has grown).
    private readonly List<(LaneChip Kind, string Id, string Label, string? Explanation)> laneChips = new(16);

    private static string[] BuildMoreLabels()
    {
        var labels = new string[16];
        for (var i = 0; i < labels.Length; i++)
        {
            labels[i] = "+" + (i + 1).ToString(CultureInfo.InvariantCulture);
        }

        return labels;
    }

    /// <summary>
    /// The Journal's chip lane (feature plan v6 U2, decision 5): one line of fixed <paramref name="height"/> at the top of
    /// the quest list, under its title. One <see cref="Chrome.Chip"/> per filter the Filters badge counts, each clearing
    /// its filter, in a single line that never wraps: when they do not fit, the rest go behind a "+N" chip whose popover
    /// lists them, each still clearable, with Reset (<see cref="ChipLane"/>). The tree scope is not a chip: the list's
    /// title names it, with its own ×. When <paramref name="hiddenSelection"/> is set (the selected quest is not in the
    /// list, feature plan v6 U3), "Selected quest hidden · Show" leads the lane and shows it in the Journal. With no chip
    /// the lane holds <paramref name="caption"/> (a quick view's line), or nothing; either way its height never changes.
    /// Records <see cref="UiRects.Chips"/>.
    /// </summary>
    public void DrawLane(float height, QuestRecord? hiddenSelection, string? caption)
    {
        CollectChips(hiddenSelection);
        var start = ImGui.GetCursorScreenPos();
        var room = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var count = laneChips.Count;
        var chipHeight = Chrome.ChipHeightPx();
        var y = start.Y + MathF.Max(0f, (height - chipHeight) * 0.5f);

        Span<float> widths = stackalloc float[Math.Max(1, count)];
        for (var i = 0; i < count; i++)
        {
            var chip = laneChips[i];
            widths[i] = chip.Kind == LaneChip.HiddenSelection ? Chrome.ActionChipWidth(chip.Label) : Chrome.ChipWidth(chip.Label);
        }

        // The "+N" is measured at its widest (every chip behind it), so the fit never changes its mind about it.
        var shown = ChipLane.Fit(widths[..count], room, gap, Chrome.ActionChipWidth(MoreLabels[Math.Clamp(count, 1, MoreLabels.Length) - 1]));
        var x = start.X;
        var filtersChanged = false;
        for (var i = 0; i < shown; i++)
        {
            ImGui.SetCursorScreenPos(new Vector2(x, y));
            filtersChanged |= DrawLaneChip(i, hiddenSelection);
            x += widths[i] + gap;
        }

        if (shown < count)
        {
            ImGui.SetCursorScreenPos(new Vector2(x, y));
            if (Chrome.ActionChip("##chipLaneMore", MoreLabels[Math.Min(count - shown, MoreLabels.Length) - 1]))
            {
                ImGui.OpenPopup(LaneMorePopup);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.ChipLaneMoreTooltip);
            }

            filtersChanged |= DrawLaneMore(shown, hiddenSelection, new Vector2(x, y + chipHeight + UiMetrics.Px(4f)));
        }

        if (count == 0 && caption is { Length: > 0 })
        {
            var captionWidth = ImGui.CalcTextSize(caption).X;
            ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + MathF.Max(0f, (height - ImGui.GetTextLineHeight()) * 0.5f)));
            Chrome.EllipsisText(caption, room, Theme.U32(Theme.Surface.TextTertiary), captionWidth);
            if (captionWidth > room && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(caption);
            }
        }

        if (filtersChanged)
        {
            changed();
        }

        // The lane is one item of its fixed height, whatever it held.
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(room, height));
        if (count > 0)
        {
            ui.RecordRect(UiRects.Chips, start, start + new Vector2(room, height));
        }
        else
        {
            ui.Rects.Remove(UiRects.Chips);
        }
    }

    /// <summary>The "+N" popover under its chip: the chips the lane had no room for, one per line, each still clearable, and Reset.</summary>
    private bool DrawLaneMore(int from, QuestRecord? hiddenSelection, Vector2 position)
    {
        // Next-window data only when the popup will begin, or it would land on the next child window.
        if (!ImGui.IsPopupOpen(LaneMorePopup))
        {
            return false;
        }

        ImGui.SetNextWindowPos(position, ImGuiCond.Appearing);
        using var popup = ImRaii.Popup(LaneMorePopup);
        if (!popup)
        {
            return false;
        }

        UiMetrics.ApplyFontScale();
        var changedAny = false;
        for (var i = from; i < laneChips.Count; i++)
        {
            changedAny |= DrawLaneChip(i, hiddenSelection);
        }

        ImGui.Spacing();
        if (ImGui.SmallButton(Strings.Reset + "##chipLaneReset"))
        {
            ResetAll();
            ImGui.CloseCurrentPopup();
        }

        Tip(Strings.ResetTooltip);
        return changedAny;
    }

    /// <summary>The chips engaged this frame, in <see cref="LaneChip"/> order.</summary>
    private void CollectChips(QuestRecord? hiddenSelection)
    {
        laneChips.Clear();
        var f = ui.Filters;
        if (hiddenSelection is not null)
        {
            laneChips.Add((LaneChip.HiddenSelection, "##chipHiddenSelection", Strings.SelectedHiddenChip, null));
        }

        AddIf(f.HideCompletedEngaged(), LaneChip.HideCompleted, "##chipHideCompleted", Strings.HideCompleted);
        AddIf(f.AvailableOnlyEngaged(), LaneChip.Available, "##chipAvailable", Strings.AvailableOnly);
        if (f.StateMask != QuestStateMask.All)
        {
            laneChips.Add((LaneChip.States, "##chipStates", StateChipText(f), stateChipTooltip));
        }

        AddIf(f.Expansions.Count > 0, LaneChip.Expansion, "##chipExpansion", Strings.ChipExpansion);
        if (f.AddedInEngaged())
        {
            laneChips.Add((LaneChip.AddedIn, "##chipAddedIn", AddedInChipText(f), null));
        }

        if (f.LevelRangeEngaged())
        {
            laneChips.Add((LaneChip.Level, "##chipLevel", LevelChipText(f), null));
        }

        if (f.ClassJobCategoryId is not null)
        {
            laneChips.Add((LaneChip.Job, "##chipJob", JobPreview(f), null));
        }

        AddIf(f.RewardKindsEngaged(), LaneChip.Rewards, "##chipRewards", Strings.RewardKinds);
        AddIf(f.RepeatableOnly, LaneChip.Repeatable, "##chipRepeatable", Strings.ChipRepeatable);
        AddIf(f.SeasonalActiveOnly, LaneChip.Seasonal, "##chipSeasonal", Strings.ChipSeasonal);
        AddIf(f.PinnedOnly, LaneChip.Pinned, "##chipPinned", Strings.ChipPinned);
        AddIf(f.AbandonedOnly, LaneChip.Abandoned, "##chipAbandoned", Strings.AbandonedChip);
        AddIf(f.OnceOnlyStory, LaneChip.OnceOnly, "##chipOnceOnly", Strings.OnceOnlyStoryChip);
    }

    private void AddIf(bool engaged, LaneChip kind, string id, string label)
    {
        if (engaged)
        {
            laneChips.Add((kind, id, label, null));
        }
    }

    /// <summary>Draws chip <paramref name="index"/> at the cursor; true when its click cleared a filter.</summary>
    private bool DrawLaneChip(int index, QuestRecord? hiddenSelection)
    {
        var (kind, id, label, explanation) = laneChips[index];
        if (kind == LaneChip.HiddenSelection)
        {
            var show = Chrome.ActionChip(id, label, accent: true);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.SelectedHiddenChipTooltip);
            }

            if (show && hiddenSelection is not null)
            {
                ui.Reveal(hiddenSelection);
            }

            return false;
        }

        var clicked = Chrome.Chip(id, label);
        if (ImGui.IsItemHovered())
        {
            if (explanation is { Length: > 0 })
            {
                UiMetrics.Tooltip(explanation, Strings.ChipTooltip);
            }
            else
            {
                UiMetrics.Tooltip(Strings.ChipTooltip);
            }
        }

        if (!clicked)
        {
            return false;
        }

        var f = ui.Filters;
        switch (kind)
        {
            case LaneChip.HideCompleted:
                f.HideCompleted = false;
                f.PerCategoryHideCompleted.Clear();
                break;
            case LaneChip.Available:
                f.AvailableOnly = false;
                f.PerCategoryAvailableOnly.Clear();
                break;
            case LaneChip.States:
                f.StateMask = QuestStateMask.All;
                break;
            case LaneChip.Expansion:
                f.Expansions.Clear();
                break;
            case LaneChip.AddedIn:
                f.AddedIn = string.Empty;
                break;
            case LaneChip.Level:
                f.LevelMin = FilterSet.NoLevelMin;
                f.LevelMax = FilterSet.NoLevelMax;
                break;
            case LaneChip.Job:
                f.ClassJobCategoryId = null;
                break;
            case LaneChip.Rewards:
                f.RewardKinds.Clear();
                break;
            case LaneChip.Repeatable:
                f.RepeatableOnly = false;
                break;
            case LaneChip.Seasonal:
                f.SeasonalActiveOnly = false;
                break;
            case LaneChip.Pinned:
                f.PinnedOnly = false;
                break;
            case LaneChip.Abandoned:
                f.AbandonedOnly = false;
                break;
            case LaneChip.OnceOnly:
                f.OnceOnlyStory = false;
                break;
        }

        return true;
    }

    /// <summary>
    /// Clears every filter and the search text (Reset in the drawer, the chip lane's "+N" popover, the context dock's
    /// Clear and the empty list's reset), then offers the floating Undo, "Filters reset · Undo", which puts the filter
    /// set and the search back (plan v7 UI-2, <see cref="GuardedAction.ResetFilters"/>). Call it inside the window the
    /// click was in. Nothing happens when there is nothing to clear.
    /// </summary>
    public void ResetAll()
    {
        if (!FilterSummary.CanReset(ui.Filters, ui.SearchText))
        {
            return;
        }

        var before = ui.Filters.Clone();
        var search = ui.SearchText;
        ui.Filters.Reset();
        ui.SearchText = string.Empty;
        changed();
        if (SafetyRules.OffersUndo(GuardedAction.ResetFilters))
        {
            UndoToast.Show(Strings.UndoToastFiltersReset, () => Restore(before, search));
        }
    }

    /// <summary>Undo of <see cref="ResetAll"/>: the filter set and the search as they were.</summary>
    private void Restore(FilterSet before, string search)
    {
        ui.Filters = before;
        ui.SearchText = search;
        changed();
    }

    /// <summary>
    /// The "Added in" filter (P8): a combo of the patch series the catalog's quests were added in, newest first, with
    /// "Any patch" on top, then "New since data" while the game has quests newer than the shipped data (1.5.0).
    /// Disabled, showing "Any patch", when no quest has a known patch (quest_patches.json missing) and none is new.
    /// <paramref name="width"/> wide, at the cursor (the drawer's field pill style pushed by the caller).
    /// </summary>
    private void DrawAddedIn(FilterSet f, float width)
    {
        var preview = f.AddedInEngaged() ? AddedInChipText(f) : Strings.AddedInAny;
        ImGui.SetNextItemWidth(width);
        var newSinceData = NewSinceDataCount?.Invoke() ?? 0;
        using (ImRaii.Disabled(patchSeries.Count == 0 && newSinceData == 0 && !f.AddedInEngaged()))
        {
            using var combo = ImRaii.Combo("##addedIn", preview);
            Tip(Strings.AddedInTooltip);
            if (!combo)
            {
                return;
            }

            // The combo popup opens from the left column (own font scale 1), so it scales itself.
            UiMetrics.ApplyFontScale();
            if (ImGui.Selectable(Strings.AddedInAny, !f.AddedInEngaged()))
            {
                f.AddedIn = string.Empty;
                changed();
            }

            if (newSinceData > 0 || f.AddedInNewSinceData())
            {
                if (ImGui.Selectable(NewSinceDataOption(newSinceData), f.AddedInNewSinceData()))
                {
                    f.AddedIn = FilterSet.NewSinceData;
                    changed();
                }

                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.FreshnessShowNewTooltip);
                }
            }

            foreach (var (series, label) in patchSeries)
            {
                if (ImGui.Selectable(label, string.Equals(f.AddedIn, series, StringComparison.Ordinal)))
                {
                    f.AddedIn = series;
                    changed();
                }
            }
        }
    }

    /// <summary>"New since data  (12)", memoized per count and language.</summary>
    private string NewSinceDataOption(int count)
    {
        if (count != newSinceDataOptionCount || newSinceDataOptionLanguage != Localization.Loc.Version)
        {
            newSinceDataOptionCount = count;
            newSinceDataOptionLanguage = Localization.Loc.Version;
            newSinceDataOption = string.Format(CultureInfo.CurrentCulture, Strings.AddedInNewSinceDataOptionFormat, count);
        }

        return newSinceDataOption;
    }

    /// <summary>"Added in 7.5x", or "New since data", memoized per filter value.</summary>
    private string AddedInChipText(FilterSet f)
    {
        if (!string.Equals(addedInChipFor, f.AddedIn, StringComparison.Ordinal) || addedInChipLanguage != Localization.Loc.Version)
        {
            addedInChipFor = f.AddedIn;
            addedInChipLanguage = Localization.Loc.Version;
            addedInChip = f.AddedInNewSinceData()
                ? Strings.AddedInNewSinceData
                : string.Format(CultureInfo.CurrentCulture, Strings.AddedInChipFormat, f.AddedIn);
        }

        return addedInChip;
    }

    /// <summary>"States: −Completed, −Locked out", naming up to <see cref="MaxStateChipNames"/> excluded states then "+N"; rebuilt when the mask changes.</summary>
    private string StateChipText(FilterSet f)
    {
        if (stateChipMask == f.StateMask && stateChipLanguage == Localization.Loc.Version && stateChip.Length > 0)
        {
            return stateChip;
        }

        stateChipMask = f.StateMask;
        stateChipLanguage = Localization.Loc.Version;
        var text = string.Empty;
        var tooltip = string.Empty;
        var named = 0;
        var excluded = 0;
        foreach (var state in StateOrder)
        {
            if (f.StateMask.Contains(state))
            {
                continue;
            }

            // The tooltip names every excluded state, so the chip's "+N" has somewhere to be read in full.
            tooltip += (excluded > 0 ? Strings.ChipStateSeparator : string.Empty) + Strings.StateName(state);
            excluded++;
            if (named < MaxStateChipNames)
            {
                text += (named > 0 ? Strings.ChipStateSeparator : string.Empty) + Strings.ChipStateExcludedMarker + Strings.StateName(state);
                named++;
            }
        }

        if (excluded > named)
        {
            text += string.Format(CultureInfo.CurrentCulture, Strings.ChipStateMoreFormat, excluded - named);
        }

        stateChip = string.Format(CultureInfo.CurrentCulture, Strings.ChipStateFormat, text);
        stateChipTooltip = string.Format(CultureInfo.CurrentCulture, Strings.ChipStateTooltipFormat, tooltip);
        return stateChip;
    }

    /// <summary>
    /// The tree node <paramref name="scope"/> as its parent's name and its own (the parent empty for a section, a
    /// virtual node or an NPC's quests): the scope chip joins them, the quest table's title (R3 #6) draws the parent as
    /// a breadcrumb before the name. Scans the catalog once; callers memoize.
    /// </summary>
    internal static (string Parent, string Name) ScopeParts(QuestScope scope, QuestCatalog? catalog)
    {
        switch (scope.Kind)
        {
            case ScopeKind.VirtualFeature:
                return (string.Empty, Strings.FeatureUnlocks);
            case ScopeKind.VirtualUnlisted:
                return (string.Empty, Strings.RemovedFromGame);
            case ScopeKind.VirtualOtherPaths:
                return (string.Empty, Strings.OtherPaths);
            case ScopeKind.VirtualJustOpened:
                return (string.Empty, Strings.OpenedScopeName);
            case ScopeKind.VirtualNewlyReady:
                return (string.Empty, Strings.RailNewlyReady);
            case ScopeKind.VirtualIssuer:
                var npc = catalog is null ? null : QuestDiscovery.IssuerName(catalog, scope.Id);
                return (string.Empty, npc is null ? Strings.ChipIssuerUnknown : string.Format(CultureInfo.CurrentCulture, Strings.ChipIssuerFormat, npc));
        }

        if (catalog is not null)
        {
            foreach (var quest in catalog.All)
            {
                var j = quest.Journal;
                switch (scope.Kind)
                {
                    case ScopeKind.Section when j.SectionId == scope.Id:
                        return (string.Empty, j.SectionName);
                    case ScopeKind.Category when j.CategoryId == scope.Id:
                        return (j.SectionName, j.CategoryName);
                    case ScopeKind.Genre when j.GenreId == scope.Id:
                        return (j.CategoryName, j.GenreName);
                }
            }
        }

        return (string.Empty, Strings.ScopeUnnamed);
    }

    /// <summary>"Parent › Child", or the child alone when the two carry the same name (a folded tree node).</summary>
    internal static string ScopePath(string parent, string child) =>
        parent.Length == 0 || string.Equals(parent, child, StringComparison.Ordinal)
            ? child
            : string.Format(CultureInfo.CurrentCulture, Strings.FoldedScopeFormat, parent, child);

    private string LevelChipText(FilterSet f)
    {
        if (levelChipMin != f.LevelMin || levelChipMax != f.LevelMax)
        {
            levelChipMin = f.LevelMin;
            levelChipMax = f.LevelMax;
            var max = f.LevelMax == FilterSet.NoLevelMax ? LevelCap : f.LevelMax;
            levelChip = string.Format(CultureInfo.CurrentCulture, Strings.LevelRangeChipFormat, f.LevelMin, max);
        }

        return levelChip;
    }

    private string JobPreview(FilterSet f)
    {
        if (jobPreviewValid && jobPreviewId == f.ClassJobCategoryId && jobPreviewLanguage == Localization.Loc.Version)
        {
            return jobPreview;
        }

        jobPreviewValid = true;
        jobPreviewId = f.ClassJobCategoryId;
        jobPreviewLanguage = Localization.Loc.Version;
        jobPreview = Strings.JobAll;
        if (f.ClassJobCategoryId is { } id)
        {
            jobPreview = string.Format(CultureInfo.CurrentCulture, "{0} {1}", Strings.JobCategory, id);
            for (var i = 0; i < JobChoiceIds.Length; i++)
            {
                if (JobChoiceIds[i] == id)
                {
                    jobPreview = JobChoiceLabels.Value[i];
                    break;
                }
            }

            if (currentJobCategory == id)
            {
                jobPreview = Strings.JobCurrentOnly;
            }
        }

        return jobPreview;
    }

    /// <summary>The ClassJobCategory row whose only member is the snapshot's current job, found once per job.</summary>
    private uint? CurrentJobCategory(CharacterSnapshot? snapshot)
    {
        if (snapshot is null || bundle is null)
        {
            return null;
        }

        if (snapshot.CurrentJob == currentJobCached)
        {
            return currentJobCategory;
        }

        currentJobCached = snapshot.CurrentJob;
        currentJobCategory = null;
        jobPreviewValid = false;
        for (uint id = 2; id < 512; id++)
        {
            var count = 0;
            var matches = false;
            foreach (var job in bundle.Jobs.JobsIn(id))
            {
                count++;
                matches = job == snapshot.CurrentJob;
            }

            if (count == 1 && matches)
            {
                currentJobCategory = id;
                break;
            }
        }

        return currentJobCategory;
    }

    private int listsLanguage = -1;

    private void EnsureLists(CatalogBundle current)
    {
        if (ReferenceEquals(bundle, current) && listsLanguage == Localization.Loc.Version)
        {
            return;
        }

        listsLanguage = Localization.Loc.Version;
        bundle = current;
        categories.Clear();
        var seen = new HashSet<uint>();
        var ordered = new List<QuestRecord>(current.Catalog.All);
        ordered.Sort(static (a, b) => a.Journal.SortKey != b.Journal.SortKey ? a.Journal.SortKey.CompareTo(b.Journal.SortKey) : a.RowId.CompareTo(b.RowId));
        foreach (var quest in ordered)
        {
            if (quest.IsUnlisted || !seen.Add(quest.Journal.CategoryId))
            {
                continue;
            }

            categories.Add((quest.Journal.CategoryId, quest.Journal.CategoryName));
        }

        // The "Added in" choices (P8): each series some quest in the game was added in, newest first, with its count.
        patchSeries.Clear();
        foreach (var series in PatchIndex.For(current.Catalog).Series)
        {
            patchSeries.Add((series.Series, string.Format(CultureInfo.CurrentCulture, Strings.AddedInOptionFormat, series.Series, series.Quests)));
        }

        addedInChipFor = null;

        expansions.Clear();
        var ids = new List<uint>(current.Names.Expansions.Keys);
        ids.Sort();
        foreach (var id in ids)
        {
            if (id <= byte.MaxValue)
            {
                expansions.Add(((byte)id, current.Names.Expansion(id)));
            }
        }

        expansionLabels = new string[expansions.Count];
        for (var i = 0; i < expansions.Count; i++)
        {
            expansionLabels[i] = Strings.ExpansionShort(expansions[i].Id);
        }

        currentJobCached = byte.MaxValue;
        jobPreviewValid = false;
    }
}
