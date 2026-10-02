using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The Journal tree: All quests, then Section → Category → Genre, then the Unlock quests, Removed from the game and
/// Other paths virtual nodes (Other paths only while the viewed character has quests on paths not taken).
/// Each node shows a halo gauge, its name, an expansion pill when every quest under it belongs to one expansion, a
/// Ready badge when any quest under it can be accepted now, and "done / total" in Dusk with a 44 × 3 mini bar
/// (glyph proposal §4, T11). Selecting a node scopes the table through <see cref="UiState.Scope"/>.
/// A category with a single genre is folded into one leaf (the category's name, the genre's scope and counts), and a
/// section whose only category folded likewise becomes a single leaf, so no node ever expands to just one child.
/// The node list is built once per catalog; count labels are re-materialized only when the counts instance changes.
/// <para>
/// The Moon Road look (feature plan v4 V2) under Full and Quiet flair: each node's glyph is an orbit round its official
/// icon or gap glyph (<see cref="DrawNodeGlyph"/>, icons from the catalog's <see cref="NodeIconMap"/>), the road under
/// each row replaces the mini bar and the section rules, a header line names the Journal with the overall count, and
/// moon-road dividers separate the story, side and virtual blocks (<c>TreePane.Art.cs</c>). Plain flair keeps the 1.3
/// tree.
/// </para>
/// <para>
/// Rows read the short names of <see cref="JournalNames.Short"/> ("Eden", "Hildibrand", "Main Scenario" with an
/// ARR–EW pill), and a row whose name is shortened or cut names the node in full, with its journal path, on hover.
/// Each row is fitted by <see cref="TreeRowFit"/> within the tree's <see cref="TreeTier"/> (feature plan v4 L3): the
/// expansion pill goes first, then the mini bar, then the count becomes a percentage, and in the narrowest tier the
/// ring alone carries progress and the Ready pill becomes a gold dot on the glyph, so nothing ever overlaps. A
/// section's expansion suffix is never cut: without its pill the label carries it whole ("Main Sc… · DT"). Texts
/// are measured once per font size, so a row does no measuring per frame.
/// </para>
///
/// Rows are <c>TreeNodeEx</c> items (keyboard navigation, open-on-arrow, the reveal logic) with
/// <see cref="ImGuiTreeNodeFlags.FramePadding"/> and a pushed vertical frame padding so each row is
/// <see cref="UiMetrics.TreeRowHeight"/> tall (at least 30 px), and <see cref="ImGuiTreeNodeFlags.SpanFullWidth"/> so the
/// hover and selection washes cross the whole row. Everything else is painted over the item without new items.
/// </summary>
public sealed partial class TreePane
{
    /// <summary>A label ImGui renders as nothing (text after "##" is hidden) but that is still a real, terminated string.</summary>
    private const string HiddenLabel = "##";

    /// <summary>No expansion pill: the node's quests span several expansions (or it has none).</summary>
    private const int MixedExpansion = -1;

    private sealed class Node(QuestScope scope, string id, string name, bool leaf)
    {
        public QuestScope Scope { get; } = scope;
        public string Id { get; } = id;

        /// <summary>
        /// The row's label: the short name (<see cref="JournalNames.Short"/>) without an expansion suffix the row draws
        /// as a pill (<see cref="Suffix"/>); the virtual nodes take theirs again after a language switch.
        /// </summary>
        public string Name { get; set; } = name;

        /// <summary>The game's full name, for the tooltip; the virtual nodes' label.</summary>
        public string FullName { get; set; } = name;

        /// <summary>
        /// The expansion suffix as the label carries it where the pill does not show (" · DT", after <see cref="Name"/>);
        /// empty when it has none. It is never cut: the name before it is ellipsised instead (<see cref="TreeRowFit"/>).
        /// </summary>
        public string SuffixText { get; set; } = string.Empty;

        /// <summary>The short name's expansion suffix ("ARR–EW"), drawn as the row's pill; empty when it has none.</summary>
        public string Suffix { get; set; } = string.Empty;

        /// <summary>Whether the label is not the full name, so hovering the row names it in full.</summary>
        public bool Shortened { get; set; }

        /// <summary>The journal path down to the node ("Sidequests › Hildibrand Sidequests"), for the tooltip; empty for the virtual nodes.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// The node's identity icon for the orbit (design v4 §6.2-6.3): an official game icon or a gap glyph from
        /// <see cref="NodeIconMap"/>, applied once per catalog; empty draws the moon halo. Read only by
        /// <see cref="DrawNodeGlyph"/>.
        /// </summary>
        public NodeIcon Icon { get; set; }

        /// <summary>The tree block a top-level node belongs to, for the moon-road dividers between blocks.</summary>
        public JournalBlock Block { get; set; } = JournalBlock.Virtual;

        public bool Leaf { get; } = leaf;
        public List<Node> Children { get; } = [];

        /// <summary>Full journal path of a folded node, shown on hover; null for ordinary nodes.</summary>
        public string? FoldedPath { get; set; }

        /// <summary>The count is a number of quests rather than progress (the Other paths node): no percentage form.</summary>
        public bool CountIsTally { get; set; }

        /// <summary>The completion as "82 %", the count's form in the Compact tier.</summary>
        public string PercentText { get; set; } = string.Empty;

        // Measured widths, kept for the font size they were measured at; any text change resets MeasuredAt.
        public float MeasuredAt { get; set; } = -1f;
        public float NameWidth { get; set; }
        public float SuffixWidth { get; set; }
        public float CountWidth { get; set; }
        public float PercentWidth { get; set; }
        public float ReadyWidth { get; set; }
        public float PillWidth { get; set; }

        /// <summary>The pill after the name: the short name's expansion suffix, else the single expansion of its quests.</summary>
        public string PillText => Suffix.Length > 0 ? Suffix : ExpansionText;

        /// <summary>Lowest expansion among the node's quests; Sprout mode folds a node whose lowest lies beyond the story.</summary>
        public byte MinExpansion { get; set; } = byte.MaxValue;

