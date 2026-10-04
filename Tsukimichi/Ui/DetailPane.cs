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
using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Travel;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unlocks;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail pane for <see cref="UiState.SelectedRowId"/> as a card stack on a Night panel (ui-revamp §2.5, game UX
/// panel finding 8): a hero at most 96 px tall (the journal banner cover-cropped under a scrim, the state pill and the
/// special badge, the name and a caption line; the Night card when the artwork is hidden or missing), the "Not yet"
/// callout for a quest the character cannot take (DetailPane.Unmet.cs), then the
/// Requirements card first with one marker on the blocking line, Rewards as tiles, the Moonlit verdict, the Path card (the chain
/// line once, then the star chart, <see cref="PathChart"/>), the Giver card; under the scrolling stack a sticky action
/// bar: a row of labelled travel and automation pills (Go to giver, Teleport, Walk, Start Questionable, Run with
/// AutoDuty; DetailPane.Actions.cs; their live status is in the status bar), then round buttons for Pin, Show path, Route to this (the
/// unlock route window), Flag on map, Link in chat, Copy coordinates, Open journal and Report, and a plain provenance line. Every action is a
/// focusable item, so the pane works without a mouse (accessibility A6). Everything shown is materialized when the
/// selection or the session version changes, so drawing allocates nothing.
/// <para>
/// At Flair Full and Quiet (feature plan V4) the hero is the Moon Road hero and the cards are open sections
/// (DetailPane.Hero.cs), with a moon-road divider between them; Plain keeps the look above. Every quest has a banner at
/// every level, from the fallback chain.
/// </para>
/// </summary>
public sealed partial class DetailPane
{
    public const int MaxUnlocks = 8;

    private const int PathScrollFrames = 2;
    private const float MarkScale = 0.72f;

    // Section icons, converted once (ToIconString allocates).
    private static readonly string RequirementsIcon = FontAwesomeIcon.Tasks.ToIconString();
    private static readonly string RewardsIcon = FontAwesomeIcon.Gift.ToIconString();
    private static readonly string MoonlitIcon = FontAwesomeIcon.Moon.ToIconString();
    private static readonly string PathIcon = FontAwesomeIcon.Route.ToIconString();
    private static readonly string GiverIcon = FontAwesomeIcon.MapMarkerAlt.ToIconString();

    // Refresh the "Checked just now" line this often while nothing else changes.
    private const double ProvenanceRefreshSeconds = 30.0;

    // "Copied · paste it into a GitHub issue" in place of the provenance line for a few seconds after Report.
    private const double ReportNoteSeconds = 5.0;

    /// <summary>
    /// One requirement line. An unmet numeric requirement carries its gap meter (<paramref name="GapFraction"/>, 0 to 1,
    /// and <paramref name="GapText"/>, "52 → 56"); an unmet one a quest clears carries that quest
    /// (<paramref name="JumpRowId"/>, 0 for none) and the jump button's tooltip.
    /// </summary>
    private sealed record RequirementLine(bool Met, bool IsNext, string Label, string Detail, float GapFraction = 0f, string? GapText = null, uint JumpRowId = 0, string? JumpTooltip = null)
    {
        /// <summary>The game icon after the check or cross (UI-5e, I17): the job's, the previous quest's marker, the society's emblem; none for most others.</summary>
        public GameIconRef Icon { get; init; }
    }

    /// <summary>A reward tile: unique rewards wear the gold ring and crescent; a mark says when the store sells it or a duty drops it.</summary>
    private sealed record RewardTile(RewardRef Reward, bool Unique, string? Mark);

    private sealed class Model
    {
        public uint RowId;
        public int Version;
        public CatalogBundle? Bundle;

        /// <summary>The unlock index's revision and Sprout mode's reach the Path stations' "Opens …" lines were built with.</summary>
        public int UnlocksRevision = -1;
        public byte UnlocksReach;

        /// <summary>The sheet icons the requirement lines were built with (null while they are read).</summary>
        public IPaneIconSheets? IconSheets;
        public QuestRecord? Quest;
        public QuestEvaluation? Evaluation;
        public QuestState State;

        /// <summary>The quest's name as the spoiler shield prints it (<see cref="Core.Query.SpoilerMask.DisplayName(QuestRecord)"/>).</summary>
        public string DisplayName = string.Empty;

        /// <summary>The shield masks the name; the pane offers "Reveal this name".</summary>
        public bool NameMasked;

        /// <summary>Provenance of a refiled or removed quest ("Filed under … (rule 4: …)", "Removed from the game in patch 6.3"); null for an ordinary quest.</summary>
        public string? FilingLine;

        /// <summary>"Expansion · Lv N · job", the hero's caption.</summary>
        public string HeaderLine = string.Empty;

        /// <summary><see cref="HeaderLine"/> cut at its separators, laid out as whole segments that wrap (L5).</summary>
        public string[] HeaderSegments = [];

        /// <summary>The journal path's genre and category, laid out as whole segments that wrap on the "›" (L5).</summary>
        public string[] JournalSegments = [];

        /// <summary>The "Not yet" callout (L8) for a quest the character cannot take on the current job; null otherwise.</summary>
        public NotYetCallout? Callout;

        /// <summary>The callout's second line: who a quest on another path is for (D1); null for none.</summary>
        public string? CalloutDetail;

        /// <summary>How many requirements are unmet; the Requirements caption turns to the unmet tone above 0.</summary>
        public int UnmetCount;
        public string StateName = string.Empty;

        /// <summary>What the state pill cannot say: the blocker, the step, the date; empty when the state says it all.</summary>
        public string StatusReason = string.Empty;

        /// <summary>" · " and <see cref="StatusReason"/>, after the state word on the Moon Road state line; empty for none.</summary>
        public string StatusTail = string.Empty;
        public string? StateNote;

        /// <summary>The job a Ready on another job quest is ready on, for its medal's badge (feature plan v6 G1); 0 otherwise.</summary>
        public byte ReadyOnJob;

        /// <summary>"Note: …" from <c>curated/quirks.json</c>, drawn under the requirements; null for a quest without one.</summary>
        public string? QuirkNote;
        /// <summary>The chain's name on the Path card's chain line; null for a quest outside every chain.</summary>
        public string? ChainText;

        /// <summary>"Next in chain:", "Still open earlier:", "Last quest in this chain" or "Chain complete" (<see cref="ChainLine.Label"/>).</summary>
        public string ChainLabel = string.Empty;

        /// <summary>The chain bar's tooltip, the totals ("16 of 47 quests done").</summary>
        public string ChainTooltip = string.Empty;
        public ChainNextKind ChainKind;

        /// <summary>The quest the chain line links to, through the spoiler shield; null when it links to none.</summary>
        public string? ChainNextName;
        public uint ChainNextRowId;
        public float ChainFraction;

        /// <summary>The chain's first quest: the bar's motion key, so it eases on a completion but not on a move to another chain.</summary>
        public uint ChainKeyId;
        public bool HasSnapshot;
        public bool Pinned;
        public bool HasUniqueEntries;
        public readonly List<RequirementLine> Requirements = [];
        public string RequirementsCaption = string.Empty;
        public readonly List<RewardTile> Rewards = [];
        public string RewardsCaption = string.Empty;
        public string? GiverName;
        public string? CoordinateText;

        /// <summary>"Region › Place (x, y)" for the Giver card; null when the giver's map is unknown.</summary>
        public string? PlaceLine;
    }

    private readonly UiState ui;
    private readonly QueryRunner runner;
    private readonly GameLinks links;
    private readonly ITextureProvider textures;
    private readonly IPluginLog? log;
    private readonly PathChart chart;

    private readonly Model model = new() { RowId = uint.MaxValue, Version = -1 };
    private bool pinnedShown;

    // Quests with shipped unique-reward entries (and the entries per quest), rebuilt when the shipped data instance changes.
    private UniqueRewardsData? uniqueData;
    private readonly Dictionary<uint, List<UniqueRewardEntry>> uniqueByQuest = [];

    // The "Mark as unique…" confirm popup and its eight-second Undo line.
    private readonly VerdictPrompt verdict = new(Strings.MarkUniquePopup);

    // "Show path": the scroll is requested on two consecutive frames because ImGui clamps a scroll target against the
    // content size measured in the previous frame, which does not yet include a freshly selected quest's sections.
    private int pathScrollFrames;
    private double reportNoteUntil;
    private uint reportNoteRowId;

    /// <param name="log">Receives a failed diagnostic copy; null uses the plugin log.</param>
    public DetailPane(UiState ui, QueryRunner runner, GameLinks links, ITextureProvider textures, IPluginLog? log = null)
    {
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        this.log = log;
        chart = new PathChart(RevealRow) { OpensOf = id => runner.Unlocks?.Places(id, runner.UnlockReach) ?? string.Empty };
    }

    /// <summary>The user's unique-reward verdicts; null until the plugin attaches them, which hides the Moonlit card.</summary>
    public IUniqueOverrides? Overrides { get; set; }

    /// <summary>Composes the "Report" diagnostic block; null until the plugin attaches it, which hides the button.</summary>
    public DiagnosticBuilder? Diagnostics { get; set; }

    public void Draw(SessionState session, CatalogBundle bundle, Vector2 size)
    {
        using var colors = Theme.PushNightPanel();
        using var child = ImRaii.Child("##detail", size, true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (!child)
        {
            return;
        }

        ui.RecordWindow(UiRects.Detail);
        tier = DetailTiers.For(MathF.Round(ImGui.GetWindowWidth() / MathF.Max(0.01f, UiMetrics.Scale)));
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
            chart.RequestScrollToTarget(pulse: true);
        }

        if (model.Quest is not { } quest)
        {
            if (EmptyState.DrawWithAction(Strings.EmptyNotInCatalogHeading, Strings.EmptyNotInCatalogBody, Strings.EmptyClearSelection) == EmptyState.ActionClicked)
            {
                ui.SelectedRowId = null;
            }

            return;
        }

        var detailHeight = ImGui.GetContentRegionAvail().Y;
        PrepareTravel(session, quest);
        var bar = ActionBarHeight();
        using (ImRaii.PushColor(ImGuiCol.ChildBg, Vector4.Zero))
        using (var body = ImRaii.Child("##detailBody", new Vector2(0f, MathF.Max(UiMetrics.Px(40f), detailHeight - bar)), false))
        {
            if (body)
            {
                DrawBody(session, quest, rowId, detailHeight);
            }
        }

        var barTop = ImGui.GetCursorScreenPos().Y;
        DrawActionBar(quest, rowId);
        DrawProvenance(session);

        // The bar runs to the pane's bottom edge; the floating layers sit above it.
        var paneMin = ImGui.GetWindowPos();
        actionBarRect = new ScreenRect(new Vector2(paneMin.X, barTop), paneMin + ImGui.GetWindowSize());
        actionBarFrame = ImGui.GetFrameCount();
    }

    private ScreenRect actionBarRect;
    private int actionBarFrame = -1;

    /// <summary>
    /// The sticky action bar's rectangle on screen this frame (empty when the pane did not draw one): the floating
    /// layers keep above it (<see cref="FloatingLayers"/>), so the dock never covers Pin, Route or the travel pills.
    /// </summary>
    public ScreenRect ActionBarRect => actionBarFrame == ImGui.GetFrameCount() ? actionBarRect : default;

    private void DrawBody(SessionState session, QuestRecord quest, uint rowId, float detailHeight)
    {
        var width = ImGui.GetContentRegionAvail().X;
        bodyRight = ImGui.GetCursorScreenPos().X + width;

        // The sections are cards at Full (gilt brass) and Quiet (tonal planes), and heading rows on the pane at Plain
        // (docs/design/flair-v13 §1, "Card frame"): content wraps at the card's inner edge, or the body's at Plain.
        cardRight = bodyRight - UiMetrics.Px(Theme.Spacing.CardPad.X);
        DrawHero(quest);
        DrawNotYet(quest);
        DrawUnderHero(session, rowId);
        if (Theme.HeroStyle == HeroStyle.Banner)
        {
            MoonRoadDivider();
        }

        Gap();
        var start = ImGui.GetCursorScreenPos();
        BeginSection("##requirements", Strings.Requirements, RequirementsIcon, model.RequirementsCaption, model.UnmetCount > 0 ? Theme.DangerText : Theme.Surface.TextSecondary);
        DrawRequirements(start.X);
        EndSection();
        ui.RecordItem(UiRects.DetailRequirements);

        // How you'll clear it (1.19.0, C7): after Requirements, before Rewards, only for a quest that involves a duty.
        DrawClearSection(session, quest);

        // Rewards then Unlocks, a pair of sections in one rhythm (RewardSplit keeps them apart); neither draws an empty header.
        if (HasExpAndGil(quest) || model.Rewards.Count > 0)
        {
            Gap();
            BeginSection("##rewards", Strings.Rewards, RewardsIcon, model.RewardsCaption, Theme.Surface.TextSecondary);
            if (DrawExpAndGil(quest))
            {
                DrawExpAdvice(session, quest);
            }

            if (model.Rewards.Count > 0)
            {
                DrawRewards(cardRight);
            }

            EndSection();
        }

        DrawUnlocksSection(session, quest);
        DrawHandInSection(session, quest);

        if (Overrides is { } overrides)
        {
            Gap();
            BeginSection("##moonlit", Strings.UniqueSection, MoonlitIcon);
            DrawUnique(overrides, rowId);
            EndSection();
        }

        Gap();
        if (pathScrollFrames > 0)
        {
            pathScrollFrames--;
            ImGui.SetScrollHereY(0f);
        }

        var pad = UiMetrics.Px(10f);
        BeginSection("##path", Strings.Path, PathIcon, chart.HeaderCaption, Theme.Surface.TextSecondary, chart.HeaderTooltip);
        DrawPathNext();
        DrawChain();

        // The chart ends at the card's inner edge, or a gutter short of the body's at Plain (the minimap draws in the gutter).
        var chartRight = Theme.Flair == Flair.Plain ? bodyRight - pad : cardRight;
        chart.Draw(MathF.Max(1f, chartRight - ImGui.GetCursorScreenPos().X), 0.4f * detailHeight, pad);
        DrawQuestMapButton(rowId);
        EndSection();
        ui.RecordItem(UiRects.DetailPath);
        DrawDuties(session, quest);

        Gap();
        BeginSection("##giver", Strings.Giver, GiverIcon);
        DrawGiver();
        EndSection();
        ui.RecordItem(UiRects.DetailGiver);
        DrawJournalCard(session, quest);
        DrawCollector(session, quest);
        Gap();
    }

    /// <summary>The air between two sections: the level's gap (12 at Full, 8 at Quiet, 4 at Plain) less ImGui's own spacing.</summary>
    private static void Gap() =>
        ImGui.Dummy(new Vector2(0f, MathF.Max(0f, UiMetrics.Px(Theme.Spacing.Gap) - (2f * ImGui.GetStyle().ItemSpacing.Y))));

    /// <summary>The side of a Rewards tile and of an Unlocks row's icon well, so the two sections read as a pair.</summary>
    private static float PairTile => MathF.Max(UiMetrics.Px(40f), UiMetrics.Icon(34f) + UiMetrics.Px(8f));

    /// <summary>The gap between two rows of Rewards tiles, and between two Unlocks rows.</summary>
    private static float PairGap => UiMetrics.Px(6f);

    /// <summary>
    /// A card header's caption (0.85×): right-aligned on the title line, draw-list only, while it clears the title;
    /// else on a line of its own under the title, wrapped between words (L5: "RequirementsAll met" collided).
    /// </summary>
    private static void CardCaption(string caption, float cardRight, Vector4 color)
    {
        if (caption.Length == 0)
        {
            return;
        }

        var titleMin = ImGui.GetItemRectMin();
        var titleMax = ImGui.GetItemRectMax();
        var titleHeight = titleMax.Y - titleMin.Y;
        using var role = Typography.Caption();
        var size = ImGui.GetFontSize();
        var width = ImGui.CalcTextSize(caption).X;
        var right = cardRight - UiMetrics.Px(Theme.Spacing.CardPad.X);
        var x = right - width;
        if (x >= titleMax.X + UiMetrics.Px(8f))
        {
            ImGui.GetWindowDrawList().AddText(new Vector2(x, titleMin.Y + ((titleHeight - size) * 0.5f)), Theme.U32(color), caption);
            return;
        }

        TextFlow.Wrapped(caption, MathF.Max(1f, right - ImGui.GetCursorScreenPos().X), Theme.U32(color));
    }

    // ------------------------------------------------------------------ hero

    /// <summary>
    /// The hero at the Decoration level (docs/design/flair-v13 §1, "Banners"; DetailPane.Hero.cs): at Full the
    /// night-graded banner from the fallback chain with the medal rising over its edge and the title on the art; at
    /// Quiet a title block over a hairline, then a 52 px medal plate; at Plain the name and path, then a key-value list
    /// (State, Level, Giver) with a 12 px glyph and no banner.
    /// </summary>
    private void DrawHero(QuestRecord quest)
    {
        switch (Theme.HeroStyle)
        {
            case HeroStyle.Banner:
                DrawMoonRoadHero(quest);
                break;
            case HeroStyle.Plate:
                DrawPlateHero(quest);
                break;
            default:
                DrawLedgerHero(quest);
                break;
        }
    }

    private BlockerNames BlockerNamesOf() => lastNames ?? BlockerNames.Default;

    // The session's names and states as of the last refresh, for the state tooltip's blocker line.
    private BlockerNames? lastNames;
    private IReadOnlyDictionary<uint, QuestEvaluation>? lastStates;

    // The spoiler shield and the catalog as of the last refresh, for the hero banner's shield (a donor's own state).
    private Core.Query.SpoilerMask? lastSpoilers;
    private QuestCatalog? lastCatalog;

    /// <summary>A state's text tone for pills and captions: the palette's status word (<see cref="Theme.StateText"/>, at least 4.5 : 1).</summary>
    private static Vector4 StateTextColor(QuestState state) => Theme.StateText(state);

    /// <summary>
    /// The quest's special icon (<see cref="QuestRecord.IconSpecial"/>) drawn on the draw list at <paramref name="min"/>;
    /// false (nothing drawn) while the texture is still loading or when the game has no such icon.
    /// </summary>
    private bool DrawSpecialBadge(ImDrawListPtr dl, QuestRecord quest, Vector2 min, float size)
    {
        if (!textures.TryGetFromGameIcon(new GameIconLookup(quest.IconSpecial), out var badge) || !badge.TryGetWrap(out var wrap, out _))
        {
            return false;
        }

        dl.AddImage(wrap.Handle, min, min + new Vector2(size, size));
        return true;
    }

    /// <summary>What the special badge means.</summary>
    private static string BadgeTooltip(QuestRecord quest) => quest.Festival != 0 ? Strings.DetailSeasonalBadgeTooltip : Strings.DetailSpecialBadgeTooltip;

    /// <summary>
    /// Under the hero: the name-reveal line when the shield masks it, what the pill cannot say (the blocker, the step,
    /// the job that can take it), the journal path and the filing line, the Questionable cross-check, then what the
    /// game's offers say, a full journal and Switch gearset (1.19.0, DetailPane.RightAnswers.cs).
    /// </summary>
    private void DrawUnderHero(SessionState session, uint rowId)
    {
        if (model.NameMasked)
        {
            TextFlow.Wrapped(Strings.SpoilerMaskedNote, RoomTo(bodyRight), Theme.U32(Theme.Surface.TextDisabled));
            SameLineOrWrap(SmallButtonWidth(Strings.SpoilerRevealName), bodyRight);
            if (ImGui.SmallButton(Strings.SpoilerRevealName))
            {
                session.RevealName(rowId);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.SpoilerRevealNameTooltip);
            }
        }

        // The "Not yet" callout already says why a quest cannot be taken (L8); the status line is for the rest. The
        // banner hero spells the state out under the rising medal; the plate and the ledger say it themselves.
        if (model.Callout is null && Theme.HeroStyle == HeroStyle.Banner)
        {
            DrawStateLine();
        }

        // The journal path wraps on its "›", never inside a name (L5); the plate and the ledger carry it in their title block.
        if (Theme.HeroStyle == HeroStyle.Banner)
        {
            SegmentFlow(model.JournalSegments, JournalSeparator, RoomTo(bodyRight), Theme.Surface.TextDisabled);
        }

        if (model.FilingLine is { } filing)
        {
            TextFlow.Wrapped(filing, RoomTo(bodyRight), Theme.U32(Theme.Surface.TextSecondary));
        }

        if (model.Quest is { } quest)
        {
            DrawQuestionableLine(session, quest);
            DrawRightAnswers(session, quest);
        }
    }

    /// <summary>
    /// Selects a path step, an alternative, an unlock or the chain's next quest. A row the table already lists is
    /// selected in place (the table scrolls to it; the tab, scope and filters stay as they are). Otherwise it is
    /// revealed the way the other panes do: the Journal tab scoped to its genre with the narrowing filters cleared, so
    /// the table shows the row wherever the step lives. A row id the catalog does not know is selected plainly so the
    /// detail pane can say so.
    /// </summary>
    private void RevealRow(uint rowId)
    {
        if (model.Bundle?.Catalog.GetByRowId(rowId) is not { } quest || IsListed(rowId))
        {
            ui.SelectedRowId = rowId;
            return;
        }

        ui.Reveal(quest);
    }

    /// <summary>Whether the table's current rows hold <paramref name="rowId"/>.</summary>
    private bool IsListed(uint rowId)
    {
        var rows = runner.Rows;
        for (var i = 0; i < rows.Length; i++)
        {
            if (rows[i].Quest.RowId == rowId)
            {
                return true;
            }
        }

        return false;
    }

    // ------------------------------------------------------------------ cards

    /// <summary>
    /// Every requirement with its check or cross; the one blocking the quest is the only marked row: a 2 px gold bar
    /// at the card's left edge and its label in gold (one marker, game UX panel finding 8). The curated quirk's
    /// "Note: …" follows.
    /// </summary>
    private void DrawRequirements(float cardLeft)
    {
        if (!model.HasSnapshot)
        {
            TextFlow.Wrapped(Strings.RequirementsNeedSnapshot, RoomTo(cardRight), Theme.U32(Theme.Surface.TextSecondary));
        }
        else if (model.Requirements.Count == 0)
        {
            ImGui.TextDisabled(Strings.NoRequirements);
        }
        else
        {
            DrawRequirementLines(cardLeft);
        }

        if (model.QuirkNote is { } note)
        {
            // The curated quirk: what the game does that its data does not say. Shown whatever the state, since it is
            // the answer to "the NPC offers this while the plugin shows it Blocked".
            TextFlow.Wrapped(note, RoomTo(cardRight), Theme.U32(Theme.Surface.TextSecondary));
        }
    }

    /// <summary>
    /// Rewards as tiles on sunken wells, wrapping at the card's inner edge: unique rewards wear a 1.5 px gold ring and a
    /// crescent badge, a small caption says "Store only" or "Also drops" under a tile the store sells or a duty drops.
    /// Hovering a tile, or focusing it with the keyboard, shows <see cref="RewardTooltip"/>.
    /// </summary>
    private void DrawRewards(float innerRight)
    {
        if (model.Rewards.Count == 0)
        {
            ImGui.TextDisabled(Strings.NoRewards);
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var tile = PairTile;
        var iconSize = tile - UiMetrics.Px(8f);
        var gap = PairGap;
        var markSize = ImGui.GetFontSize() * MarkScale;
        var obtained = Theme.MoonRoadArt && model.State is QuestState.Completed or QuestState.DoneThisCycle;
        var rowStart = ImGui.GetCursorScreenPos();
        var x = rowStart.X;
        var y = rowStart.Y;
        var rowHeight = 0f;
        for (var i = 0; i < model.Rewards.Count; i++)
        {
            var reward = model.Rewards[i];
            var markWidth = reward.Mark is null ? 0f : ImGui.CalcTextSize(reward.Mark).X * MarkScale;
            var slot = MathF.Max(tile, markWidth);
            var height = tile + (reward.Mark is null ? 0f : markSize + UiMetrics.Px(2f));
            if (x > rowStart.X && x + slot > innerRight)
            {
                x = rowStart.X;
                y += rowHeight + gap;
                rowHeight = 0f;
            }

            var min = new Vector2(x + ((slot - tile) * 0.5f), y);
            var max = min + new Vector2(tile, tile);
            ImGui.SetCursorScreenPos(min);
            ImGui.PushID(i);
            ImGui.InvisibleButton("##reward", new Vector2(tile, tile));
            var hovered = ImGui.IsItemHovered();
            ImGui.PopID();
            var rounding = UiMetrics.Px(6f);
            dl.AddRectFilled(min, max, Theme.U32(hovered ? Theme.Surface.Hover : Theme.Surface.Sunken), rounding);
            var iconMin = min + new Vector2((tile - iconSize) * 0.5f);
            if (reward.Reward.Icon == 0 || !GameIcon.DrawAt(dl, textures, reward.Reward.Icon, iconMin, iconMin + new Vector2(iconSize), UiMetrics.Px(4f)))
            {
                // No icon known (a title): the veiled moon, as an Unlocks row draws it.
                MoonGlyph.DrawVeiled(dl, (min + max) * 0.5f, iconSize * 0.32f, 0.6f);
            }

            if (reward.Unique)
            {
                dl.AddRect(min, max, Theme.GoldU32, rounding, ImDrawFlags.None, MathF.Max(1.5f, UiMetrics.Px(1.5f)));
                Crescent(dl, new Vector2(max.X - UiMetrics.Px(1f), min.Y + UiMetrics.Px(1f)), MathF.Max(3f, UiMetrics.Px(4f)));
            }
            else if (obtained)
            {
                // A completed quest's rewards are the character's: a brass frame (the moon and the state line say it too).
                dl.AddRect(min, max, Theme.WithAlpha(Theme.Surface.Ornament, Theme.OrnamentAlpha(0.8f)), rounding, ImDrawFlags.None, UiMetrics.Hairline);
            }
            else
            {
                dl.AddRect(min, max, Theme.U32(Theme.Surface.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
            }

            Chrome.FocusRing(rounding);
            if (hovered || (ImGui.GetIO().NavVisible && ImGui.IsItemFocused()))
            {
                RewardTooltip.Draw(reward.Reward, links, textures, reward.Unique ? Strings.DetailUniqueRewardTooltip : null);
            }

            if (reward.Mark is { } mark)
            {
                var markPos = new Vector2(x + ((slot - markWidth) * 0.5f), max.Y + UiMetrics.Px(2f));
                dl.AddText(ImGui.GetFont(), markSize, markPos, Theme.U32(Theme.Surface.TextTertiary), mark);
            }

            x += slot + gap;
            rowHeight = MathF.Max(rowHeight, height);
        }

        ImGui.SetCursorScreenPos(new Vector2(rowStart.X, y));
        ImGui.Dummy(new Vector2(1f, rowHeight));
    }

    /// <summary>The unique-reward badge: a small gold crescent (a disc with an offset bite) on the tile's corner.</summary>
    private static void Crescent(ImDrawListPtr dl, Vector2 center, float r)
    {
        dl.AddCircleFilled(center, r + 1f, Theme.U32(Theme.Surface.Raised), 12);
        dl.AddCircleFilled(center, r, Theme.GoldU32, 12);
        dl.AddCircleFilled(center + new Vector2(r * 0.45f, -r * 0.35f), r * 0.8f, Theme.U32(Theme.Surface.Raised), 12);
    }

    /// <summary>
    /// The user's Moonlit verdict: restore an override, or vouch for a quest the shipped data does not list. Both are
    /// armed buttons of the shared <see cref="VerdictPrompt"/> (Ctrl or Shift and click; feature plan v6 S1): the
    /// change is saved at once and the floating Undo follows, with "Add note" after a verdict.
    /// </summary>
    private void DrawUnique(IUniqueOverrides overrides, uint rowId)
    {
        // Every line wraps between words and each button moves to the next line rather than run off the card (L5).
        if (overrides.Get(rowId) is { } stored)
        {
            TextFlow.Wrapped(stored.Unique ? Strings.MarkedUniqueByYou : Strings.MarkedNotUniqueByYou, RoomTo(cardRight), Theme.U32(stored.Unique ? Theme.Surface.Text : Theme.Surface.TextSecondary));
            if (stored.Note is { Length: > 0 } note)
            {
                TextFlow.Wrapped(note, RoomTo(cardRight), Theme.U32(Theme.Surface.TextSecondary));
            }

            SameLineOrWrap(Chrome.ArmedButtonWidth(Strings.RestoreOverride), cardRight);
            verdict.DrawRestoreButton(overrides, rowId, Strings.RestoreOverride, Strings.RestoreOverrideTooltip);
        }
        else if (model.HasUniqueEntries)
        {
            TextFlow.Wrapped(Strings.ListedInMoonlit, RoomTo(cardRight), Theme.U32(Theme.Surface.TextSecondary));
        }
        else
        {
            TextFlow.Wrapped(Strings.NotListedInMoonlit, RoomTo(cardRight), Theme.U32(Theme.Surface.TextSecondary));
            SameLineOrWrap(Chrome.ArmedButtonWidth(Strings.MarkUnique), cardRight);
            verdict.DrawMarkUniqueButton(overrides, rowId, model.DisplayName);
        }

        // Begun on every path so a note popup opened for one quest is not orphaned when the selection moves on.
        verdict.Draw(overrides);
    }

    /// <summary>
    /// "Next: quest · Ready" under the Path header (feature plan v6 U5), when an earlier quest on the path is the one to
    /// do: the name selects it. Nothing when the quest shown is the next one or done, or when the chain line already
    /// links the same quest, so a quest is never named twice.
    /// </summary>
    private void DrawPathNext()
    {
        if (chart.NextStepRowId is not { } rowId || (model.ChainNextName is not null && model.ChainNextRowId == rowId))
        {
            return;
        }

        ImGui.TextDisabled(Strings.PathNextLabel);
        ImGui.SameLine();
        var tail = Strings.StateReasonSeparator + chart.NextStepState;
        var tailWidth = ImGui.CalcTextSize(tail).X;
        var nameRight = MathF.Max(ImGui.GetCursorScreenPos().X + UiMetrics.Px(24f), cardRight - tailWidth);
        DrawQuestLink("##pathNext", chart.NextStepName, rowId, nameRight);
        if (ImGui.GetItemRectMax().X + tailWidth <= cardRight)
        {
            ImGui.SameLine(0f, 0f);
            using var tone = Theme.PushText(Theme.Surface.TextTertiary);
            ImGui.TextUnformatted(tail);
        }
    }

    /// <summary>
    /// The chain line at the top of the Path card (feature plan v6 U5): the chain's name, then what to do next in it
    /// ("Next in chain: quest", "Still open earlier: quest", or a plain "Last quest in this chain" / "Chain complete"),
    /// on the same line while it fits and on the next otherwise, over a slim moon-gold bar whose hover gives the totals.
    /// Nothing is drawn for a quest outside every chain.
    /// </summary>
    private void DrawChain()
    {
        if (model.ChainText is not { } text)
        {
            return;
        }

        var left = ImGui.GetCursorScreenPos().X;
        TextFlow.Wrapped(text, RoomTo(cardRight), Theme.U32(Theme.Surface.TextSecondary));
        var oneLine = ImGui.GetItemRectSize().Y <= ImGui.GetTextLineHeight() + 0.5f;
        var textEnd = ImGui.GetItemRectMax().X;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var labelWidth = ImGui.CalcTextSize(model.ChainLabel).X;
        var tail = model.ChainNextName is { } next ? labelWidth + spacing + MathF.Min(ImGui.CalcTextSize(next).X, UiMetrics.Px(80f)) : labelWidth;
        if (oneLine && textEnd + (spacing * 2f) + tail <= cardRight)
        {
            ImGui.SameLine(0f, spacing * 2f);
        }

        if (model.ChainNextName is not { } nextName)
        {
            using var tone = Theme.PushText(model.ChainKind == ChainNextKind.Complete ? Theme.AccentDim : Theme.Surface.TextTertiary);
            ImGui.TextUnformatted(model.ChainLabel);
        }
        else
        {
            ImGui.TextDisabled(model.ChainLabel);
            ImGui.SameLine();
            DrawQuestLink("##chainNext", nextName, model.ChainNextRowId, cardRight);
        }

        DrawChainBar(left);
    }

    /// <summary>The chain bar's motion key tag ("CHBR"), with the chain's first quest in the low half.</summary>
    private const uint ChainBarTag = 0x4348_4252;

    /// <summary>
    /// The chain's slim bar under its line: a sunken track with a hairline rim, filled in moon gold (the accent once the
    /// chain is complete), the fill easing to a new fraction like the dashboard gauges (at once under Reduce motion).
    /// Its hover gives the totals.
    /// </summary>
    private void DrawChainBar(float left)
    {
        var height = MathF.Max(2f, UiMetrics.Px(3f));
        var hit = MathF.Max(height, UiMetrics.Px(8f));
        var width = MathF.Max(1f, cardRight - left);
        ImGui.SetCursorScreenPos(new Vector2(left, ImGui.GetCursorScreenPos().Y));
        var top = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##chainBar", new Vector2(width, hit));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(model.ChainTooltip);
        }

        var min = new Vector2(left, top.Y + ((hit - height) * 0.5f));
        var max = min + new Vector2(width, height);
        var rounding = height * 0.5f;
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(min, max, Theme.U32(Theme.Surface.Sunken), rounding);
        dl.AddRect(min, max, Theme.U32(Theme.Surface.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        var fraction = Motion.Gauge(Motion.Key(ChainBarTag, model.ChainKeyId), model.ChainFraction);
        if (fraction > 0f)
        {
            var fill = model.ChainKind == ChainNextKind.Complete ? Theme.U32(Theme.AccentDim) : Theme.GoldU32;
            dl.AddRectFilled(min, new Vector2(min.X + MathF.Max(height, width * fraction), max.Y), fill, rounding);
        }
    }

    /// <summary>
    /// A quest name as a moon-gold link ending by <paramref name="right"/> (ellipsised there, the whole name on hover)
    /// that selects the quest (<see cref="RevealRow"/>), with a focus ring and a hand cursor.
    /// </summary>
    private void DrawQuestLink(string id, string name, uint rowId, float right)
    {
        var lineHeight = ImGui.GetTextLineHeight();
        var nameWidth = ImGui.CalcTextSize(name).X;
        var nameRoom = MathF.Max(UiMetrics.Px(24f), MathF.Min(nameWidth, RoomTo(right)));
        var min = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, new Vector2(nameRoom, lineHeight));
        var hovered = ImGui.IsItemHovered();
        var ink = hovered ? Theme.Surface.Text : Theme.Accent;
        var cut = Chrome.EllipsisTextAt(ImGui.GetWindowDrawList(), min, nameRoom, name, Theme.U32(ink), nameWidth);
        Chrome.FocusRing(UiMetrics.Px(3f));
        if (clicked)
        {
            RevealRow(rowId);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (cut)
            {
                UiMetrics.Tooltip(name, Strings.DetailChainNextTooltip);
            }
            else
            {
                UiMetrics.Tooltip(Strings.DetailChainNextTooltip);
            }
        }
    }

    // The Giver card's face fade (1.15, spec A7): the giver and icon it last faded in for, and when it could first be drawn.
    private ulong giverFaceKey = ulong.MaxValue;
    private double giverFaceSince = -1d;

    /// <summary>
    /// The Giver card (1.15, spec A5): at Full and Quiet the portrait plate (72 or 64 px) on the left, its slot always
    /// held so nothing moves when a texture lands, and the name and the place centred on it. At Plain the card is text
    /// alone: the Ledger hero's Giver row already carries the 18 px plate, and one plate per giver is enough. With Giver
    /// portraits off, the name and the place as before. Hovering the plate shows the 128 px portrait with where it comes from.
    /// </summary>
    private void DrawGiver()
    {
        if (model.GiverName is not { } giver)
        {
            ImGui.TextDisabled(Strings.NoGiver);
            return;
        }

        var place = model.PlaceLine ?? Strings.DetailNoGiverPlace;
        var flair = Theme.Flair;
        if (!GiverPortraits.Enabled || model.Quest is not { } quest || flair == Flair.Plain)
        {
            // The giver's name ends in an ellipsis when it is too long for the card, with the whole name on hover (L5).
            if (Chrome.EllipsisText(giver, RoomTo(cardRight), Theme.U32(Theme.Surface.Text)) && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(giver);
            }

            TextFlow.Wrapped(place, RoomTo(cardRight), Theme.U32(Theme.Surface.TextSecondary));
            return;
        }

        var request = GiverPortraits.For(quest, lastSpoilers ?? Core.Query.SpoilerMask.None);
        var plate = MathF.Round(UiMetrics.Px(PortraitPlate.CardSize(flair)));
        var gap = UiMetrics.Px(PortraitPlate.CardGap(flair));
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var line = ImGui.GetTextLineHeight();
        // Full and Quiet: the plate holds its slot; the name and the place are centred on it.
        var x = start.X + plate + gap;
        var room = MathF.Max(1f, cardRight - x);
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var block = line + spacing + TextFlow.Height(place, room);
        var height = MathF.Max(plate, block);
        ImGui.Dummy(new Vector2(plate, height));
        var after = ImGui.GetCursorScreenPos();
        var top = new Vector2(start.X, start.Y + MathF.Round((height - plate) * 0.5f));
        var drawn = Chrome.Portrait(dl, top, plate, request, GiverFaceAlpha(request));
        if (drawn && giverFaceSince < 0d)
        {
            // The fade starts when the face can first be drawn, so a texture that lands late still fades in.
            giverFaceSince = ImGui.GetTime();
        }

        if (ImGui.IsItemHovered() && ImGui.IsMouseHoveringRect(top, top + new Vector2(plate)))
        {
            Chrome.PortraitTooltip(request, giver, model.PlaceLine);
        }

        ImGui.SetCursorScreenPos(new Vector2(x, start.Y + MathF.Floor(MathF.Max(0f, height - block) * 0.5f)));
        if (Chrome.EllipsisText(giver, room, Theme.U32(Theme.Surface.Text)) && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(giver);
        }

        ImGui.SetCursorScreenPos(new Vector2(x, ImGui.GetCursorScreenPos().Y));
        TextFlow.Wrapped(place, room, Theme.U32(Theme.Surface.TextSecondary));

        // Back under the taller of the plate and the text, where the plate's item left the cursor.
        ImGui.SetCursorScreenPos(new Vector2(after.X, MathF.Max(after.Y, ImGui.GetCursorScreenPos().Y)));
    }

    /// <summary>
    /// The Giver card face's opacity this frame (spec A7): it fades in over <see cref="MotionTokens.ArtFade"/>, ease-out,
    /// when the giver (or the face) changes; at once under Reduce motion or at Plain.
    /// </summary>
    private float GiverFaceAlpha(in PortraitRequest request)
    {
        if (UiMetrics.ReduceMotion || Theme.Flair == Flair.Plain)
        {
            return 1f;
        }

        var key = ((ulong)request.Portrait.GiverId << 32) | (request.FaceAllowed ? request.Portrait.Icon : 0u);
        if (key != giverFaceKey)
        {
            giverFaceKey = key;
            giverFaceSince = -1d;
        }

        return giverFaceSince < 0d ? 0f : PortraitPlate.FadeAlpha(ImGui.GetTime() - giverFaceSince);
    }

    // ------------------------------------------------------------------ action bar

    private static readonly string PinIcon = FontAwesomeIcon.Thumbtack.ToIconString();
    private static readonly string ShowPathIcon = FontAwesomeIcon.Route.ToIconString();
    private static readonly string RouteIcon = FontAwesomeIcon.MapSigns.ToIconString();
    private static readonly string LinkIcon = FontAwesomeIcon.Link.ToIconString();
    private static readonly string CopyIcon = FontAwesomeIcon.Copy.ToIconString();
    private static readonly string ReportIcon = FontAwesomeIcon.Bug.ToIconString();

    private static readonly string StopIcon = FontAwesomeIcon.Stop.ToIconString();

    // This frame's travel checks for the quest shown (PrepareTravel), read by the bar's layout and its buttons.
    private TeleportCheck teleportCheck;
    private WalkCheck walkCheck;
    private GoToCheck goToCheck;
    private HopCheck hopCheck;
    private uint hopLabelShard = uint.MaxValue;
    private string hopLabel = string.Empty;

    /// <summary>
    /// The round buttons of the second row (1.10; the travel and automation actions are pills on the first): Pin, Show
    /// path, Route to this, Flag on map (while Lifestream is loaded; without it Flag leads the pills), Link, Copy,
    /// Journal, Report (when attached), the aethernet hop (in the giver's city) and "…" ("Open on…", the pills the
    /// first row had no room for, and the Questionable hand-off when shown).
    /// </summary>
    private int IconButtonCount => 7 + (Diagnostics is null ? 0 : 1) + (links.FlagLeads ? 0 : 1) + (hopCheck.Visible ? 1 : 0);

    /// <summary>Reads the travel checks and the pills' state once per frame, before the bar's height is planned.</summary>
    private void PrepareTravel(SessionState session, QuestRecord quest)
    {
        teleportCheck = links.CheckTeleport(quest);
        walkCheck = links.WalkShown ? links.CheckWalk(quest) : default;
        goToCheck = links.GoToShown || (links.IsTraveling && !links.WalkShown) ? links.CheckGoTo(quest) : default;
        hopCheck = links.TeleportShown ? links.CheckHop(quest) : default;
        PrepareActions(session, quest);
    }

    /// <summary>
    /// The gap between round buttons: half the usual item spacing, so every round button fits one row at the default
    /// right-column width (each button keeps its full <see cref="UiMetrics.MinTarget"/>).
    /// </summary>
    private static float RoundGap => UiMetrics.Px(4f);

    /// <summary>
    /// The second row's lines as <see cref="DrawActionBar"/> flows them: the round buttons wrap onto as many lines as
    /// the pane's width needs, so on a narrow column every button stays reachable.
    /// </summary>
    private int ActionRows(float width)
    {
        var rows = 1;
        var used = UiMetrics.MinTarget;
        for (var i = 1; i < IconButtonCount; i++)
        {
            if (!FitsOnRow(ref used, width))
            {
                rows++;
            }
        }

        return rows;
    }

    /// <summary>
    /// Whether the next round button fits after <paramref name="used"/> on the current row; advances
    /// <paramref name="used"/> either way (to the new row's first button when it does not fit).
    /// </summary>
    private static bool FitsOnRow(ref float used, float width)
    {
        var need = RoundGap + UiMetrics.MinTarget;
        if (used + need <= width)
        {
            used += need;
            return true;
        }

        used = UiMetrics.MinTarget;
        return false;
    }

    /// <summary>Places the next round button: beside the previous item when it fits, else at the start of a new row.</summary>
    private static void NextRound(ref float used, float width)
    {
        if (FitsOnRow(ref used, width))
        {
            ImGui.SameLine(0f, RoundGap);
        }
    }

    /// <summary>
    /// Height under the scrolling stack, as laid out: the spacing after the body child, the hairline and its spacing,
    /// the pill row, each round-button row and its spacing, then the provenance line (a caption).
    /// Fits the pill row to the width as it goes, so the bar draws what was planned.
    /// </summary>
    private float ActionBarHeight()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var width = ImGui.GetContentRegionAvail().X;
        LayoutActions(width);
        var rows = ActionRows(width);
        return UiMetrics.Hairline + (2 * spacing) + ActionRowHeight(spacing) + (rows * (UiMetrics.MinTarget + spacing)) + Typography.CaptionSize;
    }

    /// <summary>
    /// The sticky action bar (1.10): first the travel and automation pills
    /// (DetailPane.Actions.cs), then round buttons for Pin, Show path, Route to this, Flag on map (while Lifestream is
    /// loaded), Link in chat, Copy coordinates, Open journal, Report, the aethernet hop in the giver's city, and "…".
    /// Disabled buttons say why on hover. All are focusable items (accessibility A6).
    /// </summary>
    private void DrawActionBar(QuestRecord quest, uint rowId)
    {
        Chrome.Hairline();
        DrawActionRow(quest, rowId);

        var width = ImGui.GetContentRegionAvail().X;
        var used = UiMetrics.MinTarget;
        var pinned = model.Pinned;
        var canPin = runner.CanPin;
        if (Chrome.IconButtonRound("##pin", PinIcon, !canPin ? Strings.ActionPinUnavailable : pinned ? Strings.ActionUnpinTooltip : Strings.ActionPinTooltip, pinned, canPin))
        {
            runner.TogglePinWithUndo(quest);
        }

        NextRound(ref used, width);
        if (Chrome.IconButtonRound("##showPath", ShowPathIcon, Strings.ActionShowPathTooltip))
        {
            ui.ShowPath(rowId);
        }

        NextRound(ref used, width);
        if (Chrome.IconButtonRound("##route", RouteIcon, Strings.RouteToThisTooltip))
        {
            ui.OpenRoute(Core.Route.RouteTarget.ForQuest(rowId, model.DisplayName));
        }

        if (!links.FlagLeads)
        {
            // Without Lifestream, or with Teleport hidden by the automation level, Flag on map leads the pills instead.
            NextRound(ref used, width);
            var canFlag = links.CanFlagMap(quest);
            if (Chrome.IconButtonRound("##flagIcon", ActionIcons.FlagIcon, canFlag ? Strings.FlagOnMap : Strings.ActionFlagUnavailable, enabled: canFlag))
            {
                links.FlagMap(quest);
            }
        }

        NextRound(ref used, width);
        if (Chrome.IconButtonRound("##link", LinkIcon, Strings.LinkInChat))
        {
            links.PrintQuestLink(quest);
        }

        NextRound(ref used, width);
        var hasCoordinates = model.CoordinateText is not null;
        if (Chrome.IconButtonRound("##copy", CopyIcon, hasCoordinates ? Strings.CopyCoordinatesTooltip : Strings.ActionCopyCoordinatesUnavailable, enabled: hasCoordinates)
            && links.CoordinateText(quest) is { } coordinates)
        {
            ImGui.SetClipboardText(coordinates);
        }

        NextRound(ref used, width);
        var canOpen = GameLinks.CanOpenJournal(quest, model.State);
        if (Chrome.IconButtonRound("##journal", PillIcon.JournalBook, canOpen ? Strings.OpenJournal : Strings.OpenJournalUnavailable, enabled: canOpen))
        {
            links.OpenJournal(quest);
        }

        if (Diagnostics is { } diagnostics)
        {
            NextRound(ref used, width);
            if (Chrome.IconButtonRound("##report", ReportIcon, Strings.ReportTooltip))
            {
                var copied = DiagnosticBuilder.TryCopy(diagnostics.Compose(quest), log ?? Plugin.Log);
                reportNoteUntil = copied ? ImGui.GetTime() + ReportNoteSeconds : 0.0;
                reportNoteRowId = rowId;
            }
        }

        DrawHopButton(quest, ref used, width);
        DrawMoreMenu(ref used, width, quest, rowId);
    }

    /// <summary>In the giver's city, the aethernet hop toward the giver; its tooltip is composed on hover only.</summary>
    private void DrawHopButton(QuestRecord quest, ref float used, float width)
    {
        if (!hopCheck.Visible)
        {
            return;
        }

        NextRound(ref used, width);
        var hop = hopCheck;
        if (Chrome.IconButtonRound("##hop", ActionIcons.AethernetIcon, null, enabled: hop.Ready))
        {
            links.AethernetToGiver(quest);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            // "Aethernet to Lancers' Guild" over the reason, named once per shard.
            var shard = hop.Firmament ? GoToGiverPlan.FirmamentHop : hop.Shard?.RowId ?? 0;
            if (hopLabelShard != shard)
            {
                hopLabelShard = shard;
                hopLabel = GameLinks.HopLabel(hop);
            }

            UiMetrics.Tooltip(hopLabel, GameLinks.HopTooltip(hop));
        }
    }

    /// <summary>
    /// The plain provenance line under the bar ("Checked just now · live"), or, for a few seconds after Report on the
    /// quest shown, the Report confirmation. The line is composed only when its inputs change: the 30-second bucket
    /// (the age), the viewed character, live or stored, and the minute the snapshot was taken; the quest shown does
    /// not change it. Nothing allocates on the frames between.
    /// </summary>
    private void DrawProvenance(SessionState session)
    {
        // Provenance is a caption (ui-revamp §4.2).
        using var caption = Typography.Caption();
        var now = ImGui.GetTime();
        if (now < reportNoteUntil && reportNoteRowId == model.RowId)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Surface.Text);
            ImGui.TextUnformatted(Strings.ReportCopied);
            ImGui.PopStyleColor();
            return;
        }

        var snapshot = session.ViewedSnapshot;
        var key = new ProvenanceKey(
            (long)Math.Floor(now / ProvenanceRefreshSeconds),
            snapshot?.ContentId ?? 0UL,
            snapshot is not null && session.IsLive,
            snapshot is null ? -1L : snapshot.TakenUtc.Ticks / TimeSpan.TicksPerMinute);
        if (key != provenanceKey)
        {
            provenanceKey = key;
            provenance = BuildProvenance(session);
        }

        // One line whatever the width (the bar's height counts one caption line): ellipsised, whole on hover.
        if (Chrome.EllipsisText(provenance, ImGui.GetContentRegionAvail().X, Theme.U32(Theme.Surface.TextTertiary)) && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(provenance);
        }
    }

    /// <summary>What the provenance line depends on; a new key composes the line again.</summary>
    private readonly record struct ProvenanceKey(long Bucket, ulong ContentId, bool Live, long TakenMinute);

    private ProvenanceKey provenanceKey = new(-1, 0, false, -1);
    private string provenance = string.Empty;

    /// <summary>"Checked just now · live" for the logged-in character, "From Michiru's snapshot, 2 d ago" for a stored one.</summary>
    private static string BuildProvenance(SessionState session)
    {
        if (session.ViewedSnapshot is not { } snapshot)
        {
            return Strings.ProvenanceLogIn;
        }

        var age = UiFormat.Age(snapshot.TakenUtc);
        if (session.IsLive)
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.ProvenanceLiveFormat, age);
        }

        var name = snapshot.Name;
        var space = name.IndexOf(' ', StringComparison.Ordinal);
        var first = space > 0 ? name[..space] : name;
        return string.Format(CultureInfo.CurrentCulture, Strings.ProvenanceSnapshotFormat, first, age);
    }

    // ------------------------------------------------------------------ model

    private void Refresh(SessionState session, CatalogBundle bundle, uint rowId)
    {
        var pinned = runner.IsPinned(rowId);
        // The index is built off the frame, so its revision moves after the catalog has; the Path stations' tooltips
        // read it (and Sprout mode's reach) once per build.
        var unlocksRevision = runner.Unlocks?.Revision ?? 0;
        var unlocksReach = runner.UnlockReach;
        // The requirement lines' icons (UI-5e) read sheets warmed off the frame: their landing builds the lines again.
        var iconSheets = IconSheets?.Invoke();
        if (model.RowId == rowId && model.Version == session.Version && ReferenceEquals(model.Bundle, bundle) && pinnedShown == pinned
            && model.UnlocksRevision == unlocksRevision && model.UnlocksReach == unlocksReach && ReferenceEquals(model.IconSheets, iconSheets))
        {
            return;
        }

        model.IconSheets = iconSheets;
        pinnedShown = pinned;
        model.RowId = rowId;
        model.Version = session.Version;
        model.Bundle = bundle;
        model.UnlocksRevision = unlocksRevision;
        model.UnlocksReach = unlocksReach;
        model.Pinned = pinned;
        model.Requirements.Clear();
        model.Rewards.Clear();
        model.StateNote = null;
        model.ReadyOnJob = 0;
        model.QuirkNote = null;
        model.ChainText = null;
        model.ChainNextName = null;
        model.ChainLabel = string.Empty;
        model.ChainTooltip = string.Empty;
        model.GiverName = null;
        model.PlaceLine = null;
        model.CoordinateText = null;
        model.StatusReason = string.Empty;
        model.StatusTail = string.Empty;
        model.RequirementsCaption = string.Empty;
        model.RewardsCaption = string.Empty;
        model.Evaluation = null;
        model.Callout = null;
        model.CalloutDetail = null;
        model.UnmetCount = 0;
        model.HeaderSegments = [];
        model.JournalSegments = [];
        lastNames = session.Names;
        lastStates = session.States;
        lastSpoilers = session.Spoilers;
        lastCatalog = bundle.Catalog;

        var quest = bundle.Catalog.GetByRowId(rowId);
        model.Quest = quest;
        if (quest is null)
        {
            return;
        }

        var snapshot = session.ViewedSnapshot;
        model.HasSnapshot = snapshot is not null;
        session.States.TryGetValue(rowId, out var evaluation);
        model.Evaluation = evaluation;
        model.State = evaluation?.State ?? QuestState.Unknown;
        var spoilers = session.Spoilers;
        model.DisplayName = spoilers.DisplayName(quest);
        model.NameMasked = spoilers.IsMasked(quest);
        model.StateName = Strings.StateName(model.State, quest);
        var status = BlockerText.StatusText(evaluation, quest, session.Names, session.States);
        var prefix = model.StateName + BlockerText.Separator;
        model.StatusReason = status.StartsWith(prefix, StringComparison.Ordinal) ? status[prefix.Length..] : status == model.StateName ? string.Empty : status;
        model.StatusTail = model.StatusReason.Length > 0 ? BlockerText.Separator + model.StatusReason : string.Empty;
        model.HasUniqueEntries = HasShippedUniqueEntry(session.UniqueRewards, rowId);

        model.JournalSegments = quest.IsUnlisted ? [Strings.RemovedFromGame] : [quest.Journal.GenreName, quest.Journal.CategoryName];
        model.FilingLine = FilingLine(quest, session.Curated);
        model.QuirkNote = session.Curated.Quirks.TryGetValue(rowId, out var quirk) ? WhyText.NoteLine(quirk.Note) : null;
        var jobName = quest.ClassJobCategory <= 1 ? Strings.JobAny : links.ClassJobCategoryName(quest.ClassJobCategory);
        if (jobName.Length == 0)
        {
            jobName = runner.JobShort(quest);
        }

        model.HeaderLine = string.Format(CultureInfo.CurrentCulture, Strings.HeaderLineFormat, bundle.Names.Expansion(quest.Expansion), quest.DisplayLevel, jobName);
        if (quest.AddedIn.Length > 0)
        {
            // P8: the patch of origin closes the hero's caption ("Dawntrail · Lv 100 · Any · Added in 7.5").
            model.HeaderLine += string.Format(CultureInfo.CurrentCulture, Strings.DetailAddedInFormat, quest.AddedIn);
        }

        // Decision 9: when the character completed it, as far as its snapshot knows ("Done 12 Sep 2026").
        if (Core.Runtime.CompletionDates.For(snapshot, quest.QuestId) is { } done)
        {
            model.HeaderLine += Strings.DetailDoneSuffix(done);
        }

        // The caption line as whole segments ("Heavensward", "Lv 56", the job, "Added in 3.0") that wrap apart (L5).
        model.HeaderSegments = model.HeaderLine.Split(BlockerText.Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (evaluation is not null)
        {
            // The status line already carries the step ("In journal · step 3 of 7") and the blocker; only the job
            // that can take the quest is a note beside it.
            if (evaluation.ReadyOnJob is { } job)
            {
                model.ReadyOnJob = job;
                model.StateNote = string.Format(CultureInfo.CurrentCulture, Strings.ReadyOnJobFormat, bundle.Names.ClassJobAbbreviation(job));
            }
            else if (evaluation.OtherPath is { } path)
            {
                // A path not taken: who the quest is for, and the character's own path (feature plan v4 D1).
                model.StateNote = PathText.Detail(path, session.Names.GrandCompany);
            }

            // A job that cannot take the quest has no level to compare: the level is read on the job that can (L8).
            var shown = snapshot is null ? evaluation : NotYetText.OnAdmittedJob(evaluation, quest, snapshot, session.Context);

            // What stands in the way, in one line at the top (L8); a path not taken keeps its D1 line under it.
            model.Callout = NotYetText.Callout(shown, quest, session.Names, session.States);
            model.CalloutDetail = model.Callout is not null && evaluation.OtherPath is not null ? model.StateNote : null;

            var unmet = 0;
            var levelJob = LevelJobOf(shown.Requirements);
            foreach (var result in shown.Requirements)
            {
                // The evaluator wrote the prerequisite's or lock's real name into the detail; the shield masks it here.
                // In the UI language: the evaluator's own detail is English, and stays so for the diagnostic block.
                var clause = RequirementDetail.Text(result, session.Names, snapshot?.CurrentJob);
                var detail = result.Req switch
                {
                    PreviousQuestsRequirement p => spoilers.MaskNamesIn(clause, bundle.Catalog, p.QuestIds),
                    ForeclosureRequirement f => spoilers.MaskNamesIn(clause, bundle.Catalog, f.CompletedLockIds),
                    _ => clause,
                };
                model.Requirements.Add(UnmetLine(session, bundle, quest, result, ReferenceEquals(result, shown.NextStep), detail) with
                {
                    Icon = RequirementIcon(bundle, result.Req, levelJob, iconSheets),
                });
                unmet += result.Met ? 0 : 1;
            }

            model.UnmetCount = unmet;
            if (model.Requirements.Count > 0)
            {
                model.RequirementsCaption = unmet == 0
                    ? Strings.DetailRequirementsAllMet
                    : string.Format(CultureInfo.CurrentCulture, Strings.DetailRequirementsUnmetFormat, unmet, model.Requirements.Count);
            }
        }

        BuildRewards(session, quest);
        chart.Load(session, bundle, quest, MaxUnlocks);
        BuildChain(session, bundle, rowId);

        if (quest.Issuer is { } issuer)
        {
            model.GiverName = issuer.Name.Length > 0 ? issuer.Name : Strings.NoGiver;
            if (links.MapCoordinates(quest) is { } coords)
            {
                model.CoordinateText = string.Format(CultureInfo.CurrentCulture, Strings.CoordinatesFormat, coords.X, coords.Y);
            }

            if (links.Map(issuer.MapId) is { } map)
            {
                var place = map.Region.Length > 0 && map.Region != map.PlaceName
                    ? string.Format(CultureInfo.CurrentCulture, Strings.JournalPathFormat, map.Region, map.PlaceName)
                    : map.PlaceName;
                model.PlaceLine = model.CoordinateText is { } text ? place + " " + text : place;
            }
        }
    }

    /// <summary>
    /// The reward tiles: what the quest hands over to keep (<see cref="RewardSplit"/>: a duty, a job, an action, flying
    /// or a feature is an Unlocks row instead), then what the unlock index files under Rewards that no reward slot
    /// carries (a title); which are unique for this quest (shipped entries), and which the store sells or a duty drops.
    /// </summary>
    private void BuildRewards(SessionState session, QuestRecord quest)
    {
        uniqueByQuest.TryGetValue(quest.RowId, out var entries);
        var unique = 0;
        foreach (var reward in quest.Rewards)
        {
            // A slot the sheets name nothing for (an item row without a name) is no tile: it drew a blank one.
            if (!RewardSplit.IsReward(reward) || reward.Name.Length == 0)
            {
                continue;
            }

            var isUnique = IsUniqueReward(entries, reward.Kind, reward.Id, reward.ItemId);

            // The reward tooltip says where else it comes from; the tile's caption only names the kind of source.
            var mark = session.StoreResells.Contains(reward) ? Strings.MoonlitStoreOnly
                : session.StoreResells.DropWhere(reward) is not null ? Strings.MoonlitAlsoDrops
                : null;
            unique += isUnique ? 1 : 0;
            model.Rewards.Add(new RewardTile(reward, isUnique, mark));
        }

        // A title the reward data names: a Rewards tile as well, under the shield and Sprout mode's reach like an unlock row.
        if (runner.Unlocks is { } source && !session.Spoilers.IsMasked(quest))
        {
            var reach = runner.UnlockReach;
            foreach (var extra in source.Current.ExtraRewards(quest.RowId))
            {
                if (extra.Reward is not { } kind || !UnlockView.InReach(extra, reach))
                {
                    continue;
                }

                var icon = extra.Icon;
                if (icon == 0 && UnlockIcon is { } resolve)
                {
                    // The shipped entry, whose source names a title's achievement (and so its icon), else one built here.
                    var asEntry = ShippedEntry(entries, kind, extra.TargetId, extra.ItemId)
                        ?? new UniqueRewardEntry(quest.RowId, kind, extra.TargetId, extra.ItemId, extra.Name, Confidence.Static, string.Empty);
                    icon = resolve(quest, asEntry);
                }

                var isUnique = IsUniqueReward(entries, kind, extra.TargetId, extra.ItemId);
                unique += isUnique ? 1 : 0;
                model.Rewards.Add(new RewardTile(new RewardRef(kind, extra.TargetId, extra.ItemId, 1, extra.Name, icon), isUnique, null));
            }
        }

        model.RewardsCaption = unique == 0 ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.DetailRewardsUniqueFormat, unique);
    }

    /// <summary>Whether a shipped unique-reward entry of the quest names the reward: same kind, and same row or same item.</summary>
    private static bool IsUniqueReward(List<UniqueRewardEntry>? entries, RewardKind kind, uint id, uint itemId) =>
        ShippedEntry(entries, kind, id, itemId) is not null;

    /// <summary>The shipped unique-reward entry of the quest that names the reward (same kind, and same row or same item); null when none.</summary>
    private static UniqueRewardEntry? ShippedEntry(List<UniqueRewardEntry>? entries, RewardKind kind, uint id, uint itemId)
    {
        if (entries is null)
        {
            return null;
        }

        foreach (var entry in entries)
        {
            if (entry.Kind == kind && (entry.RewardId == id || (entry.ItemId != 0 && entry.ItemId == itemId)))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// The chain line for a quest that belongs to one, a curated or genre chain ("Hildibrand") or a side story ("Story:
    /// &lt;first quest&gt;"), from the session's chain catalog: its name, what to do next in it seen from this quest
    /// (<see cref="ChainLine"/>), the bar's fill and the totals for the bar's hover.
    /// </summary>
    private void BuildChain(SessionState session, CatalogBundle bundle, uint rowId)
    {
        if (session.Chains.ForQuest(rowId) is not { } chain || ChainLine.For(chain, rowId, session.States) is not { } line)
        {
            // Outside every chain, or nothing in it counts for this character: no line at all.
            return;
        }

        model.ChainText = ChainCatalog.DisplayName(chain, id => session.Spoilers.DisplayName(bundle.Catalog, id, id.ToString(CultureInfo.InvariantCulture)));
        model.ChainKind = line.Kind;
        model.ChainLabel = line.Label;
        model.ChainTooltip = line.Tooltip;
        model.ChainFraction = line.Fraction;
        model.ChainKeyId = chain.RowIds.Count > 0 ? chain.RowIds[0] : 0;
        if (line.LinkRowId is { } next)
        {
            model.ChainNextRowId = next;
            model.ChainNextName = session.Spoilers.DisplayName(bundle.Catalog, next, next.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// The provenance line under the journal path: which rule (or curated file) filed a refiled quest, or why the
    /// game removed it: the patch when the curated note names one, else the sheet signal rule 1 read ("Rule 1:
    /// placeholder issuer"). It never repeats the path: an unlisted retired row's path already reads "Removed from
    /// the game", so only a listed retired row (its path is the genre) gets those words here. Null for a quest the
    /// sheet filed itself.
    /// </summary>
    internal static string? FilingLine(QuestRecord quest, CuratedData curated)
    {
        if (quest.IsRetired)
        {
            if (curated.RetiredQuests.TryGetValue(quest.RowId, out var retired) && retired.Patch.Length > 0)
            {
                return string.Format(CultureInfo.CurrentCulture, Strings.RemovedInPatchFormat, retired.Patch);
            }

            if (JournalRefiler.IsRetiredRow(quest))
            {
                var reason = Strings.RetiredReason(quest.Issuer is { NpcId: JournalRefiler.PlaceholderIssuer }, quest.IsHidden);
                return string.Format(CultureInfo.CurrentCulture, quest.IsUnlisted ? Strings.RetiredRuleFormat : Strings.RemovedByRuleFormat, reason);
            }

            // Curated without a patch: the path of an unlisted row says it already.
            return quest.IsUnlisted ? null : Strings.RemovedFromGame;
        }

        if (quest.RefiledFrom == 0 || quest.IsUnlisted)
        {
            return null;
        }

        return quest.RefiledFrom == JournalRefiler.CuratedRule
            ? string.Format(CultureInfo.CurrentCulture, Strings.FilingCuratedFormat, quest.Journal.GenreName)
            : string.Format(CultureInfo.CurrentCulture, Strings.FilingRuleFormat, quest.Journal.GenreName, quest.RefiledFrom, Strings.FilingReason(quest.RefiledFrom));
    }

    private bool HasShippedUniqueEntry(UniqueRewardsData data, uint rowId)
    {
        if (!ReferenceEquals(uniqueData, data))
        {
            uniqueData = data;
            uniqueByQuest.Clear();
            foreach (var entry in data.Entries)
            {
                if (!uniqueByQuest.TryGetValue(entry.QuestRowId, out var list))
                {
                    list = [];
                    uniqueByQuest[entry.QuestRowId] = list;
                }

                list.Add(entry);
            }
        }

        return uniqueByQuest.ContainsKey(rowId);
    }
}
