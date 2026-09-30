using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The Journal tree as a strip of icons (feature plan v4 L1/L3, design v4 §8.2 "Icon strip"): what the tree becomes
/// when its handle is dragged under <see cref="PaneLayout.StripSnapLogical"/>. One row per top-level node the full
/// tree would show (All quests, each section, Unlock quests, and Removed from the game and Other paths when the tree
/// lists them), each its node glyph (<see cref="DrawNodeGlyph"/>, where the orbit icon will plug in) with the gold
/// Ready dot when something under it is Ready. Hovering a row names the node in full with its progress and Ready
/// count; a click selects it, and the rows are real items, so keyboard navigation reaches them. The row holding the
/// selected scope carries the selection's wash and gold rule. A reveal made while the strip shows is left pending for
/// the full tree, which opens and scrolls to it when the pane is widened again. Nothing is allocated: the names and
/// counts are the nodes' own strings.
/// </summary>
public sealed partial class TreePane
{
    /// <summary>Draws the strip in the current (left pane) window.</summary>
    public void DrawStrip(CatalogBundle current, QueryRunner runner, bool showUnlisted)
    {
        EnsureNodes(current);
        RefreshCounts(runner);
        revealing = false;

        lineHeight = ImGui.GetTextLineHeight();
        glyphRadius = UiMetrics.TreeGlyphRadius(lineHeight);
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = UiMetrics.TreeRowHeight(lineHeight);

        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(ImGui.GetStyle().ItemSpacing.X, 0f)))
        {
            DrawStripNode(allNode, width, rowHeight);
            foreach (var section in sections)
            {
                DrawStripNode(section, width, rowHeight);
            }

            DrawStripNode(featureNode, width, rowHeight);
            if (showUnlisted || ui.Scope == QuestScope.VirtualUnlisted)
            {
                DrawStripNode(unlistedNode, width, rowHeight);
            }

            // As in the full tree: quests on paths the character did not take, while there are any (or it is selected).
            if (otherPathsTotal > 0 || ui.Scope == QuestScope.VirtualOtherPaths)
            {
                DrawStripNode(otherPathsNode, width, rowHeight);
            }
        }

        ui.RecordSpan(UiRects.Tree, start, width);
    }

    private void DrawStripNode(Node node, float width, float rowHeight)
    {
        var clicked = ImGui.InvisibleButton(node.Id, new Vector2(MathF.Max(1f, width), rowHeight));
        var hovered = ImGui.IsItemHovered();
        if (clicked)
        {
            Select(node.Scope);
        }

        if (!ImGui.IsItemVisible())
        {
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var selected = ui.Scope == node.Scope || (!node.Leaf && Contains(node, ui.Scope));
        if (selected || hovered)
        {
            dl.AddRectFilled(min, max, ImGui.GetColorU32(selected ? (hovered ? SelectedHoverWash : SelectedWash) : HoverWash));
        }

        if (selected)
        {
            dl.AddRectFilled(min, new Vector2(min.X + MathF.Max(2f, MathF.Round(UiMetrics.Px(2f))), max.Y), Theme.MoonU32);
        }

        var center = new Vector2(MathF.Round((min.X + max.X) * 0.5f), MathF.Round((min.Y + max.Y) * 0.5f));
        DrawNodeGlyph(dl, node, center, glyphRadius, node.Count.Fraction, readyDot: node.Ready > 0);
        Chrome.FocusRing();

        if (hovered)
        {
            DrawStripTooltip(node);
        }
    }

    /// <summary>A strip row's hover: the node's full name, its progress (with the other-path tally), and its Ready count.</summary>
    private static void DrawStripTooltip(Node node)
    {
        using var tooltipStyle = Theme.PushTooltip();
        using var tooltip = ImRaii.Tooltip();
        UiMetrics.ApplyFontScale();
        ImGui.TextUnformatted(node.FullName);
        var progress = node.HoverText.Length > 0 ? node.HoverText : node.ProgressText;
        if (progress.Length > 0)
        {
            ImGui.TextDisabled(progress);
        }

        if (node.Ready > 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.Moon))
            {
                ImGui.TextUnformatted(node.ReadyTooltip);
            }
        }
    }
}
