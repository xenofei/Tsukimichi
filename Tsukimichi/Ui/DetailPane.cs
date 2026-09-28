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
using Tsukimichi.Core.Storage;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail pane for <see cref="UiState.SelectedRowId"/> on a Night panel: header, requirements, rewards, Moonlit
/// verdict, path (grouped by expansion, completed runs folded), what the quest unlocks next, giver, provenance.
/// Everything shown is materialized into a <see cref="Model"/> when the selection or the session version changes, so
/// drawing allocates nothing.
/// </summary>
public sealed class DetailPane
{
    public const int MaxUnlocks = 8;

    private const float HeaderGlyphRadius = 20f;
    private const float PathGlyphRadius = 7f;
    private const int PathScrollFrames = 2;
    private const double PathHighlightSeconds = 1.5;
    private const int MinFoldedRun = 2;
    private const int NoteLength = 120;

    private sealed record RequirementLine(bool Met, bool IsNext, string Label, string Detail);

    private sealed record RewardLine(RewardRef Reward, string Text, string Kind);

    private sealed record PathLine(uint RowId, string Name, QuestState State, bool IsTarget, byte Expansion);

    private enum PathRowKind
    {
        ExpansionHeader,
        Step,
        FoldedRun,
    }

    /// <summary>One drawn line of the Path section: an expansion header, a single step, or a folded run of completed steps.</summary>
    private sealed class PathRow(PathRowKind kind, string text, string? expandedText, PathLine? step, List<PathLine>? run, int runIndex)
    {
        public PathRowKind Kind { get; } = kind;
        public string Text { get; } = text;
        public string ExpandedText { get; } = expandedText ?? text;
        public PathLine? Step { get; } = step;
        public List<PathLine>? Run { get; } = run;
        public int RunIndex { get; } = runIndex;
    }

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
        public bool HasUniqueEntries;
        public readonly List<RequirementLine> Requirements = [];
        public readonly List<RewardLine> Rewards = [];
        public readonly List<PathLine> Path = [];
        public readonly List<PathRow> PathRows = [];
        public readonly List<PathLine> Unlocks = [];
        public string? UnlocksMore;
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

    // Folded completed runs the user opened, by run index; forgotten when another quest is selected.
    private readonly HashSet<int> expandedRuns = [];

    // Quests with shipped unique-reward entries, rebuilt when the shipped data instance changes.
    private UniqueRewardsData? uniqueData;
    private readonly HashSet<uint> uniqueQuests = [];

    private string noteBuffer = string.Empty;

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

    /// <summary>The user's unique-reward verdicts; null until the plugin attaches them, which hides the Moonlit section.</summary>
    public IUniqueOverrides? Overrides { get; set; }

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
        if (Overrides is { } overrides)
        {
            Section(Strings.UniqueSection);
            DrawUnique(overrides, rowId);
        }

        Section(Strings.Path, highlight: ImGui.GetTime() < pathHighlightUntil);
        if (pathScrollFrames > 0)
        {
            pathScrollFrames--;
            ImGui.SetScrollHereY(0f);
        }

        DrawPath(scale);
        DrawUnlocks(scale);
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

    /// <summary>Icon, name and kind per reward; hovering anywhere on the row shows the blown-up reward tooltip.</summary>
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
            using (ImRaii.Group())
            {
                if (line.Reward.Icon != 0)
                {
                    var wrap = textures.GetFromGameIcon(new GameIconLookup(line.Reward.Icon)).GetWrapOrEmpty();
                    ImGui.Image(wrap.Handle, new Vector2(iconSize, iconSize));
                }
                else
                {
                    ImGui.Dummy(new Vector2(iconSize, iconSize));
                }

                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(line.Text);
                ImGui.SameLine();
                ImGui.TextDisabled(line.Kind);
            }

