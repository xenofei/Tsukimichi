using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Companions;

namespace Tsukimichi.Ui;

/// <summary>
/// The badges of "How you'll clear it" (feature plan v7 C7; spec-1.19 C7, "Badges"): the 1.14 chip, 20 px tall, with
/// a 14 px game icon when the badge has one, the label in Text (Story-required and Optional in Secondary) and the
/// palette's Line as the outline (1.5 px under high contrast). Each explains itself on hover. Worn in the detail pane's
/// section, the Duties board's rows, My blues, the Route window's steps and the Duty Finder hint
/// (<see cref="DutyBadgeRules"/> picks them per <see cref="DutyBadgeSurface"/>; <see cref="ClearBadgeSource"/> builds
/// them for the last three). Draw thread only.
/// </summary>
public static class DutyBadges
{
    /// <summary>Duty Support's icon (000089), for Solo with NPCs.</summary>
    public const uint DutySupportIcon = 89;

    /// <summary>The Duty Finder's icon (000046), for Group of 4 and 8.</summary>
    public const uint DutyFinderIcon = 46;

    /// <summary>The alliance icon (000017), for Group of 24.</summary>
    public const uint AllianceIcon = 17;

    private const float HeightLogical = 20f;
    private const float IconLogical = 14f;
    private const float PadLogical = 7f;
    private const float GapLogical = 5f;

    /// <summary>A badge as drawn: its label, icon (0 for none), whether it is in Secondary, and its hover.</summary>
    public readonly record struct Look(string Label, uint Icon, bool Secondary, string Tooltip);

    /// <summary>
    /// How <paramref name="badge"/> reads for <paramref name="duty"/>. A group badge's hover names the roulettes that
    /// draw from the duty (<paramref name="roulettes"/>, short names), or says the Duty Finder does not match it.
    /// </summary>
    public static Look Describe(DutyBadge badge, DutyRunInfo duty, IReadOnlyList<RouletteInfo> roulettes)
    {
        ArgumentNullException.ThrowIfNull(duty);
        ArgumentNullException.ThrowIfNull(roulettes);
        return badge.Kind switch
        {
            DutyBadgeKind.SoloWithNpcs => new Look(Strings.DutyBadgeSoloWithNpcs, DutySupportIcon, false, Strings.DutyBadgeSoloWithNpcsTip),
            DutyBadgeKind.Solo => new Look(Strings.DutyBadgeSolo, 0, false, Strings.DutyBadgeSoloTip),
            DutyBadgeKind.Group => new Look(
                string.Format(CultureInfo.CurrentCulture, Strings.DutyBadgeGroupFormat, badge.Players),
                badge.Players >= 24 ? AllianceIcon : DutyFinderIcon,
                false,
                GroupTooltip(duty, roulettes)),
            DutyBadgeKind.HighEnd => new Look(Strings.DutyBadgeHighEnd, 0, false, Strings.DutyBadgeHighEndTip),
            DutyBadgeKind.StoryRequired => new Look(Strings.DutyBadgeStoryRequired, 0, true, Strings.DutyBadgeStoryRequiredTip),
            _ => new Look(Strings.DutyBadgeOptional, 0, true, Strings.DutyBadgeOptionalTip),
        };
    }

    private static string GroupTooltip(DutyRunInfo duty, IReadOnlyList<RouletteInfo> roulettes)
    {
        if (!duty.InDutyFinder)
        {
            return Strings.DutyBadgeGroupPartyTip;
        }

        var names = new List<string>();
        foreach (var roulette in roulettes)
        {
            if ((duty.Roulettes & roulette.Flag) != 0)
            {
                names.Add(roulette.ShortName);
            }
        }

        return names.Count == 0
            ? Strings.DutyBadgeGroupTip
            : Strings.DutyBadgeGroupTip + "\n" + string.Format(CultureInfo.CurrentCulture, Strings.DutyBadgeGroupRoulettesFormat, string.Join(Strings.PlanningListSeparator, names));
    }

