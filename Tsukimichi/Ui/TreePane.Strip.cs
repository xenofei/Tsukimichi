using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The Journal tree as a strip of icons (feature plan v4 L1): what the tree becomes when its handle is dragged under
/// <see cref="PaneLayout.StripSnapLogical"/>. A placeholder until the tree's own narrow tiers arrive (L3, design v4
/// §8.2): one halo per top-level node (All quests, each section, the Unlock quests and Removed from the game nodes),
/// the name and count on hover, a click selecting the node. The section holding the selected scope carries the
/// selection's gold rule. Nothing is allocated: the names and counts are the nodes' own strings.
/// </summary>
public sealed partial class TreePane
{
    /// <summary>Draws the strip in the current (left pane) window.</summary>
    public void DrawStrip(CatalogBundle current, QueryRunner runner, bool showUnlisted)
    {
        EnsureNodes(current);
        RefreshCounts(runner);
        // The strip has no rows to open or scroll to: a pending reveal is left for the full tree, which opens the
        // node's ancestors and scrolls to it once the pane is wide again (a scope change drops it meanwhile).
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
        MoonGlyph.DrawHalo(dl, center, glyphRadius, node.Count.Fraction, onCard: false, dimComplete: node.Complete);
        if (node.Ready > 0)
        {
            // Something is Ready under this node: a small gold dot on the ring's upper right.
            var dot = center + (new Vector2(0.7071f, -0.7071f) * glyphRadius);
            dl.AddCircleFilled(dot, MathF.Max(2.5f, UiMetrics.Px(3f)), Theme.MoonU32);
        }

        if (hovered)
        {
            UiMetrics.Tooltip(node.Name, node.ProgressText);
        }
    }
}
