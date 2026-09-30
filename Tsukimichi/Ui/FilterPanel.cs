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
    // The issuer chip's label, memoized per (NPC id, catalog): naming the NPC is a scan of the whole catalog.
    private string issuerChip = string.Empty;
    private uint issuerChipId;
    private QuestCatalog? issuerChipCatalog;
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

        DrawPresets(f, hasSnapshot, settings);
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
    /// One-click presets as toggle chips at the head of the panel; at most one is on, and clicking the active one
    /// turns it off. My level and Stalled read the snapshot, so they are disabled in browse mode.
    /// </summary>
    private void DrawPresets(FilterSet f, bool hasSnapshot, Configuration settings)
    {
        ImGui.TextDisabled(Strings.Presets);
        var first = true;
        PresetChip(Strings.PresetFeatureQuests, Strings.PresetFeatureQuestsTooltip, Preset.FeatureQuests, f, enabled: true, ref first);
        PresetChip(Strings.PresetLevelBand, Strings.PresetLevelBandTooltip, Preset.LevelBand, f, hasSnapshot, ref first);
        // Sprout mode (T19): without a character only A Realm Reborn is in reach, which is still a useful view.
        PresetChip(Strings.PresetSprout, Strings.PresetSproutTooltip, Preset.Sprout, f, enabled: true, ref first);
        PresetChip(Strings.PresetStalled, Strings.PresetStalledTooltip, Preset.Stalled, f, hasSnapshot, ref first);

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

    private void PresetChip(string label, string tooltip, Preset preset, FilterSet f, bool enabled, ref bool first)
    {
        // Chips share a line while they fit the column and flow onto the next one otherwise.
        if (!first)
        {
            var style = ImGui.GetStyle();
            var needed = ImGui.CalcTextSize(label).X + style.FramePadding.X * 2f + style.ItemSpacing.X;
            var limit = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
            if (ImGui.GetItemRectMax().X + needed <= limit)
            {
                ImGui.SameLine();
            }
        }

        first = false;
        var active = f.Preset == preset;
        using (ImRaii.Disabled(!enabled))
        using (ImRaii.PushColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive), active))
        {
            if (ImGui.SmallButton(label))
            {
                f.Preset = active ? Preset.None : preset;
                changed();
            }
        }

        Tip(enabled ? tooltip : Strings.NeedsSnapshot);
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
    /// Chips for every engaged filter on one line; clicking one clears that filter. Drawn inside the toolbar's
    /// fixed-height strip, so it never wraps and draws nothing when none is engaged. The NPC scope from the context
    /// menu (<see cref="QuestScope.Issuer"/>) has no tree node, so it is the first chip: "Quests from Gerolt", named
    /// from <paramref name="current"/>; clearing it shows the whole journal again.
    /// </summary>
    /// <param name="current">The catalog, for the NPC's name; null while it is loading.</param>
    public void DrawChips(CatalogBundle? current)
    {
        var f = ui.Filters;
        var any = false;

        // Small buttons are text-high; centre them on the toolbar's frame-high row.
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + MathF.Max(0f, (ImGui.GetFrameHeight() - ImGui.GetTextLineHeight()) * 0.5f));

        if (ui.Scope.Kind == ScopeKind.VirtualIssuer)
        {
            // The scope is not a filter and is not persisted; the query re-runs on the dirty mark alone.
            Chip(IssuerChipText(ui.Scope.Id, current?.Catalog), ref any, () =>
            {
                ui.Scope = QuestScope.None;
                ui.MarkQueryDirty();
            }, notify: false, explanation: Strings.ChipIssuerTooltip);
        }

        if (ui.SearchText.Length > 0)
        {
            // The search is not persisted and QueryRunner applies an emptied search on its own, so no changed() here.
            Chip(Strings.ChipSearch, ref any, () => ui.SearchText = string.Empty, notify: false);
        }

        if (f.Preset != Preset.None)
        {
            Chip(FilterNames.PresetName(f.Preset), ref any, () => f.Preset = Preset.None);
        }

        if (f.HideCompletedEngaged())
        {
            Chip(Strings.HideCompleted, ref any, () =>
            {
                f.HideCompleted = false;
                f.PerCategoryHideCompleted.Clear();
            });
        }

        if (f.AvailableOnlyEngaged())
        {
            Chip(Strings.AvailableOnly, ref any, () =>
            {
                f.AvailableOnly = false;
                f.PerCategoryAvailableOnly.Clear();
            });
        }

        if (f.StateMask != QuestStateMask.All)
        {
            Chip(StateChipText(f), ref any, () => f.StateMask = QuestStateMask.All, explanation: stateChipTooltip);
        }

        if (f.Expansions.Count > 0)
        {
            Chip(Strings.ChipExpansion, ref any, () => f.Expansions.Clear());
        }

        if (f.LevelRangeEngaged())
        {
            Chip(LevelChipText(f), ref any, () =>
            {
                f.LevelMin = FilterSet.NoLevelMin;
                f.LevelMax = FilterSet.NoLevelMax;
            });
        }

        if (f.ClassJobCategoryId is not null)
        {
            Chip(JobPreview(f), ref any, () => f.ClassJobCategoryId = null);
        }

        if (f.RewardKindsEngaged())
        {
            Chip(Strings.RewardKinds, ref any, () => f.RewardKinds.Clear());
        }

        if (f.RepeatableOnly)
        {
            Chip(Strings.ChipRepeatable, ref any, () => f.RepeatableOnly = false);
        }

        if (f.SeasonalActiveOnly)
        {
            Chip(Strings.ChipSeasonal, ref any, () => f.SeasonalActiveOnly = false);
        }

        if (f.PinnedOnly)
        {
            Chip(Strings.ChipPinned, ref any, () => f.PinnedOnly = false);
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
    /// One active-filter chip. <paramref name="explanation"/>, when given, says what the chip's text means (the state
    /// chip names only a few excluded states and counts the rest) above the "click to clear" line.
    /// </summary>
    private void Chip(string label, ref bool any, Action clear, bool notify = true, string? explanation = null)
    {
        if (any)
        {
            ImGui.SameLine();
        }

        any = true;
        using var id = ImRaii.PushId(label);
        // The whole chip is its own close target, so it stands at least the minimum target tall.
        if (ImGui.Button(label, new Vector2(0f, UiMetrics.MinTarget)))
        {
            clear();
            if (notify)
            {
                changed();
            }
        }

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

    private string IssuerChipText(uint npcId, QuestCatalog? catalog)
    {
        if (issuerChip.Length == 0 || npcId != issuerChipId || !ReferenceEquals(catalog, issuerChipCatalog))
        {
            var name = catalog is null ? null : QuestDiscovery.IssuerName(catalog, npcId);
            issuerChip = name is null ? Strings.ChipIssuerUnknown : string.Format(CultureInfo.CurrentCulture, Strings.ChipIssuerFormat, name);
            issuerChipId = npcId;
            issuerChipCatalog = catalog;
        }

        return issuerChip;
    }

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