    /// <summary>The badge's width at the current scale.</summary>
    public static float Width(in Look look)
    {
        using var caption = Typography.Caption();
        var icon = look.Icon != 0 ? UiMetrics.Px(IconLogical) + UiMetrics.Px(GapLogical) : 0f;
        return (2f * UiMetrics.Px(PadLogical)) + icon + ImGui.CalcTextSize(look.Label).X;
    }

    /// <summary>The badge's height at the current scale.</summary>
    public static float Height => MathF.Round(UiMetrics.Px(HeightLogical));

    /// <summary>The gap between two badges in a run, and between a run and the text before it.</summary>
    public static float RunGap => UiMetrics.Px(4f);

    /// <summary>The width of <paramref name="looks"/> side by side, <see cref="RunGap"/> between them; 0 for none.</summary>
    public static float RunWidth(IReadOnlyList<Look> looks)
    {
        ArgumentNullException.ThrowIfNull(looks);
        var width = 0f;
        for (var i = 0; i < looks.Count; i++)
        {
            width += Width(looks[i]) + (i > 0 ? RunGap : 0f);
        }

        return width;
    }

    /// <summary>
    /// <paramref name="looks"/> left to right from <paramref name="x"/>, centred on a line <paramref name="lineHeight"/>
    /// tall from <paramref name="top"/>, each only while it ends by <paramref name="right"/> (the first that does not
    /// fit ends the run, so a row never shows a later badge without an earlier one). Returns the x after the last drawn,
    /// <paramref name="x"/> when none fits. The cursor is left where it was.
    /// </summary>
    public static float DrawRun(IReadOnlyList<Look> looks, float x, float top, float lineHeight, float right, ITextureProvider? textures)
    {
        ArgumentNullException.ThrowIfNull(looks);
        var cursor = ImGui.GetCursorScreenPos();
        var y = MathF.Round(top + ((lineHeight - Height) * 0.5f));
        var end = x;
        for (var i = 0; i < looks.Count; i++)
        {
            var left = end + (i > 0 ? RunGap : 0f);
            if (left + Width(looks[i]) > right + 0.5f)
            {
                break;
            }

            ImGui.SetCursorScreenPos(new Vector2(left, y));
            Draw(looks[i], textures);
            end = ImGui.GetItemRectMax().X;
        }

        ImGui.SetCursorScreenPos(cursor);
        return end;
    }

    /// <summary>The badge as an item at the cursor; its hover shows <see cref="Look.Tooltip"/>.</summary>
    public static void Draw(in Look look, ITextureProvider? textures)
    {
        using var caption = Typography.Caption();
        var height = MathF.Round(UiMetrics.Px(HeightLogical));
        var size = new Vector2(Width(look), height);
        var min = ImGui.GetCursorScreenPos();
        ImGui.Dummy(size);
        var dl = ImGui.GetWindowDrawList();
        var max = min + size;
        var rounding = height * 0.5f;
        dl.AddRectFilled(min, max, Theme.U32(Theme.Surface.Sunken), rounding);
        dl.AddRect(min, max, Theme.U32(Theme.Surface.Line), rounding, ImDrawFlags.None, Theme.Glyphs.HighContrast ? UiMetrics.Px(1.5f) : UiMetrics.Hairline);
        var x = min.X + UiMetrics.Px(PadLogical);
        if (look.Icon != 0 && textures is not null)
        {
            var iconSize = MathF.Round(UiMetrics.Px(IconLogical));
            var iconMin = new Vector2(x, MathF.Round(min.Y + ((height - iconSize) * 0.5f)));
            GameIcon.DrawAt(dl, textures, look.Icon, iconMin, iconMin + new Vector2(iconSize), UiMetrics.Px(2f));
        }

        if (look.Icon != 0)
        {
            x += UiMetrics.Px(IconLogical) + UiMetrics.Px(GapLogical);
        }

        var textY = min.Y + ((height - ImGui.GetTextLineHeight()) * 0.5f);
        dl.AddText(new Vector2(x, textY), Theme.U32(look.Secondary ? Theme.Surface.TextSecondary : Theme.Surface.Text), look.Label);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(look.Tooltip);
        }
    }
}