        /// <summary>The single expansion of every quest under the node, or <see cref="MixedExpansion"/>.</summary>
        public int Expansion { get; set; } = MixedExpansion;

        /// <summary>The expansion pill's text, empty when the node spans several expansions.</summary>
        public string ExpansionText { get; set; } = string.Empty;

        public NodeCount Count { get; set; }
        public string CountText { get; set; } = string.Empty;

        /// <summary>The halo's hover text: done/total and the percent, rebuilt with <see cref="CountText"/>.</summary>
        public string ProgressText { get; set; } = string.Empty;

        /// <summary>"53 on other paths: another city's start 49, …" under the node; empty when none.</summary>
        public string OtherPathsText { get; set; } = string.Empty;

        /// <summary>The halo tooltip's second line: <see cref="ProgressText"/>, then <see cref="OtherPathsText"/> when there is one.</summary>
        public string HoverText { get; set; } = string.Empty;

        /// <summary>The exact completion to two decimals, the folded-path tooltip's header.</summary>
        public string FractionText { get; set; } = string.Empty;

        public int Ready { get; set; }
        public string ReadyText { get; set; } = string.Empty;
        public string ReadyTooltip { get; set; } = string.Empty;

        public bool Complete => Count.Total > 0 && Count.Done >= Count.Total;
    }

    /// <summary>Which part of a row the mouse is over, for the tooltip.</summary>
    private enum Hover
    {
        None,
        Halo,
        Progress,
        Ready,
    }

    // Row washes (ui-revamp §2.3, glyph proposal §4): hover Silver 5 %, selected Veil 22 %, both a touch stronger while held.
    private static readonly Vector4 HoverWash = Theme.WithAlphaVector(Theme.Silver, 0.05f);
    private static readonly Vector4 SelectedWash = Theme.WithAlphaVector(Theme.Veil, 0.22f);
    private static readonly Vector4 SelectedHoverWash = Theme.WithAlphaVector(Theme.Veil, 0.30f);
    private static readonly Vector4 ActiveWash = Theme.WithAlphaVector(Theme.Silver, 0.09f);
    private static readonly uint ReadyBadgeFill = Theme.WithAlpha(Theme.Moon, 0.16f);

    /// <summary>Logical sizes: the mini bar (44 × 3, 12 before the count) and the pill paddings.</summary>
    private const float BarWidthLogical = 44f;
    private const float BarHeightLogical = 3f;
    private const float BarGapLogical = 12f;
    private const float PillPadLogical = 5f;
    private const float PillFontFraction = 0.72f;

    /// <summary>The tree's width tier, kept from frame to frame for its hysteresis.</summary>
    private TreeTier tier = TreeTier.Full;

    // Motion keys on a node's ImGui id (T17): the chevron's turn, the halo's fill, the reveal pulse.
    private const uint ChevronTag = 0x5452_4543; // "TREC"
    private const uint GaugeTag = 0x5452_4547;   // "TREG"
    private const uint RevealTag = 0x5452_4552;  // "TRER"

    private readonly UiState ui;
    private readonly ITextureProvider textures;
    private readonly Func<NodeIconMap> nodeIcons;

    /// <summary>The icon map last applied to the nodes; null after the nodes are rebuilt, so the next frame applies it again.</summary>
    private NodeIconMap? appliedIcons;

    private CatalogBundle? bundle;
    private readonly List<Node> sections = [];
    private readonly Node allNode = new(QuestScope.None, "##all", Strings.AllQuests, leaf: true);
    private readonly Node featureNode = new(QuestScope.VirtualFeature, "##feature", Strings.FeatureUnlocks, leaf: true);
    private readonly Node unlistedNode = new(QuestScope.VirtualUnlisted, "##unlisted", Strings.RemovedFromGame, leaf: true);
    private readonly Node otherPathsNode = new(QuestScope.VirtualOtherPaths, "##otherpaths", Strings.OtherPaths, leaf: true);
    private int otherPathsTotal;
    private TreeCounts? counts;
    private NodeCount featureCount;
    private int featureReady = -1;
    private bool revealing;

    /// <summary>Sprout mode's reach this frame: nodes wholly beyond it fold to their counts; null when Sprout mode is off.</summary>
    private byte? sproutReach;

    /// <summary>The scope and reach last checked against Sprout's folds, so the check runs only when either changes.</summary>
    private QuestScope sproutCheckedScope = QuestScope.None;
    private byte? sproutCheckedReach;

    // Per-frame row geometry, set once in Draw.
    private float lineHeight;
    private float rowPadY;
    private float glyphRadius;

    /// <param name="ui">The shared UI state.</param>
    /// <param name="textures">The texture provider the orbits read the game icons through.</param>
    /// <param name="nodeIcons">The current catalog's node icons (<see cref="Game.SessionState.NodeIcons"/>), resolved once per catalog.</param>
    public TreePane(UiState ui, ITextureProvider textures, Func<NodeIconMap> nodeIcons)
    {
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        this.nodeIcons = nodeIcons ?? throw new ArgumentNullException(nameof(nodeIcons));
    }

