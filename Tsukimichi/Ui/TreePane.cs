using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The Journal tree: All quests, then Section → Category → Genre, then the Feature Unlocks and Unlisted virtual nodes.
/// Each node shows a filling moon and done/total. Selecting a node scopes the table through <see cref="UiState.Scope"/>.
/// A category with a single genre is folded into one leaf (the category's name, the genre's scope and counts), and a
/// section whose only category folded likewise becomes a single leaf, so no node ever expands to just one child.
/// The node list is built once per catalog; count labels are re-materialized only when the counts instance changes.
/// </summary>
public sealed class TreePane
{
    private sealed class Node(QuestScope scope, string id, string name, bool leaf)
    {
        public QuestScope Scope { get; } = scope;
        public string Id { get; } = id;
        public string Name { get; } = name;
        public bool Leaf { get; } = leaf;
        public List<Node> Children { get; } = [];

        /// <summary>Full journal path of a folded node, shown on hover; null for ordinary nodes.</summary>
        public string? FoldedPath { get; set; }

        public NodeCount Count { get; set; }
        public string CountText { get; set; } = string.Empty;
    }

    private readonly UiState ui;

    private CatalogBundle? bundle;
    private readonly List<Node> sections = [];
    private readonly Node allNode = new(QuestScope.None, "##all", Strings.AllQuests, leaf: true);
    private readonly Node featureNode = new(QuestScope.VirtualFeature, "##feature", Strings.FeatureUnlocks, leaf: true);
    private readonly Node unlistedNode = new(QuestScope.VirtualUnlisted, "##unlisted", Strings.Unlisted, leaf: true);
    private TreeCounts? counts;
    private NodeCount featureCount;

    public TreePane(UiState ui)
    {
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
    }

    public void Draw(CatalogBundle current, QueryRunner runner, bool showUnlisted)
    {
        EnsureNodes(current);
        RefreshCounts(runner);

        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        DrawNode(allNode);
        foreach (var section in sections)
        {
            DrawNode(section);
        }

        DrawNode(featureNode);
        // A reveal can land in the Unlisted scope while the config hides the node; show it so the selection is visible.
        if (showUnlisted || ui.Scope == QuestScope.VirtualUnlisted)
        {
            DrawNode(unlistedNode);
        }

        ui.RecordSpan(UiRects.Tree, start, width);
    }

    private void DrawNode(Node node)
    {
        var flags = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick;
        if (node.Leaf)
        {
            flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
        }

        if (ui.Scope == node.Scope)
        {
            flags |= ImGuiTreeNodeFlags.Selected;
        }

        var open = ImGui.TreeNodeEx(node.Id, flags, node.Name);
        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
        {
            Select(node.Scope);
        }

        if (node.FoldedPath is { } path && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(path);
        }

        DrawCountOverlay(node);

        if (open && !node.Leaf)
        {
            foreach (var child in node.Children)
            {
                DrawNode(child);
            }

            ImGui.TreePop();
        }
    }

    /// <summary>Filling moon and done/total right-aligned on the node's line, drawn without an item so layout is untouched.</summary>
    private static void DrawCountOverlay(Node node)
    {
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var textSize = ImGui.CalcTextSize(node.CountText);
        var radius = UiMetrics.TreeMoonRadius;
        var pad = UiMetrics.Px(6f);
        var lineCenterY = (min.Y + max.Y) * 0.5f;

        var textPos = new Vector2(max.X - pad - textSize.X, lineCenterY - textSize.Y * 0.5f);
        dl.AddText(textPos, Theme.DuskU32, node.CountText);

        var moonCenter = new Vector2(textPos.X - pad - radius, lineCenterY);
        MoonGlyph.DrawFilling(dl, moonCenter, radius, node.Count.Fraction);
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
        var featureChanged = featureCount != runner.FeatureCount;
        if (ReferenceEquals(current, counts) && !featureChanged)
        {
            return;
        }

        if (featureChanged)
        {
            featureCount = runner.FeatureCount;
            Apply(featureNode, featureCount);
        }

        if (current is null || ReferenceEquals(current, counts))
        {
            return;
        }

        counts = current;
        Apply(allNode, current.Overall);
        Apply(unlistedNode, current.Unlisted);
        foreach (var section in sections)
        {
            ApplyTree(section, current);
        }
    }

    /// <summary>Counts come from the node's scope, not its depth, so a folded node reads its genre's numbers.</summary>
    private static void ApplyTree(Node node, TreeCounts current)
    {
        var count = node.Scope.Kind switch
        {
            ScopeKind.Section => current.Section(node.Scope.Id),
            ScopeKind.Category => current.Category(node.Scope.Id),
            _ => current.Genre(node.Scope.Id),
        };
        Apply(node, count);
        foreach (var child in node.Children)
        {
            ApplyTree(child, current);
        }
    }

    private static void Apply(Node node, NodeCount count)
    {
        if (node.Count == count && node.CountText.Length > 0)
        {
            return;
        }

        node.Count = count;
        node.CountText = UiFormat.Count(count.Done, count.Total);
    }

    private void EnsureNodes(CatalogBundle current)
    {
        if (ReferenceEquals(bundle, current))
        {
            return;
        }

        bundle = current;
        counts = null;
        sections.Clear();

        var ordered = new List<QuestRecord>(current.Catalog.All);
        ordered.Sort(static (a, b) => a.Journal.SortKey != b.Journal.SortKey ? a.Journal.SortKey.CompareTo(b.Journal.SortKey) : a.RowId.CompareTo(b.RowId));

        var sectionById = new Dictionary<uint, Node>();
        var categoryById = new Dictionary<uint, Node>();
        var genreById = new Dictionary<uint, Node>();
        foreach (var quest in ordered)
        {
            if (quest.IsUnlisted)
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

            if (!genreById.ContainsKey(j.GenreId))
            {
                var genre = new Node(QuestScope.Genre(j.GenreId), "##g" + j.GenreId.ToString(CultureInfo.InvariantCulture), j.GenreName, leaf: true);
                genreById[j.GenreId] = genre;
                category.Children.Add(genre);
            }
        }

        for (var i = 0; i < sections.Count; i++)
        {
            sections[i] = Fold(sections[i]);
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
                };
            }
        }

        if (section.Children.Count == 1 && section.Children[0] is { Leaf: true } only)
        {
            return new Node(only.Scope, section.Id, section.Name, leaf: true) { FoldedPath = only.FoldedPath };
        }

        return section;
    }
}