            if (ImGui.IsItemHovered())
            {
                RewardTooltip.Draw(line.Reward, links, textures);
            }
        }
    }

    /// <summary>The user's Moonlit verdict: restore an override, or vouch for a quest the shipped data does not list.</summary>
    private void DrawUnique(IUniqueOverrides overrides, uint rowId)
    {
        if (overrides.Get(rowId) is { } verdict)
        {
            using (Theme.PushText(verdict.Unique ? Theme.Moon : Theme.Dusk))
            {
                ImGui.TextUnformatted(verdict.Unique ? Strings.MarkedUniqueByYou : Strings.MarkedNotUniqueByYou);
            }

            if (verdict.Note is { Length: > 0 } note)
            {
                ImGui.SameLine();
                ImGui.TextDisabled(note);
            }

            ImGui.SameLine();
            if (ImGui.SmallButton(Strings.RestoreOverride))
            {
                overrides.Clear(rowId);
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Strings.RestoreOverrideTooltip);
            }

            return;
        }

        if (model.HasUniqueEntries)
        {
            ImGui.TextDisabled(Strings.ListedInMoonlit);
            return;
        }

        ImGui.TextDisabled(Strings.NotListedInMoonlit);
        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.MarkUnique))
        {
            noteBuffer = string.Empty;
            ImGui.OpenPopup(Strings.MarkUniquePopup);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.MarkUniqueTooltip);
        }

        using var popup = ImRaii.Popup(Strings.MarkUniquePopup);
        if (!popup)
        {
            return;
        }

        ImGui.SetNextItemWidth(220f * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("##uniqueNote", Strings.MarkUniqueNoteHint, ref noteBuffer, NoteLength);
        if (ImGui.Button(Strings.MarkUniqueConfirm))
        {
            overrides.Set(rowId, true, noteBuffer);
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button(Strings.Cancel))
        {
            ImGui.CloseCurrentPopup();
        }
    }

    /// <summary>
    /// The chain grouped by expansion, completed runs folded behind a toggle, every visible glyph joined by a thin
    /// Dusk line; each name is clickable and selects that quest.
    /// </summary>
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

        foreach (var row in model.PathRows)
        {
            switch (row.Kind)
            {
                case PathRowKind.ExpansionHeader:
                    ImGui.TextDisabled(row.Text);
                    break;

                case PathRowKind.Step:
                    DrawStep(dl, row.Step!, radius, lineHeight, glyphBox, scale, ref previousCenter, ref hasPrevious);
                    break;

                case PathRowKind.FoldedRun:
                {
                    var expanded = expandedRuns.Contains(row.RunIndex);
                    using var id = ImRaii.PushId(row.RunIndex);
                    var center = BeginGlyphLine(dl, QuestState.Completed, radius, lineHeight, glyphBox, scale, ref previousCenter, ref hasPrevious);
                    using (Theme.PushText(Theme.Dusk))
                    {
                        if (ImGui.Selectable(expanded ? row.ExpandedText : row.Text))
                        {
                            if (!expandedRuns.Remove(row.RunIndex))
                            {
                                expandedRuns.Add(row.RunIndex);
                            }
                        }
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(expanded ? Strings.FoldedRunCollapseTooltip : Strings.FoldedRunExpandTooltip);
                    }

                    previousCenter = center;
                    if (expanded)
                    {
                        foreach (var step in row.Run!)
                        {
                            DrawStep(dl, step, radius, lineHeight, glyphBox, scale, ref previousCenter, ref hasPrevious);
                        }
                    }

                    break;
                }
            }
        }
    }

    private void DrawStep(ImDrawListPtr dl, PathLine step, float radius, float lineHeight, float glyphBox, float scale, ref Vector2 previousCenter, ref bool hasPrevious)
    {
        using var id = ImRaii.PushId((int)step.RowId);
        var center = BeginGlyphLine(dl, step.State, radius, lineHeight, glyphBox, scale, ref previousCenter, ref hasPrevious);
        if (ImGui.Selectable(step.Name, step.IsTarget))
        {
            ui.SelectedRowId = step.RowId;
        }

        previousCenter = center;
    }

    /// <summary>Glyph at the line's left joined to the previous glyph, cursor left on the same line for the label.</summary>
    private static Vector2 BeginGlyphLine(ImDrawListPtr dl, QuestState state, float radius, float lineHeight, float glyphBox, float scale, ref Vector2 previousCenter, ref bool hasPrevious)
    {
        var pos = ImGui.GetCursorScreenPos();
        var center = pos + new Vector2(glyphBox * 0.5f, lineHeight * 0.5f);
        if (hasPrevious)
        {
            dl.AddLine(previousCenter + new Vector2(0f, radius), center - new Vector2(0f, radius), Theme.DuskU32, 1f * scale);
        }

        ImGui.Dummy(new Vector2(glyphBox, lineHeight));
        MoonGlyph.Draw(dl, center, radius, state);
        ImGui.SameLine();
        hasPrevious = true;
        return center;
    }

    /// <summary>Direct dependents of the selected quest: the quests it is a previous quest of, with their glyphs.</summary>
    private void DrawUnlocks(float scale)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(Strings.UnlocksNext);
        if (model.Unlocks.Count == 0)
        {
            ImGui.TextDisabled(Strings.UnlocksNone);
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var radius = PathGlyphRadius * scale;
        var lineHeight = ImGui.GetTextLineHeight();
        var glyphBox = MathF.Max(lineHeight, radius * 2.4f);
        foreach (var line in model.Unlocks)
        {
            using var id = ImRaii.PushId((int)line.RowId);
            var pos = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(glyphBox, lineHeight));
            MoonGlyph.Draw(dl, pos + new Vector2(glyphBox * 0.5f, lineHeight * 0.5f), radius, line.State);
            ImGui.SameLine();
            if (ImGui.Selectable(line.Name))
            {
                ui.SelectedRowId = line.RowId;
            }
        }

        if (model.UnlocksMore is { } more)
        {
            ImGui.TextDisabled(more);
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

        if (model.RowId != rowId)
        {
            expandedRuns.Clear();
        }

        pinnedShown = pinned;
        model.RowId = rowId;
        model.Version = session.Version;
        model.Bundle = bundle;
        model.Pinned = pinned;
        model.Requirements.Clear();
        model.Rewards.Clear();
        model.Path.Clear();
        model.PathRows.Clear();
        model.Unlocks.Clear();
        model.UnlocksMore = null;
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
        model.HasUniqueEntries = HasShippedUniqueEntry(session.UniqueRewards, rowId);

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
            model.Rewards.Add(new RewardLine(reward, text, Strings.RewardKindName(reward.Kind)));
        }

        foreach (var step in PathFinder.PathTo(rowId, bundle.Catalog, session.States))
        {
            var stepQuest = bundle.Catalog.GetByRowId(step.RowId);
            var name = stepQuest?.Name ?? step.RowId.ToString(CultureInfo.InvariantCulture);
            model.Path.Add(new PathLine(step.RowId, name, step.State, step.RowId == rowId, stepQuest?.Expansion ?? quest.Expansion));
        }

        BuildPathRows(bundle);
        BuildUnlocks(session, bundle, quest);

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

    /// <summary>
    /// Groups <see cref="Model.Path"/> by expansion under a header each, and folds every run of at least
    /// <see cref="MinFoldedRun"/> consecutive completed steps into one toggle row. The target and every step that is
    /// not completed stay listed.
    /// </summary>
    private void BuildPathRows(CatalogBundle bundle)
    {
        if (model.Path.Count <= 1)
        {
            return;
        }

        var rows = model.PathRows;
        List<PathLine>? run = null;
        var runIndex = 0;
        byte? expansion = null;

        void Flush()
        {
            if (run is null)
            {
                return;
            }

            if (run.Count >= MinFoldedRun)
            {
                var collapsed = string.Format(CultureInfo.CurrentCulture, Strings.FoldedRunCollapsedFormat, run.Count);
                var expanded = string.Format(CultureInfo.CurrentCulture, Strings.FoldedRunExpandedFormat, run.Count);
                rows.Add(new PathRow(PathRowKind.FoldedRun, collapsed, expanded, null, run, runIndex++));
            }
            else
            {
                foreach (var step in run)
                {
                    rows.Add(new PathRow(PathRowKind.Step, step.Name, null, step, null, -1));
                }
            }

            run = null;
        }

        foreach (var line in model.Path)
        {
            if (expansion != line.Expansion)
            {
                Flush();
                expansion = line.Expansion;
                var name = bundle.Names.Expansion(line.Expansion);
                rows.Add(new PathRow(PathRowKind.ExpansionHeader, name.Length > 0 ? name : Strings.ExpansionShort(line.Expansion), null, null, null, -1));
            }

            if (line.State == QuestState.Completed && !line.IsTarget)
            {
                (run ??= []).Add(line);
            }
            else
            {
                Flush();
                rows.Add(new PathRow(PathRowKind.Step, line.Name, null, line, null, -1));
            }
        }

        Flush();
    }

    /// <summary>Quests that list the selected one among their previous quests, in catalog order, capped at <see cref="MaxUnlocks"/>.</summary>
    private void BuildUnlocks(SessionState session, CatalogBundle bundle, QuestRecord quest)
    {
        if (session.Index is not { } index)
        {
            return;
        }

        var more = 0;
        foreach (var dependentId in index.Dependents(quest.RowId))
        {
            // The index also lists quests that merely lock on this one; only a true prerequisite is an unlock.
            if (bundle.Catalog.GetByRowId(dependentId) is not { } dependent || Array.IndexOf(dependent.PreviousQuests.QuestIds, quest.RowId) < 0)
            {
                continue;
            }

            if (model.Unlocks.Count >= MaxUnlocks)
            {
                more++;
                continue;
            }

            var state = session.States.TryGetValue(dependentId, out var evaluation) ? evaluation.State : QuestState.Unknown;
            model.Unlocks.Add(new PathLine(dependentId, dependent.Name, state, false, dependent.Expansion));
        }

        if (more > 0)
        {
            model.UnlocksMore = string.Format(CultureInfo.CurrentCulture, Strings.AndMoreFormat, more);
        }
    }

    private bool HasShippedUniqueEntry(UniqueRewardsData data, uint rowId)
    {
        if (!ReferenceEquals(uniqueData, data))
        {
            uniqueData = data;
            uniqueQuests.Clear();
            foreach (var entry in data.Entries)
            {
                uniqueQuests.Add(entry.QuestRowId);
            }
        }

        return uniqueQuests.Contains(rowId);
    }
}
