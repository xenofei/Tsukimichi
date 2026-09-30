using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The shared empty state of a pane (ui-revamp §2.8). <see cref="Draw"/> is the quiet form: a large veiled moon
/// centred in the remaining space with one line of guidance, drawn straight to the draw list. <see cref="DrawWithAction"/>
/// is the full form: the moon, a heading, a one-line explanation, the offending filters as chips (each clears only
/// itself) and one primary action, all real items so the keyboard reaches them.
/// </summary>
public static class EmptyState
{
    /// <summary><see cref="DrawWithAction"/>: nothing was clicked.</summary>
    public const int NothingClicked = -2;

    /// <summary><see cref="DrawWithAction"/>: the action button was clicked.</summary>
    public const int ActionClicked = -1;

    private const float MaxColumn = 360f;

    /// <summary>Draws the moon and <paramref name="guidance"/> centred in the current window's remaining region.</summary>
    public static void Draw(string guidance)
    {
        ArgumentNullException.ThrowIfNull(guidance);
        var dl = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail();
        var radius = UiMetrics.EmptyStateMoonRadius;
        var gap = UiMetrics.Px(14f);
        var wrap = PaneFit.Column(avail.X, UiMetrics.Px(80f), UiMetrics.Px(MaxColumn), UiMetrics.Px(16f));
        var textSize = ImGui.CalcTextSize(guidance, false, wrap);
        var blockHeight = radius * 2f + gap + textSize.Y;

        // A little above the geometric centre reads as centred.
        var top = origin.Y + MathF.Max(0f, (avail.Y - blockHeight) * 0.4f);
        var centerX = origin.X + avail.X * 0.5f;
        MoonGlyph.Draw(dl, new Vector2(centerX, top + radius), radius, QuestState.Unknown);
        dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), new Vector2(centerX - textSize.X * 0.5f, top + radius * 2f + gap), Theme.U32(Theme.Surface.TextSecondary), guidance, wrap);
    }

    /// <summary>
    /// The full empty state centred in the remaining region: a moon (<paramref name="moon"/>: the veiled moon for
    /// "nothing here yet", Blocked's new moon for "nothing matches"), <paramref name="heading"/> in the display role (1.2×) in the primary
    /// tone, <paramref name="body"/> wrapped in the secondary tone, one chip per <paramref name="chips"/> entry and a
    /// gold pill for <paramref name="action"/> (none when null). Returns <see cref="ActionClicked"/>, the index of the
    /// chip clicked, or <see cref="NothingClicked"/>.
    /// </summary>
    public static int DrawWithAction(string heading, string body, string? action, IReadOnlyList<string>? chips = null, QuestState moon = QuestState.Unknown)
    {
        ArgumentNullException.ThrowIfNull(heading);
        ArgumentNullException.ThrowIfNull(body);
        var dl = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail();
        var radius = UiMetrics.EmptyStateMoonRadius;
        var gap = UiMetrics.Px(12f);
        // The column keeps its floor only while the pane has room for it (feature plan v4 L6); the heading wraps in it.
        var column = PaneFit.Column(avail.X, UiMetrics.Px(120f), UiMetrics.Px(MaxColumn), UiMetrics.Px(16f));
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize();
        Vector2 headingSize;
        using (Typography.Display())
        {
            headingSize = ImGui.CalcTextSize(heading, false, column);
        }

        var bodySize = ImGui.CalcTextSize(body, false, column);
        var chipHeight = Chrome.ChipHeightPx();
        var chipRows = ChipRows(chips, column);
        var chipsHeight = chipRows == 0 ? 0f : (chipRows * chipHeight) + ((chipRows - 1) * UiMetrics.Px(6f)) + gap;
        var buttonHeight = UiMetrics.MinTarget;
        var blockHeight = (radius * 2f) + gap + headingSize.Y + UiMetrics.Px(4f) + bodySize.Y + gap + chipsHeight + (action is null ? 0f : buttonHeight);

        var top = origin.Y + MathF.Max(0f, (avail.Y - blockHeight) * 0.4f);
        var centerX = origin.X + (avail.X * 0.5f);
        var left = centerX - (column * 0.5f);
        MoonGlyph.Draw(dl, new Vector2(centerX, top + radius), radius, moon);
        var y = top + (radius * 2f) + gap;
        var s = Theme.Surface;
        using (Typography.Display())
        {
            // The display role's font is pushed (with its atlas texture) while the heading is drawn.
            dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), new Vector2(centerX - (headingSize.X * 0.5f), y), Theme.U32(s.Text), heading, column);
        }

        y += headingSize.Y + UiMetrics.Px(4f);
        dl.AddText(font, fontSize, new Vector2(centerX - (bodySize.X * 0.5f), y), Theme.U32(s.TextSecondary), body, column);
        y += bodySize.Y + gap;

        var result = NothingClicked;
        if (chips is { Count: > 0 })
        {
            var x = left;
            var spacing = UiMetrics.Px(6f);
            for (var i = 0; i < chips.Count; i++)
            {
                var width = Chrome.ChipWidth(chips[i]);
                if (x > left && x + width > left + column)
                {
                    x = left;
                    y += chipHeight + spacing;
                }

                ImGui.SetCursorScreenPos(new Vector2(x, y));
                ImGui.PushID(i);
                if (Chrome.Chip("##emptyChip", chips[i]))
                {
                    result = i;
                }

                ImGui.PopID();
                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(Strings.EmptyChipTooltip);
                }

                x += width + spacing;
            }

            y += chipHeight + gap;
        }

        if (action is not null)
        {
            var labelSize = ImGui.CalcTextSize(action);
            var size = new Vector2(labelSize.X + UiMetrics.Px(28f), buttonHeight);
            var min = new Vector2(centerX - (size.X * 0.5f), y);
            ImGui.SetCursorScreenPos(min);
            if (ImGui.InvisibleButton("##emptyAction", size))
            {
                result = ActionClicked;
            }

            var hovered = ImGui.IsItemHovered();
            var held = ImGui.IsItemActive();
            var rounding = size.Y * 0.5f;
            dl.AddRectFilled(min, min + size, Theme.WithAlpha(Theme.Moon, held ? 0.28f : hovered ? 0.22f : 0.16f), rounding);
            dl.AddRect(min, min + size, Theme.WithAlpha(Theme.Moon, 0.45f), rounding, ImDrawFlags.None, UiMetrics.Hairline);
            dl.AddText(min + ((size - labelSize) * 0.5f), Theme.AccentU32, action);
            Chrome.FocusRing(rounding);
        }

        return result;
    }

    /// <summary>
    /// Clears the filter the empty-result guard named <paramref name="name"/> (<see cref="EmptyReason.Filters"/>, the
    /// <see cref="FilterNames"/> labels); true when something changed. The search chip clears the search text; the
    /// "Include removed" and "Include other paths" chips turn their toggle on.
    /// </summary>
    public static bool ClearFilter(UiState ui, string name)
    {
        ArgumentNullException.ThrowIfNull(ui);
        var f = ui.Filters;
        switch (name)
        {
            case FilterNames.HideCompleted:
                f.HideCompleted = false;
                f.PerCategoryHideCompleted.Clear();
                return true;
            case FilterNames.AvailableOnly:
                f.AvailableOnly = false;
                f.PerCategoryAvailableOnly.Clear();
                return true;
            case FilterNames.State:
                f.StateMask = QuestStateMask.All;
                return true;
            case FilterNames.Expansion:
                f.Expansions.Clear();
                return true;
            case FilterNames.AddedIn:
                f.AddedIn = string.Empty;
                return true;
            case FilterNames.LevelRange:
                f.LevelMin = FilterSet.NoLevelMin;
                f.LevelMax = FilterSet.NoLevelMax;
                return true;
            case FilterNames.JobCategory:
                f.ClassJobCategoryId = null;
                return true;
            case FilterNames.RewardKinds:
                f.RewardKinds.Clear();
                return true;
            case FilterNames.Repeatable:
                f.RepeatableOnly = false;
                return true;
            case FilterNames.SeasonalActive:
                f.SeasonalActiveOnly = false;
                return true;
            // The two widening toggles are named while off; their chip turns them on.
            case FilterNames.IncludeUnlisted:
                f.IncludeUnlisted = true;
                return true;
            case FilterNames.IncludeOtherPaths:
                f.IncludeOtherPaths = true;
                return true;
            case FilterNames.Pinned:
                f.PinnedOnly = false;
                return true;
            case FilterNames.Abandoned:
                f.AbandonedOnly = false;
                return true;
            case FilterNames.Search:
                ui.SearchText = string.Empty;
                return true;
            default:
                if (f.Preset != Preset.None && name == FilterNames.PresetName(f.Preset))
                {
                    f.Preset = Preset.None;
                    return true;
                }

                return false;
        }
    }


    private static int ChipRows(IReadOnlyList<string>? chips, float column)
    {
        if (chips is not { Count: > 0 })
        {
            return 0;
        }

        var rows = 1;
        var x = 0f;
        var spacing = UiMetrics.Px(6f);
        for (var i = 0; i < chips.Count; i++)
        {
            var width = Chrome.ChipWidth(chips[i]);
            if (x > 0f && x + width > column)
            {
                rows++;
                x = 0f;
            }

            x += width + spacing;
        }

        return rows;
    }
}
