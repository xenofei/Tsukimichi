using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
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

    private CatalogBundle? bundle;
    private readonly List<(uint Id, string Name)> categories = [];
    private readonly List<(byte Id, string Name)> expansions = [];

    private byte currentJobCached = byte.MaxValue;
    private uint? currentJobCategory;

    private string levelChip = string.Empty;
    private byte levelChipMin = byte.MaxValue;
    private byte levelChipMax;
    private string jobPreview = Strings.JobAll;
    private uint? jobPreviewId;
    private bool jobPreviewValid;

    public FilterPanel(UiState ui, Action changed)
    {
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.changed = changed ?? throw new ArgumentNullException(nameof(changed));
    }

    /// <summary>The panel body. <paramref name="snapshot"/> null means browse mode: runtime-only filters are disabled.</summary>
    public void Draw(CatalogBundle current, CharacterSnapshot? snapshot)
    {
        EnsureLists(current);
        var f = ui.Filters;
        var hasSnapshot = snapshot is not null;
        var scale = ImGuiHelpers.GlobalScale;

        DrawRuntimeToggle(Strings.HideCompleted, "##hideCompleted", hasSnapshot, f.HideCompleted, v => f.HideCompleted = v, f.PerCategoryHideCompleted);
        DrawRuntimeToggle(Strings.AvailableOnly, "##availableOnly", hasSnapshot, f.AvailableOnly, v => f.AvailableOnly = v, f.PerCategoryAvailableOnly);

        if (ImGui.CollapsingHeader(Strings.Advanced))
        {
            using var indent = ImRaii.PushIndent(8f);
            DrawStates(f);
            DrawExpansions(f);
            DrawLevelRange(f, scale);
            DrawJobCategory(f, snapshot, scale);
            DrawRewardKinds(f, scale);
            Toggle(Strings.RepeatableOnly, f.RepeatableOnly, v => f.RepeatableOnly = v);
            Toggle(Strings.SeasonalActiveOnly, f.SeasonalActiveOnly, v => f.SeasonalActiveOnly = v, hasSnapshot);
            Toggle(Strings.IncludeUnlisted, f.IncludeUnlisted, v => f.IncludeUnlisted = v);
            Toggle(Strings.PinnedOnly, f.PinnedOnly, v => f.PinnedOnly = v);
        }

        if (ImGui.SmallButton(Strings.Reset))
        {
            ResetAll();
        }

        ImGui.Separator();
    }

    /// <summary>Chips for every engaged filter; clicking one clears that filter. Draws nothing when none is engaged.</summary>
    public void DrawChips()
    {
        var f = ui.Filters;
        var any = false;

        if (ui.SearchText.Length > 0)
        {
            Chip(Strings.ChipSearch, ref any, () => ui.SearchText = string.Empty);
        }

        if (f.HideCompletedEngaged)
        {
            Chip(Strings.HideCompleted, ref any, () =>
            {
                f.HideCompleted = false;
                f.PerCategoryHideCompleted.Clear();
            });
        }

        if (f.AvailableOnlyEngaged)
        {
            Chip(Strings.AvailableOnly, ref any, () =>
            {
                f.AvailableOnly = false;
                f.PerCategoryAvailableOnly.Clear();
            });
        }

        if (f.StateMask != QuestStateMask.All)
        {
            Chip(Strings.ChipState, ref any, () => f.StateMask = QuestStateMask.All);
        }

        if (f.Expansions.Count > 0)
        {
            Chip(Strings.ChipExpansion, ref any, () => f.Expansions.Clear());
        }

        if (f.LevelRangeEngaged)
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

        if (f.RewardKindsEngaged)
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

        if (any)
        {
            ImGui.NewLine();
        }
    }

    /// <summary>Clears every filter and the search text.</summary>
    public void ResetAll()
    {
        ui.Filters.Reset();
        ui.SearchText = string.Empty;
        changed();
    }

    private void Chip(string label, ref bool any, Action clear)
    {
        if (any)
        {
            ImGui.SameLine();
        }

        any = true;
        using var id = ImRaii.PushId(label);
        if (ImGui.SmallButton(label))
        {
            clear();
            changed();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.ChipTooltip);
        }
    }

    private void DrawRuntimeToggle(string label, string popupId, bool hasSnapshot, bool value, Action<bool> set, Dictionary<uint, bool> overrides)
    {
        using (ImRaii.Disabled(!hasSnapshot))
        {
            if (ImGui.Checkbox(label, ref value))
            {
                set(value);
                changed();
            }
        }

        if (!hasSnapshot && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(Strings.NeedsSnapshot);
        }

        ImGui.SameLine();
        using var id = ImRaii.PushId(popupId);
        using (ImRaii.Disabled(!hasSnapshot))
        {
            if (ImGui.SmallButton(Strings.Overrides))
            {
                ImGui.OpenPopup(popupId);
            }
        }

        using var popup = ImRaii.Popup(popupId);
        if (!popup)
        {
            return;
        }

        var width = 90f * ImGuiHelpers.GlobalScale;
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
        foreach (var state in StateOrder)
        {
            var mask = f.StateMask;
            var on = mask.Contains(state);
            if (ImGui.Checkbox(Strings.StateName(state), ref on))
            {
                f.StateMask = on ? mask | state.ToMask() : mask & ~state.ToMask();
                changed();
            }
        }
    }

    private void DrawExpansions(FilterSet f)
    {
        ImGui.TextDisabled(Strings.Expansions);
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
        }
    }

    private void DrawLevelRange(FilterSet f, float scale)
    {
        ImGui.TextDisabled(Strings.LevelRange);
        int min = f.LevelMin;
        int max = f.LevelMax == FilterSet.NoLevelMax ? LevelCap : f.LevelMax;
        ImGui.SetNextItemWidth(180f * scale);
        if (ImGui.DragIntRange2("##level", ref min, ref max, 0.5f, 0, LevelCap, Strings.LevelFormat, Strings.LevelMaxFormat, ImGuiSliderFlags.AlwaysClamp))
        {
            f.LevelMin = (byte)Math.Clamp(min, 0, LevelCap);
            f.LevelMax = max >= LevelCap ? FilterSet.NoLevelMax : (byte)Math.Clamp(max, 0, LevelCap);
            changed();
        }
    }

    private void DrawJobCategory(FilterSet f, CharacterSnapshot? snapshot, float scale)
    {
        ImGui.TextDisabled(Strings.JobCategory);
        var currentOnly = CurrentJobCategory(snapshot);
        ImGui.SetNextItemWidth(180f * scale);
        using var combo = ImRaii.Combo("##job", JobPreview(f));
        if (!combo)
        {
            return;
        }

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
            ImGui.SetTooltip(currentOnly is null ? Strings.NeedsSnapshot : Strings.JobCurrentOnlyTooltip);
        }
    }

    private void DrawRewardKinds(FilterSet f, float scale)
    {
        ImGui.TextDisabled(Strings.RewardKinds);
        var width = 80f * scale;
        foreach (var kind in Kinds)
        {
            using var id = ImRaii.PushId((int)kind);
            var current = (int)(f.RewardKinds.TryGetValue(kind, out var v) ? v : TriState.Show);
            ImGui.SetNextItemWidth(width);
            if (ImGui.Combo("##kind", ref current, Strings.RewardOptions))
            {
                var value = (TriState)current;
                if (value == TriState.Show)
                {
                    f.RewardKinds.Remove(kind);
                }
                else
                {
                    f.RewardKinds[kind] = value;
                }

                changed();
            }

            ImGui.SameLine();
            ImGui.TextUnformatted(Strings.RewardKindName(kind));
        }
    }

    private void Toggle(string label, bool value, Action<bool> set, bool enabled = true)
    {
        using (ImRaii.Disabled(!enabled))
        {
            if (ImGui.Checkbox(label, ref value))
            {
                set(value);
                changed();
            }
        }

        if (!enabled && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(Strings.NeedsSnapshot);
        }
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
