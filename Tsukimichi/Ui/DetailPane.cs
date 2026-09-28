using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail pane for <see cref="UiState.SelectedRowId"/> on a Night panel: header, requirements, rewards, path,
/// giver, provenance. Everything shown is materialized into a <see cref="Model"/> when the selection or the session
/// version changes, so drawing allocates nothing.
/// </summary>
public sealed class DetailPane
{
    private const float HeaderGlyphRadius = 20f;
    private const float PathGlyphRadius = 7f;
    private const int PathScrollFrames = 2;
    private const double PathHighlightSeconds = 1.5;

    private sealed record RequirementLine(bool Met, bool IsNext, string Label, string Detail);

    private sealed record RewardLine(uint Icon, string Text, string Kind);

    private sealed record PathLine(uint RowId, string Name, QuestState State, bool IsTarget);

    private sealed class Model
    {
        public uint RowId;
        public int Version;
        public CatalogBundle? Bundle;
        public QuestRecord? Quest;
        public QuestState State;
        public string JournalPath = string.Empty;
        public string HeaderLine = string.Empty;
        public string StateText = string.Empty;
        public string? StateNote;
        public bool HasSnapshot;
        public bool Pinned;
        public readonly List<RequirementLine> Requirements = [];
        public readonly List<RewardLine> Rewards = [];
        public readonly List<PathLine> Path = [];
        public string? GiverName;
        public string? PlaceText;
        public string? CoordinateText;
        public string Provenance = string.Empty;
    }

    private readonly UiState ui;
    private readonly QueryRunner runner;
    private readonly GameLinks links;
    private readonly ITextureProvider textures;

    private readonly Model model = new() { RowId = uint.MaxValue, Version = -1 };
    private bool pinnedShown;

    // "Show path": the scroll is requested on two consecutive frames because ImGui clamps a scroll target against the
    // content size measured in the previous frame, which does not yet include a freshly selected quest's sections.
    private int pathScrollFrames;
    private double pathHighlightUntil;

    public DetailPane(UiState ui, QueryRunner runner, GameLinks links, ITextureProvider textures)
    {
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
    }

    public void Draw(SessionState session, CatalogBundle bundle, Vector2 size)
    {
        using var colors = Theme.PushNightPanel();
        using var child = ImRaii.Child("##detail", size, true);
        if (!child)
        {
            return;
        }

        if (ui.SelectedRowId is not { } rowId)
        {
            ImGui.TextDisabled(Strings.SelectQuest);
            return;
        }

        Refresh(session, bundle, rowId);
        if (ui.ScrollToPath)
        {
            ui.ScrollToPath = false;
            pathScrollFrames = PathScrollFrames;
            pathHighlightUntil = ImGui.GetTime() + PathHighlightSeconds;
        }

        if (model.Quest is not { } quest)
        {
            ImGui.TextDisabled(Strings.QuestNotInCatalog);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        DrawHeader(quest, scale);
        Section(Strings.Requirements);
        DrawRequirements();
        Section(Strings.Rewards);
        DrawRewards(scale);
        Section(Strings.Path, highlight: ImGui.GetTime() < pathHighlightUntil);
        if (pathScrollFrames > 0)
        {
            pathScrollFrames--;
            ImGui.SetScrollHereY(0f);
        }

        DrawPath(scale);
        Section(Strings.Giver);
        DrawGiver(quest);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled(model.Provenance);
    }

    private void DrawHeader(QuestRecord quest, float scale)
    {
        var radius = HeaderGlyphRadius * scale;
        var box = radius * 2.6f;
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(box, box));
        MoonGlyph.Draw(ImGui.GetWindowDrawList(), pos + new Vector2(box * 0.5f), radius, model.State);
        ImGui.SameLine();

        using var group = ImRaii.Group();
        ImGui.TextWrapped(quest.Name);
        ImGui.TextDisabled(model.JournalPath);
        ImGui.TextDisabled(model.HeaderLine);
        using (Theme.PushText(Theme.StateColor(model.State)))
        {
            ImGui.TextUnformatted(model.StateText);
        }

        if (model.StateNote is { } note)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(note);
        }