    public void Draw(CatalogBundle current, QueryRunner runner, bool showUnlisted)
    {
        // A scrollbar drag, a keyboard scroll or a reveal jump pauses motion before any row draws (as the wheel does).
        Motion.WatchScroll();
        EnsureNodes(current);
        ApplyIcons();
        RefreshCounts(runner);
        ForgetFillsOnNewCharacter(runner);

        // A reveal from another pane (Moonlit, Characters, Flight, the MSQ status, chat) selected a scope whose
        // ancestors may be collapsed: this frame opens them and scrolls the selected node into view.
        revealing = ui.RevealPending;
        ui.RevealPending = false;
        sproutReach = ui.Filters.Preset == Preset.Sprout ? runner.Spoilers.ReachExpansion : null;
        KeepSelectionVisible();

        lineHeight = ImGui.GetTextLineHeight();
        glyphRadius = UiMetrics.TreeGlyphRadius(lineHeight);
        rowPadY = MathF.Max(ImGui.GetStyle().FramePadding.Y, (UiMetrics.TreeRowHeight(lineHeight) - lineHeight) * 0.5f);

        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        // The tier follows the pane (the ##left window, which the tree fills), not the room beside its scrollbar.
        tier = LayoutBudgets.TreeTierForPane(ImGui.GetWindowWidth(), UiMetrics.Scale, tier);

        // Rows touch: the washes of neighbouring rows meet, and each row is exactly its own height.
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(ImGui.GetStyle().ItemSpacing.X, 0f)))
        {
            // The Moon Road header and the dividers between the story, side and virtual blocks (Full and Quiet flair).
            var ornaments = Theme.ShowRules;
            if (ornaments)
            {
                DrawHeader(width);
            }

            var block = JournalBlock.All;
            DrawNode(allNode, section: true);
            foreach (var section in sections)
            {
                BlockDivider(ref block, section.Block, width, ornaments);
                DrawNode(section, section: true);
            }

            BlockDivider(ref block, JournalBlock.Virtual, width, ornaments);
            DrawNode(featureNode, section: true);
            // A reveal can land in the removed scope while the config hides the node; show it so the selection is visible.
            if (showUnlisted || ui.Scope == QuestScope.VirtualUnlisted)
            {
                DrawNode(unlistedNode, section: true);
            }

            // Quests on paths the character did not take: listed here only (unless the filter brings them back).
            if (otherPathsTotal > 0 || ui.Scope == QuestScope.VirtualOtherPaths)
            {
                DrawNode(otherPathsNode, section: true);
            }
        }

        revealing = false;
        ui.RecordSpan(UiRects.Tree, start, width);
    }

    /// <summary>Whether Sprout mode folds <paramref name="node"/>: every quest under it lies beyond the reach.</summary>
    private static bool IsSproutFolded(Node node, byte reach) =>
        node.MinExpansion != byte.MaxValue && node.MinExpansion > reach;

    /// <summary>
    /// Sprout mode folds nodes to their counts, so a scope selected inside one would leave no row selected. When the
    /// selection or the reach changes, a scope hidden under a folded node moves to that node (its nearest visible
    /// ancestor, always drawn since its parent is not folded), and the frame opens and scrolls to it.
    /// </summary>
    private void KeepSelectionVisible()
    {
        if (sproutReach is not { } reach)
        {
            sproutCheckedReach = null;
            return;
        }

        if (sproutCheckedReach == reach && sproutCheckedScope == ui.Scope)
        {
            return;
        }

        var target = ui.Scope;
        foreach (var section in sections)
        {
            if (FoldedHolder(section, reach, ui.Scope) is { } holder)
            {
                target = holder.Scope;
                break;
            }
        }

        if (target != ui.Scope)
        {
            Select(target);
            revealing = true;
        }

        sproutCheckedReach = reach;
        sproutCheckedScope = ui.Scope;
    }

    /// <summary>The outermost Sprout-folded node that hides <paramref name="scope"/> under it, or null when the scope's row is drawn.</summary>
    private static Node? FoldedHolder(Node node, byte reach, QuestScope scope)
    {
        if (IsSproutFolded(node, reach))
        {
            return node.Scope != scope && Contains(node, scope) ? node : null;
        }

        foreach (var child in node.Children)
        {
            if (FoldedHolder(child, reach, scope) is { } holder)
            {
                return holder;
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="node"/> or one of its descendants carries <paramref name="scope"/>.</summary>
    private static bool Contains(Node node, QuestScope scope)
    {
        if (node.Scope == scope)
        {
            return true;
        }

        foreach (var child in node.Children)
        {
            if (Contains(child, scope))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// One node: the tree item is drawn with an empty label so its arrow, hover, selection and keyboard navigation
    /// behave as usual, then the halo, the name, the pills and the right-aligned count are painted over it.
    /// <paramref name="section"/> nodes (the top level) are drawn a touch bolder with a VeilLine rule under them.
    /// </summary>
    private void DrawNode(Node node, bool section)
    {
        var flags = ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.FramePadding | ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick;
        // Sprout mode (T19): a node wholly beyond the character's story folds to its count, children unlisted.
        var sproutFolded = sproutReach is { } reach && IsSproutFolded(node, reach);
        if (node.Leaf || sproutFolded)
        {
            flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
        }

        var selected = ui.Scope == node.Scope;
        if (selected)
        {
            flags |= ImGuiTreeNodeFlags.Selected;
        }

        if (revealing && !node.Leaf && !selected && Contains(node, ui.Scope))
        {
            ImGui.SetNextItemOpen(true);
        }

        // The indented start of the row: TreeNodeEx puts its arrow here even though the item spans the full width.
        // The label is hidden, so the text colour only paints ImGui's arrow: it is made transparent and the overlay
        // draws a chevron that turns through Motion instead (T17, 140 ms).
        var indentX = ImGui.GetCursorScreenPos().X;
        var expandable = !node.Leaf && !sproutFolded;
        var arrowColor = ImGui.GetColorU32(ImGuiCol.Text);
        bool open;
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle().FramePadding.X, rowPadY)))
        using (ImRaii.PushColor(ImGuiCol.Header, SelectedWash)
                     .Push(ImGuiCol.HeaderHovered, selected ? SelectedHoverWash : HoverWash)
                     .Push(ImGuiCol.HeaderActive, selected ? SelectedHoverWash : ActiveWash)
                     .Push(ImGuiCol.Text, Vector4.Zero, expandable))
        {
            open = ImGui.TreeNodeEx(node.Id, flags, HiddenLabel);
        }

        var itemId = ImGuiP.GetItemID();
        if (revealing && selected)
        {
            ImGui.SetScrollHereY(0.5f);
            Motion.Trigger(Motion.Key(RevealTag, itemId));
        }
        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
        {
            Select(node.Scope);
        }

        // The overlay paints on the node item; its tooltip depends on the part under the mouse. Rows scrolled out of
        // view skip the painting (a fully open tree is hundreds of halos); a hidden row cannot be hovered anyway.
        var hover = Hover.None;
        var row = default(RowOutcome);
        if (ImGui.IsItemVisible())
        {
            hover = DrawNodeOverlay(node, section, selected, indentX, itemId, out row);
            if (expandable)
            {
                DrawChevron(indentX, Motion.Lerp(Motion.Key(ChevronTag, itemId), open ? 1f : 0f, MotionMath.ChevronRate), arrowColor);
            }
        }
        if (ImGui.IsItemHovered())
        {
            if (sproutFolded && hover is not (Hover.Halo or Hover.Progress or Hover.Ready))
            {
                if (row.NameCut || node.Shortened)
                {
                    UiMetrics.Tooltip(node.FullName, Strings.SproutFoldedTooltip);
                }
                else
                {
                    UiMetrics.Tooltip(Strings.SproutFoldedTooltip);
                }
            }
            else
            {
                DrawTooltip(node, hover, row);
            }
        }

        if (open && !node.Leaf && !sproutFolded)
        {
            foreach (var child in node.Children)
            {
                DrawNode(child, section: false);
            }

            ImGui.TreePop();
        }
    }

    /// <summary>What a row's overlay decided, for its tooltip.</summary>
    /// <param name="NameCut">The label ended in an ellipsis.</param>
    /// <param name="ReadyDot">The Ready count showed as a gold dot on the glyph rather than a pill.</param>
    private readonly record struct RowOutcome(bool NameCut, bool ReadyDot);

    /// <summary>
    /// Halo, name, pills, mini bar and count painted on the node's row; drawn without items so layout is untouched.
    /// The parts are fitted right to left by <see cref="TreeRowFit"/> within the tree's <see cref="TreeTier"/> (feature
    /// plan v4 L3): the name keeps at least <see cref="LayoutBudgets.RowNameMinLogical"/> (and its expansion suffix
    /// whole where the pill does not show) and ends in an ellipsis,
    /// and a part that does not fit is not drawn, so nothing overlaps the glyph or the chevron at any width. Where the
    /// Ready pill cannot show, a gold dot on the glyph carries it. Returns which part the mouse is over, for the
    /// caller's item-hover tooltip (the node is the item).
    /// </summary>
    private Hover DrawNodeOverlay(Node node, bool section, bool selected, float indentX, uint itemId, out RowOutcome outcome)
    {
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var style = ImGui.GetStyle();
        var radius = glyphRadius;
        var pad = UiMetrics.Px(6f);
        var rowCenterY = (min.Y + max.Y) * 0.5f;
        var textY = rowCenterY - lineHeight * 0.5f;
        var complete = node.Complete;
        Measure(node, section);

        // The selected row's one gold element: a 2 px Moon rule on the left edge.
        if (selected)
        {
            dl.AddRectFilled(min, new Vector2(min.X + MathF.Max(2f, MathF.Round(UiMetrics.Px(2f))), max.Y), Theme.MoonU32);
        }

        // Where TreeNodeEx puts its label: after the arrow slot (one font size plus twice the frame padding).
        var labelX = indentX + ImGui.GetFontSize() + style.FramePadding.X * 2f;
        var haloCenter = new Vector2(labelX + radius, rowCenterY);
        var namePos = new Vector2(labelX + radius * 2f + pad, textY);
        var right = max.X - pad;

        // The tier sets each part's form: the count as "done / total", as a percentage (none once complete), or gone.
        var (countText, countWidth) = tier switch
        {
            TreeTier.Full or TreeTier.Trim => (node.CountText, node.CountWidth),
            TreeTier.Compact when node.CountIsTally => (node.CountText, node.CountWidth),
            TreeTier.Compact when !complete => (node.PercentText, node.PercentWidth),
            _ => (string.Empty, 0f),
        };

        var barWidth = UiMetrics.Px(BarWidthLogical);
        var barGap = UiMetrics.Px(BarGapLogical);
        var pillText = node.PillText;
        // Under Full and Quiet flair the road under the row carries the length encoding, so the mini bar takes no room.
        var road = Theme.ShowRules && !node.CountIsTally;
        // Where the pill does not show the label carries the expansion suffix whole, and the fit makes room for it.
        var widths = new TreeRowWidths(
            node.NameWidth,
            node.SuffixWidth,
            countWidth > 0f ? countWidth + pad : 0f,
            node.Ready > 0 && tier != TreeTier.Slim ? node.ReadyWidth + pad : 0f,
            !road && tier <= TreeTier.Trim && !node.CountIsTally ? barWidth + barGap : 0f,
            tier == TreeTier.Full && pillText.Length > 0 ? node.PillWidth + pad : 0f);
        var fit = TreeRowFit.Fit(right - namePos.X, UiMetrics.Px(LayoutBudgets.RowNameMinLogical), in widths);
        var showCount = fit.Count;
        var showReady = fit.Ready;
        var showBar = fit.Bar;
        var showPill = fit.Pill;

        // Count, right-aligned: Dusk, Silver on the selected row, MoonDim once complete.
        var progressLeft = right;
        if (showCount)
        {
            var countColor = complete ? Theme.MoonDimU32 : selected ? Theme.SilverU32 : Theme.DuskU32;
            progressLeft = right - countWidth;
            dl.AddText(new Vector2(progressLeft, textY), countColor, countText);
        }

        // Mini bar 12 px before the count.
        if (showBar)
        {
            var barRight = showCount ? progressLeft - barGap : right;
            progressLeft = barRight - barWidth;
            DrawMiniBar(dl, new Vector2(progressLeft, rowCenterY), barWidth, node.Count, complete);
        }

        // The name in the room the parts leave, with an ellipsis when cut. A section whose expansion pill does not show
        // names its expansion in the label instead ("Main Scenario · DT"), drawn whole after the name, which is cut
        // before it ("Main Sc… · DT"), so the two Main Scenario rows always stay apart.
        var nameColor = complete ? Theme.MoonDimU32 : ImGui.GetColorU32(ImGuiCol.Text);
        var bold = section ? UiMetrics.Hairline : 0f;
        var textRoom = MathF.Max(0f, fit.HeadRoom - bold);
        var cut = node.Name.Length > 0 && textRoom <= 0f;
        if (textRoom > 0f)
        {
            cut = DrawLabelText(dl, namePos, textRoom, node.Name, nameColor, node.NameWidth - bold, bold);
        }

        if (fit.SuffixInLabel)
        {
            var suffixPos = new Vector2(namePos.X + MathF.Min(node.NameWidth - bold, textRoom), namePos.Y);
            var suffixRoom = namePos.X + fit.LabelRoom - bold - suffixPos.X;
            if (suffixRoom > 0f)
            {
                cut |= DrawLabelText(dl, suffixPos, suffixRoom, node.SuffixText, nameColor, node.SuffixWidth, bold);
            }
        }

        var nameEnd = namePos.X + MathF.Min(node.NameWidth, fit.HeadRoom);

        var hover = Hover.None;
        var pillX = nameEnd + pad;
        if (showPill)
        {
            var pillFont = ImGui.GetFontSize() * PillFontFraction;
            var pillHeight = MathF.Min(max.Y - min.Y - 4f, pillFont + 2f * UiMetrics.Px(2f));
            var pillMin = new Vector2(pillX, rowCenterY - pillHeight * 0.5f);
            var pillMax = new Vector2(pillX + node.PillWidth, rowCenterY + pillHeight * 0.5f);
            dl.AddRect(pillMin, pillMax, Theme.VeilU32, pillHeight * 0.5f, ImDrawFlags.None, 1f);
            dl.AddText(ImGui.GetFont(), pillFont, new Vector2(pillX + UiMetrics.Px(PillPadLogical), rowCenterY - pillFont * 0.5f), Theme.DuskU32, pillText);
            pillX = pillMax.X + pad;
        }

        if (showReady)
        {
            var badgeHeight = MathF.Min(max.Y - min.Y - 4f, lineHeight + 2f);
            var badgeMin = new Vector2(pillX, rowCenterY - badgeHeight * 0.5f);
            var badgeMax = new Vector2(pillX + node.ReadyWidth, rowCenterY + badgeHeight * 0.5f);
            dl.AddRectFilled(badgeMin, badgeMax, ReadyBadgeFill, badgeHeight * 0.5f);
            dl.AddText(new Vector2(pillX + UiMetrics.Px(PillPadLogical), textY), Theme.MoonU32, node.ReadyText);
            if (ImGui.IsMouseHoveringRect(badgeMin, badgeMax, false))
            {
                hover = Hover.Ready;
            }
        }

        // The glyph, with the Ready mark when the pill could not show. The fill moves only when the count changes (a
        // quest completed, another character viewed) or, under Full flair, when the orbit first shows; never on its own.
        var readyDot = node.Ready > 0 && !showReady;
        var shown = NodeFill(Motion.Key(GaugeTag, itemId), node.Count.Fraction);
        DrawNodeGlyph(dl, node, haloCenter, radius, shown, readyDot);
        if (road)
        {
            DrawRoad(dl, namePos.X, max.X - UiMetrics.Px(RoadEndLogical), max.Y - UiMetrics.Px(RoadLiftLogical), shown, selected);
        }

        if (selected)
        {
            // The reveal pulse (a reveal from another pane landed here): around the row, inside the pane's edges.
            Motion.DrawRevealPulse(dl, Motion.Key(RevealTag, itemId), new Vector2(indentX, min.Y + 1f), new Vector2(max.X - pad, max.Y - 1f), UiMetrics.Px(4f));
        }

        // Section rows read as chapters: a 1 px VeilLine rule under the row, indented past the arrow (Plain flair, and
        // the Other paths row, which has no road; the road takes its place otherwise).
        if (section && !road)
        {
            var y = max.Y - 0.5f;
            dl.AddLine(new Vector2(labelX, y), new Vector2(max.X - pad, y), Theme.VeilLineU32, 1f);
        }

        outcome = new RowOutcome(cut, readyDot);
        if (hover != Hover.None)
        {
            return hover;
        }

        var haloHalf = new Vector2(radius);
        if (ImGui.IsMouseHoveringRect(haloCenter - haloHalf, haloCenter + haloHalf, false))
        {
            return Hover.Halo;
        }

        return progressLeft < right && ImGui.IsMouseHoveringRect(new Vector2(progressLeft, min.Y), max, false) ? Hover.Progress : Hover.None;
    }

    /// <summary>
    /// Measures the node's texts at the current font size, once: again only when the font size (the UI scale) or one
    /// of the texts changed, so a row costs no text measuring per frame.
    /// </summary>
    private static void Measure(Node node, bool section)
    {
        var fontSize = ImGui.GetFontSize();
        if (node.MeasuredAt == fontSize)
        {
            return;
        }

        var bold = section ? UiMetrics.Hairline : 0f;
        var pillPad = 2f * UiMetrics.Px(PillPadLogical);
        node.NameWidth = ImGui.CalcTextSize(node.Name).X + bold;
        node.SuffixWidth = node.SuffixText.Length > 0 ? ImGui.CalcTextSize(node.SuffixText).X : 0f;
        node.CountWidth = node.CountText.Length > 0 ? ImGui.CalcTextSize(node.CountText).X : 0f;
        node.PercentWidth = node.PercentText.Length > 0 ? ImGui.CalcTextSize(node.PercentText).X : 0f;
        node.ReadyWidth = node.Ready > 0 ? ImGui.CalcTextSize(node.ReadyText).X + pillPad : 0f;
        node.PillWidth = node.PillText.Length > 0 ? (ImGui.CalcTextSize(node.PillText).X * PillFontFraction) + pillPad : 0f;
        node.MeasuredAt = fontSize;
    }

    /// <summary>
    /// Label text in its room, ending in an ellipsis when cut; on a section row (<paramref name="bold"/> above 0) a second
    /// pass that many pixels to the right thickens the strokes, as Dalamud has no bold face. Returns whether it was cut.
    /// </summary>
    private static bool DrawLabelText(ImDrawListPtr dl, Vector2 pos, float room, string text, uint color, float width, float bold)
    {
        var cut = Chrome.EllipsisTextAt(dl, pos, room, text, color, width);
        if (bold > 0f)
        {
            Chrome.EllipsisTextAt(dl, pos + new Vector2(bold, 0f), room, text, color, width);
        }

        return cut;
    }

    /// <summary>The 44 × 3 mini bar: Veil track, Moon fill (MoonDim once complete), at least 2 px of fill above 0.</summary>
    private static void DrawMiniBar(ImDrawListPtr dl, Vector2 leftCenter, float width, NodeCount count, bool complete)
    {
        var height = MathF.Max(2f, MathF.Round(UiMetrics.Px(BarHeightLogical)));
        var top = MathF.Round(leftCenter.Y - height * 0.5f);
        var trackMin = new Vector2(MathF.Round(leftCenter.X), top);
        var trackMax = new Vector2(trackMin.X + MathF.Round(width), top + height);
        dl.AddRectFilled(trackMin, trackMax, Theme.VeilU32, height * 0.5f);
        if (count.Done <= 0 || count.Total <= 0)
        {
            return;
        }

        var fill = MathF.Max(2f, MathF.Round(width * count.Fraction));
        dl.AddRectFilled(trackMin, new Vector2(MathF.Min(trackMax.X, trackMin.X + fill), trackMax.Y), complete ? Theme.MoonDimU32 : Theme.MoonU32, height * 0.5f);
    }

    /// <summary>
    /// The expand chevron where TreeNodeEx puts its arrow (the same size and place: RenderArrow at 0.7 of the font size,
    /// after the frame padding), turned from pointing right (<paramref name="turn"/> 0, closed) to pointing down (1,
    /// open). <paramref name="turn"/> comes from Motion, so it eases over 140 ms after a toggle and sits still otherwise.
    /// </summary>
    private static void DrawChevron(float indentX, float turn, uint color)
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var fontSize = ImGui.GetFontSize();
        var center = new Vector2(indentX + ImGui.GetStyle().FramePadding.X + (fontSize * 0.5f), (min.Y + max.Y) * 0.5f);
        var r = fontSize * 0.40f * 0.70f;
        var angle = turn * MathF.PI * 0.5f;
        var (sin, cos) = MathF.SinCos(angle);
        Vector2 Turn(float x, float y) => center + new Vector2((x * cos) - (y * sin), (x * sin) + (y * cos));
        ImGui.GetWindowDrawList().AddTriangleFilled(Turn(0.750f * r, 0f), Turn(-0.750f * r, 0.866f * r), Turn(-0.750f * r, -0.866f * r), color);
    }

    /// <summary>
    /// Hover text by row part: the Ready badge names its count; the halo and the count give done/total with the
    /// percentage (and the Ready count where it shows as a dot); elsewhere a folded node shows its full path under a
    /// 32 px halo and the exact fraction (T8, §4.7), and a node whose label is cut or shortened shows its full name and
    /// its journal path (feature plan v4 L3).
    /// </summary>
    private static void DrawTooltip(Node node, Hover hover, RowOutcome row)
    {
        switch (hover)
        {
            case Hover.Ready:
                UiMetrics.Tooltip(node.ReadyTooltip);
                return;
            case Hover.Halo:
            case Hover.Progress:
                var progress = node.HoverText.Length > 0 ? node.HoverText : node.ProgressText;
                if (row.ReadyDot)
                {
                    using var readyStyle = Theme.PushTooltip();
                    using var readyTip = ImRaii.Tooltip();
                    UiMetrics.ApplyFontScale();
                    using var readyWrap = UiMetrics.TooltipWrap();
                    ImGui.TextUnformatted(Strings.FillingMoonTooltip);
                    ImGui.TextDisabled(progress);
                    using (ImRaii.PushColor(ImGuiCol.Text, Theme.Moon))
                    {
                        ImGui.TextUnformatted(node.ReadyTooltip);
                    }

                    return;
                }

                UiMetrics.Tooltip(Strings.FillingMoonTooltip, progress);
                return;
        }

        if (node.FoldedPath is not { } path)
        {
            if (row.NameCut || node.Shortened)
            {
                UiMetrics.Tooltip(node.FullName, node.Path.Length > 0 && node.Path != node.FullName ? node.Path : null);
            }

            return;
        }

        using var tooltipStyle = Theme.PushTooltip();
        using var tooltip = ImRaii.Tooltip();
        UiMetrics.ApplyFontScale();
        using var wrap = UiMetrics.TooltipWrap();
        var box = 2f * MathF.Max(16f, UiMetrics.Icon(11f));
        MoonGlyph.DrawHaloInline(node.Count.Fraction, box, dimComplete: node.Complete);
        ImGui.SameLine();
        using (ImRaii.Group())
        {
            ImGui.TextUnformatted(node.FractionText);
            ImGui.TextDisabled(node.ProgressText);
        }

        ImGui.TextUnformatted(path);
    }

    private void Select(QuestScope scope)
    {
        if (ui.Scope == scope)
        {
            return;
        }

        ui.Scope = scope;
        ui.MarkQueryDirty();
    }

    private void RefreshCounts(QueryRunner runner)
    {
        var current = runner.Counts;
        var featureChanged = featureCount != runner.FeatureCount || featureReady != runner.FeatureReady;
        if (ReferenceEquals(current, counts) && !featureChanged)
        {
            return;
        }

        if (featureChanged)
        {
            featureCount = runner.FeatureCount;
            featureReady = runner.FeatureReady;
            Apply(featureNode, featureCount, featureReady);
        }

        if (current is null || ReferenceEquals(current, counts))
        {
            return;
        }

        counts = current;
        Apply(allNode, current.Overall, current.OverallReady);
        ApplyOtherPaths(allNode, current.OtherPathsIn(QuestScope.None), current.Overall.BeyondTrial);
        Apply(unlistedNode, current.Unlisted, 0);
        foreach (var section in sections)
        {
            ApplyTree(section, current);
        }

        // The Other paths node counts its quests; nothing there is to do, so it has no progress and no Ready badge.
        var paths = current.OtherPathsNode;
        otherPathsTotal = paths.Total;
        otherPathsNode.Count = default;
        otherPathsNode.CountText = paths.Total.ToString("N0", CultureInfo.CurrentCulture);
        otherPathsNode.ProgressText = Core.Evaluation.PathText.Tally(paths);
        otherPathsNode.HoverText = otherPathsNode.ProgressText;
        otherPathsNode.MeasuredAt = -1f;
    }

    /// <summary>
    /// The node's other-path tally and the halo tooltip that carries it, with "N beyond your trial" under the
    /// free-trial view (1.9.0).
    /// </summary>
    private static void ApplyOtherPaths(Node node, Core.Evaluation.PathTally tally, int beyondTrial = 0)
    {
        var text = Core.Evaluation.PathText.Tally(tally);
        if (beyondTrial > 0)
        {
            var trial = string.Format(CultureInfo.CurrentCulture, Strings.TrialBeyondCountFormat, beyondTrial);
            text = text.Length == 0 ? trial : text + "\n" + trial;
        }

        if (text != node.OtherPathsText || node.HoverText.Length == 0)
        {
            node.OtherPathsText = text;
            node.HoverText = text.Length == 0 ? node.ProgressText : node.ProgressText + "\n" + text;
        }
    }

    /// <summary>Counts come from the node's scope, not its depth, so a folded node reads its genre's numbers.</summary>
    private static void ApplyTree(Node node, TreeCounts current)
    {
        var (count, ready) = node.Scope.Kind switch
        {
            ScopeKind.Section => (current.Section(node.Scope.Id), current.SectionReady(node.Scope.Id)),
            ScopeKind.Category => (current.Category(node.Scope.Id), current.CategoryReady(node.Scope.Id)),
            _ => (current.Genre(node.Scope.Id), current.GenreReady(node.Scope.Id)),
        };
        Apply(node, count, ready);
        ApplyOtherPaths(node, current.OtherPathsIn(node.Scope), count.BeyondTrial);
        foreach (var child in node.Children)
        {
            ApplyTree(child, current);
        }
    }

    private static void Apply(Node node, NodeCount count, int ready)
    {
        if (node.Count != count || node.CountText.Length == 0)
        {
            node.Count = count;
            node.CountText = string.Format(CultureInfo.CurrentCulture, Strings.TreeCountFormat, count.Done, count.Total);
            node.ProgressText = UiFormat.Progress(count.Done, count.Total);
            node.FractionText = string.Format(CultureInfo.CurrentCulture, Strings.TreeFractionFormat, count.Fraction);
            // Floored, like the status bar: a node reads 100 % only once complete (and then the Compact tier hides it).
            var percent = count.Total <= 0 ? 0 : (int)MathF.Floor(100f * count.Done / count.Total);
            node.PercentText = string.Format(CultureInfo.CurrentCulture, Strings.StatusPercentFormat, percent);
            node.HoverText = string.Empty;
            node.MeasuredAt = -1f;
        }

        if (node.Ready != ready || node.ReadyText.Length == 0)
        {
            node.Ready = ready;
            node.ReadyText = ready.ToString("N0", CultureInfo.CurrentCulture);
            node.ReadyTooltip = string.Format(CultureInfo.CurrentCulture, Strings.TreeReadyBadgeFormat, ready);
            node.MeasuredAt = -1f;
        }
    }

    /// <summary>The UI language the nodes' labels and texts were built in.</summary>
    private int nodesLanguage = -1;

    private void EnsureNodes(CatalogBundle current)
    {
        if (ReferenceEquals(bundle, current) && nodesLanguage == Localization.Loc.Version)
        {
            return;
        }

        nodesLanguage = Localization.Loc.Version;
        NameVirtual(allNode, Strings.AllQuests);
        NameVirtual(featureNode, Strings.FeatureUnlocks);
        NameVirtual(unlistedNode, Strings.RemovedFromGame);
        NameVirtual(otherPathsNode, Strings.OtherPaths);
        otherPathsNode.CountIsTally = true;
        bundle = current;
        counts = null;
        featureReady = -1;
        filled.Clear();
        sproutCheckedReach = null;
        sections.Clear();

        var ordered = new List<QuestRecord>(current.Catalog.All);
        ordered.Sort(static (a, b) => a.Journal.SortKey != b.Journal.SortKey ? a.Journal.SortKey.CompareTo(b.Journal.SortKey) : a.RowId.CompareTo(b.RowId));

        var sectionById = new Dictionary<uint, Node>();
        var categoryById = new Dictionary<uint, Node>();
        var genreById = new Dictionary<uint, Node>();
        var seen = new HashSet<Node>();
        foreach (var quest in ordered)
        {
            if (quest.IsRemoved)
            {
                continue;
            }

            var j = quest.Journal;
            if (!sectionById.TryGetValue(j.SectionId, out var section))
            {
                section = new Node(QuestScope.Section(j.SectionId), "##s" + j.SectionId.ToString(CultureInfo.InvariantCulture), j.SectionName, leaf: false);
                sectionById[j.SectionId] = section;
                sections.Add(section);
            }

            if (!categoryById.TryGetValue(j.CategoryId, out var category))
            {
                category = new Node(QuestScope.Category(j.CategoryId), "##c" + j.CategoryId.ToString(CultureInfo.InvariantCulture), j.CategoryName, leaf: false);
                categoryById[j.CategoryId] = category;
                section.Children.Add(category);
            }

            if (!genreById.TryGetValue(j.GenreId, out var genre))
            {
                genre = new Node(QuestScope.Genre(j.GenreId), "##g" + j.GenreId.ToString(CultureInfo.InvariantCulture), j.GenreName, leaf: true);
                genreById[j.GenreId] = genre;
                category.Children.Add(genre);
            }

            section.MinExpansion = Math.Min(section.MinExpansion, quest.Expansion);
            category.MinExpansion = Math.Min(category.MinExpansion, quest.Expansion);
            genre.MinExpansion = Math.Min(genre.MinExpansion, quest.Expansion);
            NoteExpansion(section, quest.Expansion, seen);
            NoteExpansion(category, quest.Expansion, seen);
            NoteExpansion(genre, quest.Expansion, seen);
        }

        foreach (var node in seen)
        {
            node.ExpansionText = node.Expansion == MixedExpansion ? string.Empty : Strings.ExpansionShort((byte)node.Expansion);
        }

        for (var i = 0; i < sections.Count; i++)
        {
            // The block is read from the section id before a fold hands the node its genre's scope.
            var block = JournalBlocks.ForSection(sections[i].Scope.Id);
            sections[i] = Fold(sections[i]);
            sections[i].Block = block;
            NameTree(sections[i], parent: null, sections[i].FullName, current.Language, string.Empty);
        }

        allNode.Block = JournalBlock.All;
        appliedIcons = null;
    }

    /// <summary>
    /// Hands every node its icon from the current catalog's <see cref="NodeIconMap"/> when the map or the nodes changed
    /// (once per catalog; a map that lands after the nodes were built is applied the frame it arrives). The Other paths
    /// node, which the map does not know, takes the Other glyph.
    /// </summary>
    private void ApplyIcons()
    {
        var map = nodeIcons();
        if (ReferenceEquals(map, appliedIcons))
        {
            return;
        }

        appliedIcons = map;
        var any = map.Nodes.Count > 0;
        ApplyIcon(allNode, map, any);
        foreach (var section in sections)
        {
            ApplyIcon(section, map, any);
        }

        ApplyIcon(featureNode, map, any);
        ApplyIcon(unlistedNode, map, any);
        ApplyIcon(otherPathsNode, map, any);
    }

    /// <summary>The node's icon and its children's; with an empty map (no sheets) every node keeps the moon halo.</summary>
    private static void ApplyIcon(Node node, NodeIconMap map, bool any)
    {
        node.Icon = any ? map.For(node.Scope) : default;
        foreach (var child in node.Children)
        {
            ApplyIcon(child, map, any);
        }
    }

    /// <summary>Between the names of a journal path in the tooltip, as in <see cref="Strings.FoldedPathFormat"/>.</summary>
    private const string PathSeparator = " › ";

    /// <summary>A virtual node's label (All quests, Unlock quests, …): its own name, never shortened.</summary>
    private static void NameVirtual(Node node, string name)
    {
        node.Name = name;
        node.FullName = name;
        node.SuffixText = string.Empty;
        node.MeasuredAt = -1f;
        // Their counts' texts are in the UI language too: cleared, so the next counts rebuild them.
        node.CountText = string.Empty;
        node.ReadyText = string.Empty;
    }

    /// <summary>
    /// Gives a node and its children their labels (feature plan v4 L3): the short name (<see cref="JournalNames.Short"/>,
    /// English catalogs only), a top-level node's expansion suffix split off for its pill, and the full journal path
    /// for the tooltip. Once per catalog and language, never per frame.
    /// </summary>
    private static void NameTree(Node node, Node? parent, string section, string language, string parentPath)
    {
        var full = node.FullName;
        var shortName = JournalNames.Short(full, parent?.FullName, section, language);
        var (head, suffix) = JournalNames.SplitExpansion(shortName);
        if (parent is null)
        {
            // A section: "Main Scenario" with an "ARR–EW" pill that yields to the name when room runs short.
            node.Name = head;
            node.Suffix = suffix;
            node.SuffixText = suffix.Length > 0 ? JournalNames.SuffixSeparator + suffix : string.Empty;
        }
        else
        {
            // A role genre ("Tank · ShB") keeps its expansion in the label, so its one-expansion pill would repeat it.
            node.Name = shortName;
            node.Suffix = string.Empty;
            node.SuffixText = string.Empty;
            if (suffix.Length > 0)
            {
                node.ExpansionText = string.Empty;
            }
        }

        node.Shortened = !string.Equals(shortName, full, StringComparison.Ordinal);
        node.Path = parentPath.Length == 0 ? full : parentPath + PathSeparator + full;
        node.MeasuredAt = -1f;
        foreach (var child in node.Children)
        {
            NameTree(child, node, section, language, node.Path);
        }
    }

    /// <summary>Keeps a node's expansion while every quest agrees, and marks it mixed on the first that does not.</summary>
    private static void NoteExpansion(Node node, byte expansion, HashSet<Node> seen)
    {
        if (seen.Add(node))
        {
            node.Expansion = expansion;
        }
        else if (node.Expansion != expansion)
        {
            node.Expansion = MixedExpansion;
        }
    }

    /// <summary>
    /// Folds single-child chains: a category with one genre becomes a leaf named after the category but scoped to the
    /// genre; a section left with one folded leaf becomes that leaf under the section's name. Selection still matches
    /// because the folded node carries the genre scope the table is scoped to.
    /// </summary>
    private static Node Fold(Node section)
    {
        for (var i = 0; i < section.Children.Count; i++)
        {
            var category = section.Children[i];
            if (category.Children.Count == 1)
            {
                var genre = category.Children[0];
                section.Children[i] = new Node(genre.Scope, category.Id, category.Name, leaf: true)
                {
                    FoldedPath = string.Format(CultureInfo.CurrentCulture, Strings.FoldedPathFormat, section.Name, category.Name, genre.Name),
                    MinExpansion = category.MinExpansion,
                    Expansion = genre.Expansion,
                    ExpansionText = genre.ExpansionText,
                };
            }
        }

        if (section.Children.Count == 1 && section.Children[0] is { Leaf: true } only)
        {
            return new Node(only.Scope, section.Id, section.Name, leaf: true)
            {
                FoldedPath = only.FoldedPath,
                MinExpansion = section.MinExpansion,
                Expansion = only.Expansion,
                ExpansionText = only.ExpansionText,
            };
        }

        return section;
    }
}
