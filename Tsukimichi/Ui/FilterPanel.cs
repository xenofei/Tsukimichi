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
/// The filter panel (spec §7) drawn in the left column above the tree, plus the active-filter chips under the search
/// box. Binds straight to <see cref="UiState.Filters"/>; every change calls <c>changed</c> so the window can mark the
/// query dirty and persist the filters.
/// </summary>
public sealed class FilterPanel
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

    private static readonly RewardKind[] Kinds = Enum.GetValues<RewardKind>();

    /// <summary>Fixed job-category choices: label and ClassJobCategory row id (null = all).</summary>
    private static readonly (string Label, uint? Id)[] JobChoices =
    [
        (Strings.JobAll, null),
        (Strings.JobDowDom, 142u),
        (Strings.JobDoh, 33u),
        (Strings.JobDol, 32u),
    ];

    private readonly UiState ui;
    private readonly Action changed;
    private readonly Action displayChanged;

    private CatalogBundle? bundle;
    private readonly List<(uint Id, string Name)> categories = [];
    private readonly List<(byte Id, string Name)> expansions = [];

    private byte currentJobCached = byte.MaxValue;
    private uint? currentJobCategory;

    private string stateChip = string.Empty;
    private string stateChipTooltip = string.Empty;
    private QuestStateMask stateChipMask = QuestStateMask.All;
    // The scope chip's label, memoized per (scope, catalog): naming a node is a scan of the whole catalog.
    private string scopeChip = string.Empty;
    private QuestScope scopeChipScope = QuestScope.None;
    private QuestCatalog? scopeChipCatalog;
    private string levelChip = string.Empty;
    private byte levelChipMin = byte.MaxValue;
    private byte levelChipMax;
    private string jobPreview = Strings.JobAll;
    private uint? jobPreviewId;
    private bool jobPreviewValid;

    /// <param name="changed">A filter changed: the window re-runs the query and persists the filters.</param>
    /// <param name="displayChanged">A display slider changed: the window persists the settings (no query re-run).</param>
    public FilterPanel(UiState ui, Action changed, Action displayChanged)
    {
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.changed = changed ?? throw new ArgumentNullException(nameof(changed));
        this.displayChanged = displayChanged ?? throw new ArgumentNullException(nameof(displayChanged));
    }

    /// <summary>
    /// The panel body. <paramref name="snapshot"/> null means browse mode: runtime-only filters are disabled.
    /// <paramref name="settings"/> receives the Display sliders' values.
    /// </summary>
    public void Draw(CatalogBundle current, CharacterSnapshot? snapshot, Configuration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        EnsureLists(current);
        var f = ui.Filters;
        var hasSnapshot = snapshot is not null;
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;

        DrawPresets(settings);
        ImGui.Separator();
        DrawRuntimeToggle(Strings.HideCompleted, Strings.HideCompletedTooltip, "##hideCompleted", hasSnapshot, f.HideCompleted, v => f.HideCompleted = v, f.PerCategoryHideCompleted);
        DrawRuntimeToggle(Strings.AvailableOnly, Strings.AvailableOnlyTooltip, "##availableOnly", hasSnapshot, f.AvailableOnly, v => f.AvailableOnly = v, f.PerCategoryAvailableOnly);
        DrawPinnedFirst();

        if (ImGui.CollapsingHeader(Strings.Advanced))
        {
            using var indent = ImRaii.PushIndent(UiMetrics.Px(8f));
            DrawStates(f);
            DrawExpansions(f);
            DrawLevelRange(f);
            DrawJobCategory(f, snapshot);
            DrawRewardKinds(f);
            Toggle(Strings.RepeatableOnly, Strings.RepeatableOnlyTooltip, f.RepeatableOnly, v => f.RepeatableOnly = v);
            Toggle(Strings.SeasonalActiveOnly, Strings.SeasonalActiveOnlyTooltip, f.SeasonalActiveOnly, v => f.SeasonalActiveOnly = v, hasSnapshot);
            Toggle(Strings.IncludeUnlisted, Strings.IncludeUnlistedTooltip, f.IncludeUnlisted, v => f.IncludeUnlisted = v);
            Toggle(Strings.PinnedOnly, Strings.PinnedOnlyTooltip, f.PinnedOnly, v => f.PinnedOnly = v);
            Toggle(Strings.AbandonedOnly, Strings.AbandonedOnlyTooltip, f.AbandonedOnly, v => f.AbandonedOnly = v, hasSnapshot);
        }

        if (ImGui.SmallButton(Strings.Reset))
        {
            ResetAll();
        }

        Tip(Strings.ResetTooltip);
        ImGui.Separator();
        DrawDisplay(settings);
        ImGui.Separator();
        ui.RecordSpan(UiRects.FilterPanel, start, width);
    }

    /// <summary>UI and icon scale sliders; the values take effect on the next frame and are saved with the settings.</summary>
    private void DrawDisplay(Configuration settings)
    {
        ImGui.TextDisabled(Strings.Display);
        var sliderWidth = UiMetrics.Px(150f);

        var uiScale = ScaleMetrics.ClampUiScale(settings.UiScale);
        ImGui.SetNextItemWidth(sliderWidth);
        if (ImGui.SliderFloat("##uiScale", ref uiScale, ScaleMetrics.MinUiScale, ScaleMetrics.MaxUiScale, Strings.ScaleFormat, ImGuiSliderFlags.AlwaysClamp))
        {
            settings.UiScale = uiScale;
            displayChanged();
        }

        Tip(Strings.UiScaleTooltip);
        ImGui.SameLine();
        ImGui.TextUnformatted(Strings.UiScale);

        var iconScale = ScaleMetrics.ClampIconScale(settings.IconScale);
        ImGui.SetNextItemWidth(sliderWidth);
        if (ImGui.SliderFloat("##iconScale", ref iconScale, ScaleMetrics.MinIconScale, ScaleMetrics.MaxIconScale, Strings.ScaleFormat, ImGuiSliderFlags.AlwaysClamp))
        {
            settings.IconScale = iconScale;
            displayChanged();
        }

        Tip(Strings.IconScaleTooltip);
        ImGui.SameLine();
        ImGui.TextUnformatted(Strings.IconScale);

        var isDefault = settings.UiScale == ScaleMetrics.DefaultUiScale && settings.IconScale == ScaleMetrics.DefaultIconScale;
        using (ImRaii.Disabled(isDefault))
        {
            if (ImGui.SmallButton(Strings.ResetDisplay))
            {
                settings.UiScale = ScaleMetrics.DefaultUiScale;
                settings.IconScale = ScaleMetrics.DefaultIconScale;
                displayChanged();
            }
        }

        Tip(Strings.ResetDisplayTooltip);
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

    private static readonly string[] QuickViewLabels =
        [Strings.PresetFeatureQuests, Strings.PresetLevelBand, Strings.PresetStalled, Strings.PresetStorySidequests, Strings.PresetSprout];

    private static readonly string[] QuickViewTooltips =
        [Strings.PresetFeatureQuestsTooltip, Strings.PresetLevelBandTooltip, Strings.PresetStalledTooltip, Strings.PresetStorySidequestsTooltip, Strings.PresetSproutTooltip];

    // Sprout mode (T19): without a character only A Realm Reborn is in reach, which is still a useful view.
    private static readonly bool[] QuickViewNeedsSnapshot = [false, true, true, false, false];

    private static readonly string[] QuickViewDisabledTooltips = BuildDisabledTooltips();

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

    /// <summary>
    /// The Quick views heading of the panel: the views themselves sit on the toolbar; the panel keeps the Stalled
    /// threshold they read.
    /// </summary>
    private void DrawPresets(Configuration settings)
    {
        ImGui.TextDisabled(Strings.Presets);
        Tip(Strings.QuickViewsPanelTooltip);

        // The Stalled threshold; a change re-runs the query and is saved with the settings.
        var days = settings.StalledDaysClamped;
        ImGui.SetNextItemWidth(UiMetrics.Px(110f));
        if (ImGui.SliderInt("##stalledDays", ref days, Configuration.MinStalledDays, Configuration.MaxStalledDays, Strings.StalledDaysFormat, ImGuiSliderFlags.AlwaysClamp))
        {
            settings.StalledDays = days;
            changed();
        }

        Tip(Strings.StalledDaysTooltip);
        ImGui.SameLine();
        ImGui.TextUnformatted(Strings.StalledDaysLabel);
    }

    /// <summary>The sort's pinned-first flag lives beside the filters; MainWindow persists it with the sort.</summary>
    private void DrawPinnedFirst()
    {
        var pinnedFirst = ui.Sort.PinnedFirst;
        if (ImGui.Checkbox(Strings.PinnedFirst, ref pinnedFirst))
        {
            ui.Sort = ui.Sort with { PinnedFirst = pinnedFirst };
            ui.MarkQueryDirty();
        }

        Tip(Strings.PinnedFirstTooltip);
    }

    private static void Tip(string text)
    {
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(text);
        }
    }

    /// <summary>
    /// Whether the chip row under the toolbar has anything to show: a tree scope other than All quests, or any filter
    /// the Filters badge counts (<see cref="FilterBadge"/>). The row is not drawn at all otherwise.
    /// </summary>
    public bool HasChips() => ui.Scope != QuestScope.None || FilterBadge.Count(ui.Filters) > 0;

    /// <summary>
    /// The chip row (T14, ui-revamp §2.1): the scope first ("Scope: Sidequests › Gridania", or the NPC from the context
    /// menu), clearing to All quests, then one <see cref="Chrome.Chip"/> per filter the Filters badge counts, each
    /// clearing its filter. The search and the quick view are not chips: the search pill and the Quick views control
    /// already show them, each with its own way out. Chips flow onto another line when the row is full. Records
    /// <see cref="UiRects.Chips"/>.
    /// </summary>
    /// <param name="current">The catalog, for the scope's names; null while it is loading.</param>
    public void DrawChips(CatalogBundle? current)
    {
        var f = ui.Filters;
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var any = false;
        var filtersChanged = false;

        if (ui.Scope != QuestScope.None
            && Chip("##chipScope", ScopeChipText(ui.Scope, current?.Catalog), ref any, ui.Scope.Kind == ScopeKind.VirtualIssuer ? Strings.ChipIssuerTooltip : Strings.ScopeChipTooltip))
        {
            // The scope is not a filter and is not persisted; the query re-runs on the dirty mark alone.
            ui.Scope = QuestScope.None;
            ui.MarkQueryDirty();
        }

        if (f.HideCompletedEngaged() && Chip("##chipHideCompleted", Strings.HideCompleted, ref any))
        {
            f.HideCompleted = false;
            f.PerCategoryHideCompleted.Clear();
            filtersChanged = true;
        }

        if (f.AvailableOnlyEngaged() && Chip("##chipAvailable", Strings.AvailableOnly, ref any))
        {
            f.AvailableOnly = false;
            f.PerCategoryAvailableOnly.Clear();
            filtersChanged = true;
        }

        if (f.StateMask != QuestStateMask.All && Chip("##chipStates", StateChipText(f), ref any, stateChipTooltip))
        {
            f.StateMask = QuestStateMask.All;
            filtersChanged = true;
        }

        if (f.Expansions.Count > 0 && Chip("##chipExpansion", Strings.ChipExpansion, ref any))
        {
            f.Expansions.Clear();
            filtersChanged = true;
        }

        if (f.LevelRangeEngaged() && Chip("##chipLevel", LevelChipText(f), ref any))
        {
            f.LevelMin = FilterSet.NoLevelMin;
            f.LevelMax = FilterSet.NoLevelMax;
            filtersChanged = true;
        }

        if (f.ClassJobCategoryId is not null && Chip("##chipJob", JobPreview(f), ref any))
        {
            f.ClassJobCategoryId = null;
            filtersChanged = true;
        }

        if (f.RewardKindsEngaged() && Chip("##chipRewards", Strings.RewardKinds, ref any))
        {
            f.RewardKinds.Clear();
            filtersChanged = true;
        }

        if (f.RepeatableOnly && Chip("##chipRepeatable", Strings.ChipRepeatable, ref any))
        {
            f.RepeatableOnly = false;
            filtersChanged = true;
        }

        if (f.SeasonalActiveOnly && Chip("##chipSeasonal", Strings.ChipSeasonal, ref any))
        {
            f.SeasonalActiveOnly = false;
            filtersChanged = true;
        }

        if (f.PinnedOnly && Chip("##chipPinned", Strings.ChipPinned, ref any))
        {
            f.PinnedOnly = false;
            filtersChanged = true;
        }

        if (f.AbandonedOnly && Chip("##chipAbandoned", Strings.AbandonedChip, ref any))
        {
            f.AbandonedOnly = false;
            filtersChanged = true;
        }

        if (filtersChanged)
        {
            changed();
        }

        if (any)
        {
            var end = ImGui.GetItemRectMax();
            ui.RecordRect(UiRects.Chips, start, new Vector2(start.X + width, end.Y));
        }
        else
        {
            ui.Rects.Remove(UiRects.Chips);
        }
    }

    /// <summary>Clears every filter and the search text.</summary>
    public void ResetAll()
    {
        ui.Filters.Reset();
        ui.SearchText = string.Empty;
        changed();
    }

    /// <summary>
    /// One chip of the chip row, flowing onto the next line when the row is full; true on the click that clears it.
    /// <paramref name="explanation"/>, when given, says what the chip's text means (the state chip names only a few
    /// excluded states and counts the rest) above the "click to clear" line.
    /// </summary>
    private static bool Chip(string id, string label, ref bool any, string? explanation = null)
    {
        if (any)
        {
            var needed = Chrome.ChipWidth(label) + ImGui.GetStyle().ItemSpacing.X;
            var limit = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
            if (ImGui.GetItemRectMax().X + needed <= limit)
            {
                ImGui.SameLine();
            }
        }

        any = true;
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

        return clicked;
    }

    private void DrawRuntimeToggle(string label, string tooltip, string popupId, bool hasSnapshot, bool value, Action<bool> set, Dictionary<uint, bool> overrides)
    {
        using (ImRaii.Disabled(!hasSnapshot))
        {
            if (ImGui.Checkbox(label, ref value))
            {
                set(value);
                changed();
            }
        }

        Tip(hasSnapshot ? tooltip : Strings.NeedsSnapshot);

        ImGui.SameLine();
        using var id = ImRaii.PushId(popupId);
        using (ImRaii.Disabled(!hasSnapshot))
        {
            if (ImGui.SmallButton(Strings.Overrides))
            {
                ImGui.OpenPopup(popupId);
            }
        }

        Tip(hasSnapshot ? Strings.OverridesTooltip : Strings.NeedsSnapshot);

        using var popup = ImRaii.Popup(popupId);
        if (!popup)
        {
            return;
        }

        // Opened from the left column (own font scale 1), so the popup scales itself.
        UiMetrics.ApplyFontScale();
        var width = UiMetrics.Px(90f);
        foreach (var (categoryId, name) in categories)
        {
            using var row = ImRaii.PushId((int)categoryId);
            var current = overrides.TryGetValue(categoryId, out var v) ? (v ? 1 : 2) : 0;
            ImGui.SetNextItemWidth(width);
            if (ImGui.Combo("##override", ref current, Strings.OverrideOptions))
            {
                if (current == 0)
                {
                    overrides.Remove(categoryId);
                }
                else
                {
                    overrides[categoryId] = current == 1;
                }

                changed();
            }

            ImGui.SameLine();
            ImGui.TextUnformatted(name);
        }
    }

    private void DrawStates(FilterSet f)
    {
        ImGui.TextDisabled(Strings.States);
        Tip(Strings.StatesTooltip);
        foreach (var state in StateOrder)
        {
            var mask = f.StateMask;
            var on = mask.Contains(state);
            if (ImGui.Checkbox(Strings.StateName(state), ref on))
            {
                f.StateMask = on ? mask | state.ToMask() : mask & ~state.ToMask();
                changed();
            }

            Tip(Strings.StatesTooltip);
        }
    }

    private void DrawExpansions(FilterSet f)
    {
        ImGui.TextDisabled(Strings.Expansions);
        Tip(Strings.ExpansionsTooltip);
        foreach (var (id, name) in expansions)
        {
            var on = f.Expansions.Contains(id);
            if (ImGui.Checkbox(name, ref on))
            {
                if (on)
                {
                    f.Expansions.Add(id);
                }
                else
                {
                    f.Expansions.Remove(id);
                }

                changed();
            }

            Tip(Strings.ExpansionsTooltip);
        }
    }

    private void DrawLevelRange(FilterSet f)
    {
        ImGui.TextDisabled(Strings.LevelRange);
        int min = f.LevelMin;
        int max = f.LevelMax == FilterSet.NoLevelMax ? LevelCap : f.LevelMax;
        ImGui.SetNextItemWidth(UiMetrics.Px(180f));
        if (ImGui.DragIntRange2("##level", ref min, ref max, 0.5f, 0, LevelCap, Strings.LevelFormat, Strings.LevelMaxFormat, ImGuiSliderFlags.AlwaysClamp))
        {
            f.LevelMin = (byte)Math.Clamp(min, 0, LevelCap);
            f.LevelMax = max >= LevelCap ? FilterSet.NoLevelMax : (byte)Math.Clamp(max, 0, LevelCap);
            changed();
        }

        Tip(Strings.LevelRangeTooltip);
    }

    private void DrawJobCategory(FilterSet f, CharacterSnapshot? snapshot)
    {
        ImGui.TextDisabled(Strings.JobCategory);
        var currentOnly = CurrentJobCategory(snapshot);
        ImGui.SetNextItemWidth(UiMetrics.Px(180f));
        using var combo = ImRaii.Combo("##job", JobPreview(f));
        Tip(Strings.JobCategoryTooltip);
        if (!combo)
        {
            return;
        }

        // The combo popup opens from the left column (own font scale 1), so it scales itself.
        UiMetrics.ApplyFontScale();

        foreach (var (label, id) in JobChoices)
        {
            if (ImGui.Selectable(label, f.ClassJobCategoryId == id))
            {
                f.ClassJobCategoryId = id;
                changed();
            }
        }

        using (ImRaii.Disabled(currentOnly is null))
        {
            if (ImGui.Selectable(Strings.JobCurrentOnly, currentOnly is not null && f.ClassJobCategoryId == currentOnly))
            {
                f.ClassJobCategoryId = currentOnly;
                changed();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(currentOnly is null ? Strings.NeedsSnapshot : Strings.JobCurrentOnlyTooltip);
        }
    }

    private void DrawRewardKinds(FilterSet f)
    {
        ImGui.TextDisabled(Strings.RewardKinds);
        Tip(Strings.RewardKindsTooltip);
        var width = UiMetrics.Px(80f);
        foreach (var kind in Kinds)
        {
            using var id = ImRaii.PushId((int)kind);
            var current = f.RewardKinds.TryGetValue(kind, out var v) ? v : TriState.Show;
            ImGui.SetNextItemWidth(width);
            DrawRewardKindCombo(f, kind, current);
            Tip(Strings.RewardKindsTooltip);
            ImGui.SameLine();
            ImGui.TextUnformatted(Strings.RewardKindName(kind));
        }
    }

    /// <summary>One reward kind's Hidden / Show / Only choice; the popup opens from the left column, so it scales itself.</summary>
    private void DrawRewardKindCombo(FilterSet f, RewardKind kind, TriState current)
    {
        using var combo = ImRaii.Combo("##kind", Strings.RewardOptionName(current));
        if (!combo)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        foreach (var option in RewardOptionOrder)
        {
            if (!ImGui.Selectable(Strings.RewardOptionName(option), option == current))
            {
                continue;
            }

            if (option == TriState.Show)
            {
                f.RewardKinds.Remove(kind);
            }
            else
            {
                f.RewardKinds[kind] = option;
            }

            changed();
        }
    }

    private void Toggle(string label, string tooltip, bool value, Action<bool> set, bool enabled = true)
    {
        using (ImRaii.Disabled(!enabled))
        {
            if (ImGui.Checkbox(label, ref value))
            {
                set(value);
                changed();
            }
        }

        Tip(enabled ? tooltip : Strings.NeedsSnapshot);
    }

    /// <summary>"States: −Completed, −Locked out", naming up to <see cref="MaxStateChipNames"/> excluded states then "+N"; rebuilt when the mask changes.</summary>
    private string StateChipText(FilterSet f)
    {
        if (stateChipMask == f.StateMask && stateChip.Length > 0)
        {
            return stateChip;
        }

        stateChipMask = f.StateMask;
        var text = Strings.ChipStatePrefix;
        var tooltip = Strings.ChipStateTooltipPrefix;
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

        stateChip = text;
        stateChipTooltip = tooltip;
        return stateChip;
    }

    /// <summary>
    /// "Scope: Sidequests › Gridania": the tree node narrowing the table, named from the catalog (a section alone, a
    /// category under its section, a genre under its category, the virtual nodes by their tree names, an NPC's quests
    /// as "Quests from Gerolt"). Memoized per (scope, catalog): naming a node scans the catalog once.
    /// </summary>
    private string ScopeChipText(QuestScope scope, QuestCatalog? catalog)
    {
        if (scopeChip.Length > 0 && scope == scopeChipScope && ReferenceEquals(catalog, scopeChipCatalog))
        {
            return scopeChip;
        }

        scopeChipScope = scope;
        scopeChipCatalog = catalog;
        scopeChip = string.Format(CultureInfo.CurrentCulture, Strings.ScopeChipFormat, ScopeName(scope, catalog));
        return scopeChip;
    }

    private static string ScopeName(QuestScope scope, QuestCatalog? catalog)
    {
        switch (scope.Kind)
        {
            case ScopeKind.VirtualFeature:
                return Strings.FeatureUnlocks;
            case ScopeKind.VirtualUnlisted:
                return Strings.RemovedFromGame;
            case ScopeKind.VirtualIssuer:
                var npc = catalog is null ? null : QuestDiscovery.IssuerName(catalog, scope.Id);
                return npc is null ? Strings.ChipIssuerUnknown : string.Format(CultureInfo.CurrentCulture, Strings.ChipIssuerFormat, npc);
        }

        if (catalog is not null)
        {
            foreach (var quest in catalog.All)
            {
                var j = quest.Journal;
                switch (scope.Kind)
                {
                    case ScopeKind.Section when j.SectionId == scope.Id:
                        return j.SectionName;
                    case ScopeKind.Category when j.CategoryId == scope.Id:
                        return ScopePath(j.SectionName, j.CategoryName);
                    case ScopeKind.Genre when j.GenreId == scope.Id:
                        return ScopePath(j.CategoryName, j.GenreName);
                }
            }
        }

        return Strings.ScopeUnnamed;
    }

    /// <summary>"Parent › Child", or the child alone when the two carry the same name (a folded tree node).</summary>
    private static string ScopePath(string parent, string child) =>
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
            levelChip = string.Format(CultureInfo.CurrentCulture, "Lv {0}–{1}", f.LevelMin, max);
        }

        return levelChip;
    }

    private string JobPreview(FilterSet f)
    {
        if (jobPreviewValid && jobPreviewId == f.ClassJobCategoryId)
        {
            return jobPreview;
        }

        jobPreviewValid = true;
        jobPreviewId = f.ClassJobCategoryId;
        jobPreview = Strings.JobAll;
        if (f.ClassJobCategoryId is { } id)
        {
            jobPreview = string.Format(CultureInfo.CurrentCulture, "{0} {1}", Strings.JobCategory, id);
            foreach (var (label, choiceId) in JobChoices)
            {
                if (choiceId == id)
                {
                    jobPreview = label;
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

    private void EnsureLists(CatalogBundle current)
    {
        if (ReferenceEquals(bundle, current))
        {
            return;
        }

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

        currentJobCached = byte.MaxValue;
        jobPreviewValid = false;
    }
}
