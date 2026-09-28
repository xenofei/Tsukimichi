using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Chains;
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

    private const int PathScrollFrames = 2;

    // Section icons, converted once (ToIconString allocates).
    private static readonly string RequirementsIcon = FontAwesomeIcon.Tasks.ToIconString();
    private static readonly string RewardsIcon = FontAwesomeIcon.Gift.ToIconString();
    private static readonly string MoonlitIcon = FontAwesomeIcon.Moon.ToIconString();
    private static readonly string PathIcon = FontAwesomeIcon.Route.ToIconString();
    private static readonly string GiverIcon = FontAwesomeIcon.MapMarkerAlt.ToIconString();
    private const double PathHighlightSeconds = 1.5;
    private const int MinFoldedRun = 2;

    // Header badge for QuestRecord.IconSpecial (seasonal events, promotions).
    private const string SeasonalBadgeTooltip = "Seasonal event quest";
    private const string SpecialBadgeTooltip = "Special";

    // Chain progress line under the header.
    private const string ChainFormat = "Chain: {0} · {1} of {2} done";
    private const string ChainNextLabel = "· next:";
    private const string ChainCompleteLabel = "· complete";
    private const string ChainNextTooltip = "Select the next quest in this chain";
    private const string ChainMoonTooltipFormat = "{0} of {1} quests done";

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
        public string? ChainText;
        public string? ChainNextName;
        public uint ChainNextRowId;
        public int ChainDone;
        public int ChainTotal;
        public float ChainFraction;
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
    private readonly IPluginLog? log;

    private readonly Model model = new() { RowId = uint.MaxValue, Version = -1 };
    private bool pinnedShown;

    // Folded completed runs the user opened, by run index; forgotten when another quest is selected.
    private readonly HashSet<int> expandedRuns = [];

    // Quests with shipped unique-reward entries, rebuilt when the shipped data instance changes.
    private UniqueRewardsData? uniqueData;
    private readonly HashSet<uint> uniqueQuests = [];

    // Named and derived chains, built once per catalog bundle.
    private ChainCatalog chains = ChainCatalog.Empty;
    private CatalogBundle? chainsBundle;

    // The "Mark as unique…" confirm popup and its eight-second Undo line.
    private readonly VerdictPrompt verdict = new(Strings.MarkUniquePopup);

    // "Show path": the scroll is requested on two consecutive frames because ImGui clamps a scroll target against the
    // content size measured in the previous frame, which does not yet include a freshly selected quest's sections.
    private int pathScrollFrames;
    private double pathHighlightUntil;

    /// <param name="log">Receives the chain catalog's warnings once per rebuild; null logs nothing.</param>
    public DetailPane(UiState ui, QueryRunner runner, GameLinks links, ITextureProvider textures, IPluginLog? log = null)
    {
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        this.log = log;
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

        ui.RecordWindow(UiRects.Detail);
        if (ui.SelectedRowId is not { } rowId)
        {
            EmptyState.Draw(Strings.SelectQuest);
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
            EmptyState.Draw(Strings.QuestNotInCatalog);
            return;
        }

        var width = ImGui.GetContentRegionAvail().X;
        DrawHeader(quest);

        var start = ImGui.GetCursorScreenPos();
        Section(Strings.Requirements, RequirementsIcon);
        DrawRequirements();
        ui.RecordSpan(UiRects.DetailRequirements, start, width);

        Section(Strings.Rewards, RewardsIcon);
        DrawRewards();
        if (Overrides is { } overrides)
        {
            Section(Strings.UniqueSection, MoonlitIcon);
            DrawUnique(overrides, rowId);
        }

        start = ImGui.GetCursorScreenPos();
        Section(Strings.Path, PathIcon, highlight: ImGui.GetTime() < pathHighlightUntil);
        if (pathScrollFrames > 0)
        {
            pathScrollFrames--;
            ImGui.SetScrollHereY(0f);
        }

        DrawPath();
        DrawUnlocks();
        ui.RecordSpan(UiRects.DetailPath, start, width);

        start = ImGui.GetCursorScreenPos();
        Section(Strings.Giver, GiverIcon);
        DrawGiver(quest);
        ui.RecordSpan(UiRects.DetailGiver, start, width);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled(model.Provenance);
    }

    /// <summary>
    /// The journal banner (<see cref="QuestRecord.Icon"/>) at the pane's width with the name on a Night strip and the
    /// state moon at its right; quests without a banner (or whose banner is still loading) get a raised Night card
    /// with the moon beside the name instead. The journal path, header line and state follow either way.
    /// </summary>
    private void DrawHeader(QuestRecord quest)
    {
        if (!DrawBanner(quest))
        {
            DrawHeaderCard(quest);
        }

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

        DrawChain();
    }

    /// <summary>
    /// "Chain: name · N of M done · next: quest" with a filling moon at its left; the next quest's name selects it.
    /// Nothing is drawn for a quest outside every chain.
    /// </summary>
    private void DrawChain()
    {
        if (model.ChainText is not { } text)
        {
            return;
        }

        var lineHeight = ImGui.GetTextLineHeight();
        var size = UiMetrics.InlineGlyphSize(lineHeight);
        MoonGlyph.DrawFillingInline(model.ChainFraction, size);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(string.Format(CultureInfo.CurrentCulture, ChainMoonTooltipFormat, model.ChainDone, model.ChainTotal));
        }

        // The glyph box is taller than a text line; centre the text on it.
        ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (size - lineHeight) * 0.5f);
        ImGui.TextUnformatted(text);
        ImGui.SameLine();
        if (model.ChainNextName is not { } next)
        {
            using var done = Theme.PushText(Theme.Moon);
            ImGui.TextUnformatted(ChainCompleteLabel);
            return;
        }

        ImGui.TextDisabled(ChainNextLabel);
        ImGui.SameLine();
        using (Theme.PushText(Theme.Moon))
        {
            ImGui.TextUnformatted(next);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            UiMetrics.Tooltip(ChainNextTooltip);
            if (ImGui.IsItemClicked())
            {
                ui.SelectedRowId = model.ChainNextRowId;
            }
        }
    }

    /// <summary>Banner image with the name overlaid; false when the quest has none or it is not loaded yet.</summary>
    private bool DrawBanner(QuestRecord quest)
    {
        if (quest.Icon == 0 || !textures.GetFromGameIcon(new GameIconLookup(quest.Icon)).TryGetWrap(out var wrap, out _) || wrap.Width <= 0 || wrap.Height <= 0)
        {
            return false;
        }

        var dl = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var height = MathF.Min(UiMetrics.BannerMaxHeight, width * wrap.Height / wrap.Width);
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);
        ImGui.Image(wrap.Handle, new Vector2(width, height));

        // Name strip: a Night gradient over the lower part of the image, the name in Silver at its left and the
        // large state moon at its right.
        var pad = UiMetrics.Px(8f);
        var radius = UiMetrics.HeaderMoonRadius;
        var moonBox = radius * 2.4f;
        var badge = quest.IconSpecial != 0 ? UiMetrics.BannerBadgeSize + pad : 0f;
        var textWrap = MathF.Max(UiMetrics.Px(40f), width - pad * 3f - moonBox - badge);
        var textHeight = ImGui.CalcTextSize(quest.Name, false, textWrap).Y;
        var stripHeight = MathF.Min(height, MathF.Max(textHeight + pad * 2f, moonBox + pad));
        var stripTop = max.Y - stripHeight;
        var fade = MathF.Min(UiMetrics.Px(28f), stripTop - min.Y);
        var solid = Theme.WithAlpha(Theme.Night, 0.84f);
        var clear = Theme.WithAlpha(Theme.Night, 0f);
        if (fade > 0f)
        {
            dl.AddRectFilledMultiColor(new Vector2(min.X, stripTop - fade), new Vector2(max.X, stripTop), clear, clear, solid, solid);
        }

        dl.AddRectFilled(new Vector2(min.X, stripTop), max, solid);
        dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), new Vector2(min.X + pad, max.Y - pad - textHeight), Theme.SilverU32, quest.Name, textWrap);
        MoonGlyph.Draw(dl, new Vector2(max.X - pad - moonBox * 0.5f, max.Y - stripHeight * 0.5f), radius, model.State);
        if (badge > 0f)
        {
            var size = UiMetrics.BannerBadgeSize;
            var badgeMin = new Vector2(max.X - pad - moonBox - pad - size, max.Y - stripHeight * 0.5f - size * 0.5f);
            DrawSpecialBadge(dl, quest, badgeMin, size);
        }

        ImGui.Spacing();
        return true;
    }

    /// <summary>
    /// The quest's special icon (<see cref="QuestRecord.IconSpecial"/>) drawn on the draw list at <paramref name="min"/>,
    /// with a tooltip naming what the badge means. Nothing is drawn while the texture is still loading.
    /// </summary>
    private void DrawSpecialBadge(ImDrawListPtr dl, QuestRecord quest, Vector2 min, float size)
    {
        if (!textures.GetFromGameIcon(new GameIconLookup(quest.IconSpecial)).TryGetWrap(out var wrap, out _))
        {
            return;
        }

        var max = min + new Vector2(size, size);
        dl.AddImage(wrap.Handle, min, max);
        if (ImGui.IsMouseHoveringRect(min, max))
        {
            UiMetrics.Tooltip(quest.Festival != 0 ? SeasonalBadgeTooltip : SpecialBadgeTooltip);
        }
    }

    /// <summary>Raised Night card with the state moon and the name, for quests without a banner.</summary>
    private void DrawHeaderCard(QuestRecord quest)
    {
        var dl = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = UiMetrics.Px(8f);
        var min = ImGui.GetCursorScreenPos();

        // Content first on its own channel, the card underneath on channel 0 once its height is known.
        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);
        ImGui.SetCursorScreenPos(min + new Vector2(pad, pad));
        using (ImRaii.Group())
        {
            var radius = UiMetrics.HeaderMoonRadius;
            var box = radius * 2.6f;
            var pos = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(box, box));
            MoonGlyph.Draw(dl, pos + new Vector2(box * 0.5f), radius, model.State);
            ImGui.SameLine();
            var badge = 0f;
            if (quest.IconSpecial != 0)
            {
                var size = UiMetrics.BannerBadgeSize;
                badge = size + pad;
                var badgeMin = ImGui.GetCursorScreenPos() + new Vector2(0f, (box - size) * 0.5f);
                ImGui.Dummy(new Vector2(size, box));
                DrawSpecialBadge(dl, quest, badgeMin, size);
                ImGui.SameLine();
            }

            using var wrap = ImRaii.TextWrapPos(ImGui.GetCursorPosX() + width - box - badge - pad * 3f);
            using var silver = Theme.PushText(Theme.Silver);
            ImGui.TextWrapped(quest.Name);
        }

        var max = new Vector2(min.X + width, ImGui.GetItemRectMax().Y + pad);
        dl.ChannelsSetCurrent(0);
        dl.AddRectFilled(min, max, Theme.NightRaisedU32, UiMetrics.Px(4f));
        dl.ChannelsMerge();
        ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y));
        ImGui.Spacing();
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

        var dl = ImGui.GetWindowDrawList();
        var lineHeight = ImGui.GetTextLineHeight();
        var radius = UiMetrics.RequirementMoonRadius;
        var box = MathF.Max(lineHeight, radius * 2.4f);
        foreach (var line in model.Requirements)
        {
            // Met is a full moon, unmet a new moon; the marks are moons like everything else here.
            var pos = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(box, lineHeight));
            MoonGlyph.Draw(dl, pos + new Vector2(box * 0.5f, lineHeight * 0.5f), radius, line.Met ? QuestState.Completed : QuestState.Blocked);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(line.Met ? Strings.MetTooltip : Strings.UnmetTooltip);
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
    private void DrawRewards()
    {
        if (model.Rewards.Count == 0)
        {
            ImGui.TextDisabled(Strings.NoRewards);
            return;
        }

        var iconSize = UiMetrics.DetailIconSize;
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

    /// <summary>
    /// The user's Moonlit verdict: restore an override, or vouch for a quest the shipped data does not list. "Mark as
    /// unique…" opens the shared <see cref="VerdictPrompt"/> (Shift and click, or hold, to confirm) and an Undo line
    /// follows for eight seconds.
    /// </summary>
    private void DrawUnique(IUniqueOverrides overrides, uint rowId)
    {
        if (overrides.Get(rowId) is { } stored)
        {
            using (Theme.PushText(stored.Unique ? Theme.Moon : Theme.Dusk))
            {
                ImGui.TextUnformatted(stored.Unique ? Strings.MarkedUniqueByYou : Strings.MarkedNotUniqueByYou);
            }

            if (stored.Note is { Length: > 0 } note)
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
                UiMetrics.Tooltip(Strings.RestoreOverrideTooltip);
            }
        }
        else if (model.HasUniqueEntries)
        {
            ImGui.TextDisabled(Strings.ListedInMoonlit);
        }
        else
        {
            ImGui.TextDisabled(Strings.NotListedInMoonlit);
            ImGui.SameLine();
            if (ImGui.SmallButton(Strings.MarkUnique))
            {
                verdict.Open(rowId, true, model.Quest?.Name ?? string.Empty);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.MarkUniqueTooltip);
            }
        }

        // Begun on every path so a popup opened for one quest is not orphaned when the selection moves on.
        verdict.Draw(overrides, UiMetrics.Scale);
        verdict.DrawUndo(overrides);
    }

    /// <summary>
    /// The chain grouped by expansion, completed runs folded behind a toggle, every visible glyph joined by a thin
    /// Dusk line; each name is clickable and selects that quest.
    /// </summary>
    private void DrawPath()
    {
        if (model.Path.Count <= 1)
        {
            ImGui.TextDisabled(Strings.PathSingle);
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var radius = UiMetrics.PathGlyphRadius;
        var lineHeight = ImGui.GetTextLineHeight();
        var glyphBox = MathF.Max(lineHeight, radius * 2.4f);
        var chain = default(Chain);

        foreach (var row in model.PathRows)
        {
            switch (row.Kind)
            {
                case PathRowKind.ExpansionHeader:
                    ImGui.TextDisabled(row.Text);
                    break;

                case PathRowKind.Step:
                    DrawStep(dl, row.Step!, radius, lineHeight, glyphBox, ref chain);
                    break;

                case PathRowKind.FoldedRun:
                {
                    var expanded = expandedRuns.Contains(row.RunIndex);
                    using var id = ImRaii.PushId(row.RunIndex);
                    BeginGlyphLine(dl, QuestState.Completed, radius, lineHeight, glyphBox, ref chain);
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
                        UiMetrics.Tooltip(expanded ? Strings.FoldedRunCollapseTooltip : Strings.FoldedRunExpandTooltip);
                    }

                    if (expanded)
                    {
                        foreach (var step in row.Run!)
                        {
                            DrawStep(dl, step, radius, lineHeight, glyphBox, ref chain);
                        }
                    }

                    break;
                }
            }
        }
    }

    /// <summary>The previous glyph of the chain: where it is and whether that step is done (its outgoing line is then lit).</summary>
    private struct Chain
    {
        public Vector2 Center;
        public bool Has;
        public bool Done;
    }

    private void DrawStep(ImDrawListPtr dl, PathLine step, float radius, float lineHeight, float glyphBox, ref Chain chain)
    {
        using var id = ImRaii.PushId((int)step.RowId);
        BeginGlyphLine(dl, step.State, radius, lineHeight, glyphBox, ref chain);
        if (ImGui.Selectable(step.Name, step.IsTarget))
        {
            ui.SelectedRowId = step.RowId;
        }
    }

    /// <summary>
    /// Glyph at the line's left joined to the previous glyph by a line that is Moon where the path is already walked
    /// (the previous step done) and Dusk where it is not; the cursor is left on the same line for the label.
    /// </summary>
    private static void BeginGlyphLine(ImDrawListPtr dl, QuestState state, float radius, float lineHeight, float glyphBox, ref Chain chain)
    {
        var pos = ImGui.GetCursorScreenPos();
        var center = pos + new Vector2(glyphBox * 0.5f, lineHeight * 0.5f);
        if (chain.Has)
        {
            dl.AddLine(
                chain.Center + new Vector2(0f, radius),
                center - new Vector2(0f, radius),
                chain.Done ? Theme.MoonU32 : Theme.DuskU32,
                chain.Done ? UiMetrics.Px(1.5f) : UiMetrics.Hairline);
        }

        ImGui.Dummy(new Vector2(glyphBox, lineHeight));
        MoonGlyph.Draw(dl, center, radius, state);
        ImGui.SameLine();
        chain.Center = center;
        chain.Has = true;
        chain.Done = state == QuestState.Completed;
    }

    /// <summary>Direct dependents of the selected quest: the quests it is a previous quest of, with their glyphs.</summary>
    private void DrawUnlocks()
    {
        ImGui.Spacing();
        ImGui.TextDisabled(Strings.UnlocksNext);
        if (model.Unlocks.Count == 0)
        {
            ImGui.TextDisabled(Strings.UnlocksNone);
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var radius = UiMetrics.PathGlyphRadius;
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
            UiMetrics.Tooltip(Strings.OpenJournalUnavailable);
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
            UiMetrics.Tooltip(Strings.CopyCoordinatesTooltip);
        }

        // Teleport through Lifestream: shown only when that plugin is loaded.
        if (links.TeleportAvailable)
        {
            ImGui.SameLine();
            var canTeleport = links.CanTeleport(quest);
            using (ImRaii.Disabled(!canTeleport))
            {
                if (ImGui.SmallButton(Strings.TeleportToGiver))
                {
                    links.TeleportToGiver(quest);
                }
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                var tip = links.TeleportBusy ? Strings.TeleportBusy
                    : links.NearestAetheryte(quest) is { } aetheryte ? aetheryte.Name
                    : Strings.TeleportNoAetheryte;
                UiMetrics.Tooltip(tip);
            }
        }
    }

    /// <summary>Section header: a small FontAwesome icon in Dusk, the title, then a Dusk rule; all Moon while highlighted.</summary>
    private static void Section(string title, string icon, bool highlight = false)
    {
        ImGui.Spacing();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (Theme.PushText(highlight ? Theme.Moon : Theme.Dusk))
        {
            ImGui.TextUnformatted(icon);
        }

        ImGui.SameLine();
        using (Theme.PushText(Theme.Moon, highlight))
        {
            ImGui.TextUnformatted(title);
        }

        using (ImRaii.PushColor(ImGuiCol.Separator, highlight ? Theme.Moon : Theme.Dusk))
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
        model.ChainText = null;
        model.ChainNextName = null;
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
        BuildChain(session, bundle, rowId);

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

    /// <summary>The chain line for a quest that belongs to one, with the chains rebuilt only when the bundle changes.</summary>
    private void BuildChain(SessionState session, CatalogBundle bundle, uint rowId)
    {
        if (!ReferenceEquals(chainsBundle, bundle))
        {
            chains = ChainCatalog.Build(bundle.Catalog, session.Curated);
            chainsBundle = bundle;
            if (log is not null)
            {
                foreach (var warning in chains.Warnings)
                {
                    log.Warning("Chains: {Warning}", warning);
                }
            }
        }

        if (chains.ForQuest(rowId) is not { } chain)
        {
            return;
        }

        var progress = ChainCatalog.Progress(chain, session.States);
        model.ChainDone = progress.Done;
        model.ChainTotal = progress.Total;
        model.ChainFraction = progress.Fraction;
        model.ChainText = string.Format(CultureInfo.CurrentCulture, ChainFormat, chain.Name, progress.Done, progress.Total);
        if (progress.NextRowId is { } next)
        {
            model.ChainNextRowId = next;
            model.ChainNextName = bundle.Catalog.GetByRowId(next)?.Name ?? next.ToString(CultureInfo.InvariantCulture);
        }
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
