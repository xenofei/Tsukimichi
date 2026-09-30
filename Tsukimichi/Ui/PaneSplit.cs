using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Config;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>Which side pane a <see cref="PaneSplit"/> handle resizes.</summary>
public enum PaneSide
{
    /// <summary>The handle between the tree (the left pane) and the centre.</summary>
    Tree,

    /// <summary>The handle between the centre and the detail pane.</summary>
    Detail,
}

/// <summary>
/// The main window's pane splitter (feature plan v4 L1, UI audit §4), replacing the resizable body table whose panes
/// could be dragged down to a few pixels and whose widths lived in imgui.ini with no way back. The widths come from
/// <see cref="PaneLayout.Solve"/> (Core, tested): every pane keeps its floor, the side panes keep the width the user
/// dragged them to in logical units (<see cref="Configuration.TreePaneWidth"/>, <see cref="Configuration.DetailPaneWidth"/>),
/// and the Journal tree dragged under its snap closes to a strip of icons. Each handle is an invisible button filling
/// the 6 px gutter between two panes, with a 1 px line down its middle that lights on hover, the east-west resize
/// cursor and a double-click that puts the pane back to its default width. The drag's state is one handle at a time
/// and nothing is allocated.
/// </summary>
public sealed class PaneSplit
{
    private static readonly uint HoverLine = Theme.WithAlpha(Theme.Moon, 0.6f);

    /// <summary>How long the mouse rests on a handle before its hint shows.</summary>
    private const double TooltipDelaySeconds = 0.6;

    private PaneSide? dragging;
    private float dragStartLogical;
    private PaneSide? hoverSide;
    private double hoverSince;

    /// <summary>
    /// This frame's pane widths for a body <paramref name="total"/> pixels wide beside a rail <paramref name="rail"/>
    /// pixels wide. The tree is the strip only where <paramref name="stripAllowed"/> (the Journal tab without the filter
    /// panel; the other tabs' lists have no strip yet) and the user closed it.
    /// </summary>
    public static PaneWidths Solve(Configuration settings, float total, float rail, bool stripAllowed)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return PaneLayout.Solve(total, rail, settings.TreePaneWidth, settings.DetailPaneWidth, UiMetrics.Scale, stripAllowed && settings.TreePaneStrip);
    }

    /// <summary>A plain gutter with its line (between the rail and the tree): nothing to drag.</summary>
    public static void Divider(string id, in PaneWidths widths, float height)
    {
        ImGui.InvisibleButton(id, new Vector2(MathF.Max(1f, widths.Gutter), MathF.Max(1f, height)));
        DrawLine(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), Theme.U32(Theme.Surface.Line));
    }

    /// <summary>
    /// The drag handle for <paramref name="side"/>, drawn as the next item (a gutter <see cref="PaneWidths.Gutter"/>
    /// wide). Dragging writes the pane's logical width to <paramref name="settings"/>; the new widths apply from the
    /// next frame. Returns true when the settings changed in a way worth saving: a drag ended or a double-click reset.
    /// </summary>
    /// <param name="side">The pane the handle resizes.</param>
    /// <param name="settings">The configuration holding the widths.</param>
    /// <param name="widths">This frame's widths.</param>
    /// <param name="height">The body's height.</param>
    /// <param name="total">The body's width, as given to <see cref="Solve"/>.</param>
    /// <param name="stripAllowed">Whether the tree may close to its strip on this tab.</param>
    public bool Handle(PaneSide side, Configuration settings, in PaneWidths widths, float height, float total, bool stripAllowed)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ImGui.InvisibleButton(side == PaneSide.Tree ? "##splitTree" : "##splitDetail", new Vector2(MathF.Max(1f, widths.Gutter), MathF.Max(1f, height)));
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        var scale = UiMetrics.Scale;
        var changed = false;

        if (hovered || active)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
        }

        if (ImGui.IsItemActivated())
        {
            dragging = side;
            dragStartLogical = side == PaneSide.Tree
                ? (widths.TreeStrip ? PaneLayout.TreeStripLogical : widths.Tree / scale)
                : widths.Detail / scale;
        }

        if (active && dragging == side)
        {
            var delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 0f).X;
            if (side == PaneSide.Tree)
            {
                var wanted = PaneLayout.Dragged(dragStartLogical, delta, scale);
                if (stripAllowed)
                {
                    settings.TreePaneStrip = PaneLayout.Strip(wanted, settings.TreePaneStrip);
                }

                if (!(stripAllowed && settings.TreePaneStrip))
                {
                    settings.TreePaneWidth = PaneLayout.SanitizeTree(wanted);
                }
            }
            else
            {
                // The detail pane's handle is on its left: moving left widens it.
                settings.DetailPaneWidth = PaneLayout.SanitizeDetail(PaneLayout.Dragged(dragStartLogical, -delta, scale));
            }
        }

        if (ImGui.IsItemDeactivated() && dragging == side)
        {
            // Keep what the window could give, not how far the mouse went: a pane dragged past the room it has would
            // otherwise balloon the next time the window grows.
            var solved = PaneLayout.Solve(total, widths.Rail, settings.TreePaneWidth, settings.DetailPaneWidth, scale, stripAllowed && settings.TreePaneStrip);
            if (side == PaneSide.Tree && !solved.TreeStrip)
            {
                settings.TreePaneWidth = PaneLayout.SanitizeTree(MathF.Round(solved.Tree / scale));
            }
            else if (side == PaneSide.Detail)
            {
                settings.DetailPaneWidth = PaneLayout.SanitizeDetail(MathF.Round(solved.Detail / scale));
            }

            dragging = null;
            changed = true;
        }

        if (hovered && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            if (side == PaneSide.Tree)
            {
                settings.TreePaneWidth = PaneLayout.TreeDefaultLogical;
                settings.TreePaneStrip = false;
            }
            else
            {
                settings.DetailPaneWidth = PaneLayout.DetailDefaultLogical;
            }

            // The second click also began a drag; ending it here keeps a held button from undoing the reset.
            dragging = null;
            changed = true;
        }

        DrawLine(min, max, hovered || active ? HoverLine : Theme.U32(Theme.Surface.Line));

        // The hint waits for a resting mouse, so a drag that merely crosses the handle does not flash it.
        if (!hovered || active)
        {
            if (hoverSide == side)
            {
                hoverSide = null;
            }
        }
        else if (hoverSide != side)
        {
            hoverSide = side;
            hoverSince = ImGui.GetTime();
        }
        else if (ImGui.GetTime() - hoverSince >= TooltipDelaySeconds)
        {
            UiMetrics.Tooltip(Strings.PaneDividerTooltip);
        }

        return changed;
    }

    /// <summary>The gutter's 1 px line, down its middle.</summary>
    private static void DrawLine(Vector2 min, Vector2 max, uint color)
    {
        var x = MathF.Floor((min.X + max.X) * 0.5f) + 0.5f;
        ImGui.GetWindowDrawList().AddLine(new Vector2(x, min.Y), new Vector2(x, max.Y), color, 1f);
    }
}