        if (model.Pinned)
        {
            ImGui.SameLine();
            using var moon = Theme.PushText(Theme.Moon);
            ImGui.TextUnformatted(Strings.Pinned);
        }
    }

    private void DrawRequirements()
    {
        if (!model.HasSnapshot)
        {
            ImGui.TextWrapped(Strings.RequirementsNeedSnapshot);
            return;
        }

        if (model.Requirements.Count == 0)
        {
            ImGui.TextDisabled(Strings.NoRequirements);
            return;
        }

        foreach (var line in model.Requirements)
        {
            using (Theme.PushText(line.Met ? Theme.Moon : Theme.Eclipse))
            {
                ImGui.TextUnformatted(line.Met ? Strings.Met : Strings.Unmet);
            }

            ImGui.SameLine();
            if (line.IsNext)
            {
                using (Theme.PushText(Theme.Moon))
                {
                    ImGui.TextUnformatted(Strings.NextStepMarker);
                }

                ImGui.SameLine();
            }

            using (Theme.PushText(line.IsNext ? Theme.Moon : Theme.Silver))
            {
                ImGui.TextUnformatted(line.Label);
            }

            if (line.Detail.Length > 0)
            {
                ImGui.SameLine();
                ImGui.TextDisabled(line.Detail);
            }
        }
    }

    private void DrawRewards(float scale)
    {
        if (model.Rewards.Count == 0)
        {
            ImGui.TextDisabled(Strings.NoRewards);
            return;
        }

        var iconSize = ImGui.GetTextLineHeight() + 4f * scale;
        foreach (var line in model.Rewards)
        {
            if (line.Icon != 0)
            {
                var wrap = textures.GetFromGameIcon(new GameIconLookup(line.Icon)).GetWrapOrEmpty();
                ImGui.Image(wrap.Handle, new Vector2(iconSize, iconSize));
                ImGui.SameLine();
            }
            else
            {
                ImGui.Dummy(new Vector2(iconSize, iconSize));
                ImGui.SameLine();
            }

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(line.Text);
            ImGui.SameLine();
            ImGui.TextDisabled(line.Kind);
        }
    }

    /// <summary>Vertical chain: a glyph per step joined by a thin Dusk line, the name clickable to select that quest.</summary>
    private void DrawPath(float scale)
    {
        if (model.Path.Count <= 1)
        {
            ImGui.TextDisabled(Strings.PathSingle);
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var radius = PathGlyphRadius * scale;
        var lineHeight = ImGui.GetTextLineHeight();
        var glyphBox = MathF.Max(lineHeight, radius * 2.4f);
        var previousCenter = Vector2.Zero;
        var hasPrevious = false;

        foreach (var step in model.Path)
        {
            using var id = ImRaii.PushId((int)step.RowId);
            var pos = ImGui.GetCursorScreenPos();
            var center = pos + new Vector2(glyphBox * 0.5f, lineHeight * 0.5f);
            if (hasPrevious)
            {
                dl.AddLine(previousCenter + new Vector2(0f, radius), center - new Vector2(0f, radius), Theme.DuskU32, 1f * scale);
            }

            ImGui.Dummy(new Vector2(glyphBox, lineHeight));
            MoonGlyph.Draw(dl, center, radius, step.State);
            ImGui.SameLine();
            if (ImGui.Selectable(step.Name, step.IsTarget))
            {
                ui.SelectedRowId = step.RowId;
            }

            previousCenter = center;
            hasPrevious = true;
        }
    }

    private void DrawGiver(QuestRecord quest)
    {
        if (model.GiverName is null)
        {
            ImGui.TextDisabled(Strings.NoGiver);
            return;
        }

        ImGui.TextUnformatted(model.GiverName);
        if (model.PlaceText is { } place)
        {
            ImGui.TextDisabled(place);
            if (model.CoordinateText is { } coords)
            {
                ImGui.SameLine();
                ImGui.TextDisabled(coords);
            }
        }

        using (ImRaii.Disabled(!links.CanFlagMap(quest)))
        {
            if (ImGui.SmallButton(Strings.FlagOnMap))
            {
                links.FlagMap(quest);
            }
        }

        ImGui.SameLine();
        var canOpen = GameLinks.CanOpenJournal(quest, model.State);
        using (ImRaii.Disabled(!canOpen))
        {
            if (ImGui.SmallButton(Strings.OpenJournal))
            {
                links.OpenJournal(quest);
            }
        }

        if (!canOpen && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(Strings.OpenJournalUnavailable);
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.LinkInChat))
        {
            links.PrintQuestLink(quest);
        }

        using (ImRaii.Disabled(model.CoordinateText is null))
        {
            if (ImGui.SmallButton(Strings.CopyCoordinates) && links.CoordinateText(quest) is { } coordinates)
            {
                ImGui.SetClipboardText(coordinates);
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(Strings.CopyCoordinatesTooltip);
        }
    }

    private static void Section(string title, bool highlight = false)
    {
        ImGui.Spacing();
        using (Theme.PushText(Theme.Moon, highlight))
        {
            ImGui.TextUnformatted(title);
        }

        using (ImRaii.PushColor(ImGuiCol.Separator, Theme.Moon, highlight))
        {
            ImGui.Separator();
        }
    }

    private void Refresh(SessionState session, CatalogBundle bundle, uint rowId)
    {
        var pinned = runner.IsPinned(rowId);
        if (model.RowId == rowId && model.Version == session.Version && ReferenceEquals(model.Bundle, bundle) && pinnedShown == pinned)
        {
            return;
        }

        pinnedShown = pinned;
        model.RowId = rowId;
        model.Version = session.Version;
        model.Bundle = bundle;
        model.Pinned = pinned;
        model.Requirements.Clear();
        model.Rewards.Clear();
        model.Path.Clear();
        model.StateNote = null;
        model.GiverName = null;
        model.PlaceText = null;
        model.CoordinateText = null;

        var quest = bundle.Catalog.GetByRowId(rowId);
        model.Quest = quest;
        if (quest is null)
        {
            return;
        }

        var snapshot = session.ViewedSnapshot;
        model.HasSnapshot = snapshot is not null;
        session.States.TryGetValue(rowId, out var evaluation);
        model.State = evaluation?.State ?? QuestState.Unknown;
        model.StateText = Strings.StateName(model.State);

        model.JournalPath = string.Format(CultureInfo.CurrentCulture, Strings.JournalPathFormat, quest.Journal.GenreName, quest.Journal.CategoryName);
        var jobName = quest.ClassJobCategory <= 1 ? Strings.JobAny : links.ClassJobCategoryName(quest.ClassJobCategory);
        if (jobName.Length == 0)
        {
            jobName = runner.JobShort(quest);
        }

        model.HeaderLine = string.Format(CultureInfo.CurrentCulture, Strings.HeaderLineFormat, bundle.Names.Expansion(quest.Expansion), quest.Level, jobName);

        if (evaluation is not null)
        {
            if (evaluation.ReadyOnJob is { } job)
            {
                model.StateNote = string.Format(CultureInfo.CurrentCulture, Strings.ReadyOnJobFormat, bundle.Names.ClassJobAbbreviation(job));
            }
            else if (evaluation.Sequence is { } sequence)
            {
                model.StateNote = string.Format(CultureInfo.CurrentCulture, Strings.AcceptedSequenceFormat, sequence);
            }

            foreach (var result in evaluation.Requirements)
            {
                model.Requirements.Add(new RequirementLine(result.Met, ReferenceEquals(result, evaluation.NextStep), Strings.RequirementName(result.Req.Kind), result.Detail));
            }
        }

        foreach (var reward in quest.Rewards)
        {
            var text = reward.Count > 1
                ? string.Format(CultureInfo.CurrentCulture, Strings.RewardCountFormat, reward.Name, reward.Count)
                : reward.Name;
            model.Rewards.Add(new RewardLine(reward.Icon, text, Strings.RewardKindName(reward.Kind)));
        }

        foreach (var step in PathFinder.PathTo(rowId, bundle.Catalog, session.States))
        {
            var name = bundle.Catalog.GetByRowId(step.RowId)?.Name ?? step.RowId.ToString(CultureInfo.InvariantCulture);
            model.Path.Add(new PathLine(step.RowId, name, step.State, step.RowId == rowId));
        }

        if (quest.Issuer is { } issuer)
        {
            model.GiverName = issuer.Name.Length > 0 ? issuer.Name : Strings.NoGiver;
            if (links.Map(issuer.MapId) is { } map)
            {
                model.PlaceText = map.Region.Length > 0 && map.Region != map.PlaceName
                    ? string.Format(CultureInfo.CurrentCulture, Strings.JournalPathFormat, map.Region, map.PlaceName)
                    : map.PlaceName;
            }

            if (links.MapCoordinates(quest) is { } coords)
            {
                model.CoordinateText = string.Format(CultureInfo.CurrentCulture, Strings.CoordinatesFormat, coords.X, coords.Y);
            }
        }

        model.Provenance = snapshot is null
            ? Strings.ProvenanceNoSnapshot
            : string.Format(
                CultureInfo.CurrentCulture,
                model.State == QuestState.Completed ? Strings.ProvenanceCompletedFormat : Strings.ProvenanceEvaluatedFormat,
                UiFormat.Time(snapshot.TakenUtc));
    }
}
