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
/// The Journal tree: All quests, then Section → Category → Genre, then the Feature Unlocks and Unlisted virtual nodes.
/// Each node shows a filling moon and done/total. Selecting a node scopes the table through <see cref="UiState.Scope"/>.
/// The node list is built once per catalog; count labels are re-materialized only when the counts instance changes.
/// </summary>
public sealed class TreePane
{
    private const float MoonRadius = 6f;

    private sealed class Node(QuestScope scope, string id, string name, bool leaf)
    {
        public QuestScope Scope { get; } = scope;
        public string Id { get; } = id;
        public string Name { get; } = name;
        public bool Leaf { get; } = leaf;
        public List<Node> Children { get; } = [];
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

        DrawNode(allNode);
        foreach (var section in sections)
        {
            DrawNode(section);
        }

        DrawNode(featureNode);
        if (showUnlisted)
        {
            DrawNode(unlistedNode);
        }
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
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var textSize = ImGui.CalcTextSize(node.CountText);
        var radius = MoonRadius * scale;
        var pad = 6f * scale;
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
            Apply(section, current.Section(section.Scope.Id));
            foreach (var category in section.Children)
            {
                Apply(category, current.Category(category.Scope.Id));
                foreach (var genre in category.Children)
                {
                    Apply(genre, current.Genre(genre.Scope.Id));
                }
            }
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
    }
}
