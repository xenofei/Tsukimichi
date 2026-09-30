using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Ui;

/// <summary>
/// The blown-up reward tooltip shared by the table's reward icons and the detail pane's reward rows: a large icon,
/// the name in bold, the kind and count, then what the game sheets know (item level and category plus the item
/// description for items; the description for emotes and actions). Sheet text is read once per reward through
/// <see cref="GameLinks"/>; the kind line is cached per (kind, count) so hovering allocates nothing after the first time.
/// </summary>
public static class RewardTooltip
{
    public const float WrapWidthEm = 26f;

    private static readonly Dictionary<(RewardKind Kind, uint Count), string> KindLines = [];

    private static readonly Dictionary<string, string> DropLines = new(StringComparer.Ordinal);

    /// <summary>
    /// Draws the tooltip; call only while the reward's item is hovered. <paramref name="source"/>, when given, closes
    /// the tooltip as a disabled line (Moonlit passes where its unique verdict came from).
    /// </summary>
    public static void Draw(RewardRef reward, GameLinks links, ITextureProvider textures, string? source = null)
    {
        ArgumentNullException.ThrowIfNull(reward);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(textures);

        using var tooltip = ImRaii.Tooltip();
        // A tooltip has no parent window, so it applies the font scale itself.
        UiMetrics.ApplyFontScale();
        var scale = UiMetrics.Scale;
        var iconSize = UiMetrics.TooltipIconSize;
        if (reward.Icon != 0)
        {
            var wrap = textures.GetFromGameIcon(new GameIconLookup(reward.Icon)).GetWrapOrEmpty();
            ImGui.Image(wrap.Handle, new Vector2(iconSize, iconSize));
            ImGui.SameLine();
        }

        using var group = ImRaii.Group();
        using var wrapPos = ImRaii.TextWrapPos(ImGui.GetCursorPosX() + ImGui.GetFontSize() * WrapWidthEm);
        BoldText(reward.Name, scale);
        ImGui.TextDisabled(KindLine(reward));
        if (links.IsStoreResell?.Invoke(reward) == true)
        {
            // Sold on the FFXIV Online Store as well (curated/online_store.json), so not exclusive to the quest.
            using (Theme.PushText(Theme.Dusk))
            {
                ImGui.TextUnformatted(Strings.MoonlitStoreOnly);
                ImGui.SameLine();
                ImGui.TextUnformatted(Strings.MoonlitStoreOnlyTooltipLine);
            }
        }

        if (links.DropWhere?.Invoke(reward) is { } where)
        {
            // A duty drops it too (curated/other_sources.json): the same "Also drops in …" line as the item hover hint.
            using (Theme.PushText(Theme.Dusk))
            {
                ImGui.TextUnformatted(DropLine(where));
            }
        }

        if (reward.ItemId != 0)
        {
            if (links.Item(reward.ItemId) is { } item)
            {
                if (item.Summary.Length > 0)
                {
                    ImGui.TextDisabled(item.Summary);
                }

                if (item.Description.Length > 0)
                {
                    ImGui.Spacing();
                    ImGui.TextWrapped(item.Description);
                }
            }
        }
        else
        {
            var description = links.RewardDescription(reward);
            if (description.Length > 0)
            {
                ImGui.Spacing();
                ImGui.TextWrapped(description);
            }
        }

        if (!string.IsNullOrEmpty(source))
        {
            ImGui.Spacing();
            ImGui.TextDisabled(source);
        }
    }

    /// <summary>"Also drops in …", cached per duty text (a handful of distinct values) so hovering allocates nothing after the first time.</summary>
    private static string DropLine(string where)
    {
        if (!DropLines.TryGetValue(where, out var line))
        {
            line = Strings.AlsoDropsLine(where);
            DropLines[where] = line;
        }

        return line;
    }

    /// <summary>"Kind" or "Kind ×N", cached per (kind, count).</summary>
    private static string KindLine(RewardRef reward)
    {
        var key = (reward.Kind, reward.Count);
        if (!KindLines.TryGetValue(key, out var line))
        {
            var kind = Strings.RewardKindName(reward.Kind);
            line = reward.Count > 1 ? string.Format(CultureInfo.CurrentCulture, Strings.RewardCountFormat, kind, reward.Count) : kind;
            KindLines[key] = line;
        }

        return line;
    }

    /// <summary>Dalamud ships no bold face, so the name is drawn twice, one scaled pixel apart.</summary>
    private static void BoldText(string text, float scale)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.TextWrapped(text);
        var wrapWidth = ImGui.GetFontSize() * WrapWidthEm;
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), ImGui.GetFontSize(), pos + new Vector2(MathF.Max(1f, MathF.Round(scale)), 0f), ImGui.GetColorU32(ImGuiCol.Text), text, wrapWidth);
    }
}
