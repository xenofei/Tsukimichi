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
/// bar with one labelled primary action (Flag on map, or Teleport when Lifestream is loaded) and round buttons for Pin,
/// Show path, Route to this (the unlock route window), Link in chat, Copy coordinates, Open journal and Report, and a plain provenance line. Every action is a
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
    private const float HeroMaxHeight = 96f;
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
    private sealed record RequirementLine(bool Met, bool IsNext, string Label, string Detail, float GapFraction = 0f, string? GapText = null, uint JumpRowId = 0, string? JumpTooltip = null);

    /// <summary>A reward tile: unique rewards wear the gold ring and crescent; a mark says when the store sells it or a duty drops it.</summary>
    private sealed record RewardTile(RewardRef Reward, bool Unique, string? Mark);

    private sealed class Model
    {
        public uint RowId;
        public int Version;
        public CatalogBundle? Bundle;
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

        /// <summary>"Note: …" from <c>curated/quirks.json</c>, drawn under the requirements; null for a quest without one.</summary>
        public string? QuirkNote;
        public string? ChainText;

        /// <summary>The chain halo's tooltip ("3 of 7 quests done"), composed with <see cref="ChainText"/>.</summary>
        public string ChainHaloTooltip = string.Empty;
        public string? ChainNextName;
        public uint ChainNextRowId;
        public float ChainFraction;
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
        chart = new PathChart(RevealRow);
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
        PrepareTravel(quest);
        var bar = ActionBarHeight();
        using (ImRaii.PushColor(ImGuiCol.ChildBg, Vector4.Zero))
        using (var body = ImRaii.Child("##detailBody", new Vector2(0f, MathF.Max(UiMetrics.Px(40f), detailHeight - bar)), false))
        {
            if (body)
            {
                DrawBody(session, quest, rowId, detailHeight);
            }
        }

        DrawActionBar(quest, rowId);
        DrawProvenance(session);
    }

    private void DrawBody(SessionState session, QuestRecord quest, uint rowId, float detailHeight)
    {
        var width = ImGui.GetContentRegionAvail().X;
        bodyRight = ImGui.GetCursorScreenPos().X + width;

        // Flair Full and Quiet: open sections, their content running to the body's edge; Plain: the 1.3 cards.
        var open = Theme.ShowRules;
        cardRight = open ? bodyRight : bodyRight - UiMetrics.Px(10f);
        DrawHero(quest);
        DrawNotYet(quest);
        DrawUnderHero(session, rowId);
        if (open)
        {
            MoonRoadDivider();
        }

        Gap();
        var start = ImGui.GetCursorScreenPos();
        BeginSection("##requirements", Strings.Requirements, RequirementsIcon, model.RequirementsCaption, model.UnmetCount > 0 ? Theme.EclipseText : Theme.Surface.TextTertiary);
        DrawRequirements(start.X);
        EndSection();
        ui.RecordItem(UiRects.DetailRequirements);

        Gap();
        BeginSection("##rewards", Strings.Rewards, RewardsIcon, model.RewardsCaption, Theme.Surface.TextTertiary);
        DrawRewards(cardRight);
        EndSection();
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
        BeginSection("##path", Strings.Path, PathIcon, chart.HeaderCaption, Theme.Surface.TextTertiary);
        DrawChain();

        // The chart ends at the card's inner edge, or a gutter short of the body's (the minimap draws in the gutter).
        var chartRight = open ? bodyRight - pad : cardRight;
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

    private static void Gap() => ImGui.Dummy(new Vector2(0f, UiMetrics.Px(2f)));

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
        var right = cardRight - UiMetrics.Px(10f);
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
    /// The hero. At Flair Full and Quiet the Moon Road hero (DetailPane.Hero.cs): every quest's banner from the fallback
    /// chain in its frame, the state moon rising on its edge, the title and the chips under it. At Plain the 1.3 hero:
    /// the banner (from the same chain) with the state pill and the name on it, or the Night card while it loads.
    /// </summary>
    private void DrawHero(QuestRecord quest)
    {
        if (Theme.ShowRules)
        {
            DrawMoonRoadHero(quest);
            return;
        }

        if (!DrawBanner(quest))
        {
            DrawHeaderCard(quest);
        }
    }

    /// <summary>
    /// The journal banner at the column's width and at most 96 px tall, cover-cropped, a scrim from 30 % of its height
    /// to the bottom, the state pill top left (clamped, with an ellipsis), the special badge top right, and the name
    /// bottom left while it fits under the pill, else under the banner; the caption line follows under the banner as
    /// whole segments that wrap (L5): the 1.3 hero, with the banner from the fallback chain (<see cref="CurrentBanner"/>)
    /// so every quest has one. False while it is not loaded yet.
    /// </summary>
    private bool DrawBanner(QuestRecord quest)
    {
        var choice = CurrentBanner(quest);
        if (!BannerArtwork.TryGetWrap(textures, in choice, out var wrap, out var shown) || wrap.Width <= 0 || wrap.Height <= 0)
        {
            return false;
        }

        var dl = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var height = MathF.Min(UiMetrics.Px(HeroMaxHeight), width * wrap.Height / wrap.Width);
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);
        var rounding = UiMetrics.Px(6f);
        Chrome.ImageCover(wrap.Handle, new Vector2(width, height), new Vector2(wrap.Width, wrap.Height), rounding);
        var after = ImGui.GetCursorScreenPos();
        Chrome.Scrim(dl, new Vector2(min.X, min.Y + (height * 0.3f)), max, 0f, 0.92f);

        var pad = UiMetrics.Px(8f);
        var badgeSize = MathF.Min(UiMetrics.BannerBadgeSize, UiMetrics.Px(22f));

        // State pill top left, clamped to the room the special badge leaves; its item carries the state tooltip.
        var pillMin = min + new Vector2(pad, pad);
        var pillRoom = width - (pad * 2f) - (quest.IconSpecial != 0 ? badgeSize + pad : 0f);
        var pillSize = StatePill(dl, pillMin, pillRoom);
        ImGui.SetCursorScreenPos(pillMin);
        ImGui.InvisibleButton("##heroState", pillSize);
        Chrome.FocusRing(pillSize.Y * 0.5f);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.StateTooltip(model.State, model.Evaluation, quest, BlockerNamesOf(), lastStates);
        }

        if (quest.IconSpecial != 0)
        {
            var badgeMin = new Vector2(max.X - pad - badgeSize, min.Y + pad);
            if (DrawSpecialBadge(dl, quest, badgeMin, badgeSize))
            {
                ImGui.SetCursorScreenPos(badgeMin);
                ImGui.InvisibleButton("##heroBadge", new Vector2(badgeSize, badgeSize));
                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(BadgeTooltip(quest));
                }
            }
        }

        // The name (display role) on the banner's foot while it fits under the pill; a longer one goes under the
        // banner, so it never grows upward over the pill (L5).
        var nameWidth = MathF.Max(UiMetrics.Px(40f), width - (pad * 2f));
        var nameOnBanner = false;
        using (Typography.Display())
        {
            var nameHeight = TextFlow.Height(model.DisplayName, nameWidth);
            if (nameHeight <= height - pillSize.Y - (pad * 3f))
            {
                ImGui.SetCursorScreenPos(new Vector2(min.X + pad, max.Y - pad - nameHeight));
                TextFlow.Wrapped(model.DisplayName, nameWidth, Theme.U32(Theme.Surface.Text));
                nameOnBanner = true;
            }
        }

        ImGui.SetCursorScreenPos(after);
        if (!nameOnBanner)
        {
            using var display = Typography.Display();
            TextFlow.Wrapped(model.DisplayName, width, Theme.U32(Theme.Surface.Text));
        }

        // The 1.3 banner had no tooltip; only a banner the shield swapped for the category art says why, as 1.3's Night
        // card did.
        if (ArtworkWithheld(quest))
        {
            BannerTooltip(min, max, shown);
        }

        // The caption line under the banner, its segments whole and wrapping.
        SegmentFlow(model.HeaderSegments, BlockerText.Separator, width, Theme.Surface.TextSecondary);
        return true;
    }

    private BlockerNames BlockerNamesOf() => lastNames ?? BlockerNames.Default;

    // The session's names and states as of the last refresh, for the state tooltip's blocker line.
    private BlockerNames? lastNames;
    private IReadOnlyDictionary<uint, QuestEvaluation>? lastStates;

    // The spoiler shield and the catalog as of the last refresh, for the hero banner's shield (a donor's own state).
    private Core.Query.SpoilerMask? lastSpoilers;
    private QuestCatalog? lastCatalog;

    /// <summary>
    /// The state pill (ui-revamp §2.5): the state colour at 18 % over a dark base, a 1 px border at 70 %, a 12 px moon and
    /// the state's name in its text tone. At most <paramref name="maxWidth"/> wide: a longer name ends in an ellipsis
    /// (the pill's tooltip names the state in full). Returns its size.
    /// </summary>
    private Vector2 StatePill(ImDrawListPtr dl, Vector2 min, float maxWidth)
    {
        using var caption = Typography.Caption();
        var captionSize = ImGui.GetFontSize();
        var moon = UiMetrics.Icon(6f);
        var height = MathF.Max(UiMetrics.Px(20f), MathF.Max(captionSize + UiMetrics.Px(6f), (moon * 2f) + UiMetrics.Px(6f)));
        var textWidth = ImGui.CalcTextSize(model.StateName).X;
        var chrome = UiMetrics.Px(5f) + (moon * 2f) + UiMetrics.Px(6f) + UiMetrics.Px(8f);
        var size = new Vector2(MathF.Min(chrome + textWidth, MathF.Max(height, maxWidth)), height);
        var max = min + size;
        var tone = Theme.StateColor(model.State);
        var rounding = height * 0.5f;
        dl.AddRectFilled(min, max, Theme.WithAlpha(Theme.Night, 0.6f), rounding);
        dl.AddRectFilled(min, max, Theme.WithAlpha(tone, 0.18f), rounding);
        dl.AddRect(min, max, Theme.WithAlpha(tone, 0.7f), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        var center = new Vector2(min.X + UiMetrics.Px(5f) + moon, min.Y + (height * 0.5f));
        MoonGlyph.Draw(dl, center, moon, model.State);
        var textRoom = size.X - chrome;
        if (textRoom > 0f)
        {
            var textPos = new Vector2(center.X + moon + UiMetrics.Px(6f), min.Y + ((height - captionSize) * 0.5f));
            Chrome.EllipsisTextAt(dl, textPos, textRoom, model.StateName, Theme.U32(StateTextColor(model.State)), textWidth);
        }

        return size;
    }

    /// <summary>A state's text tone for pills and captions: the state colour, with Locked out, Not checked and Blocked in their readable text tones.</summary>
    private static Vector4 StateTextColor(QuestState state) => state switch
    {
        QuestState.Foreclosed => Theme.EclipseText,
        QuestState.Unknown => Theme.VeilText,
        QuestState.Blocked => Theme.Surface.TextSecondary,
        _ => Theme.StateColor(state),
    };

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
    /// The Night card with the state moon, the name and the caption line, for quests without a banner, and for quests
    /// whose banner the spoiler shield hides (the card then says the artwork appears once the quest is in the journal).
    /// </summary>
    private void DrawHeaderCard(QuestRecord quest)
    {
        var dl = ImGui.GetWindowDrawList();
        Chrome.BeginCard("##headerCard");

        // Below 320 the moon (28 px) sits above the title, which then takes the card's whole width (L5).
        var stacked = DetailTiers.Stacks(tier);
        var radius = stacked ? UiMetrics.Icon(14f) : UiMetrics.Icon(20f);
        var box = stacked ? radius * 2f : radius * 2.3f;
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(box, box));
        MoonGlyph.Draw(dl, pos + new Vector2(box * 0.5f), radius, model.State);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.StateTooltip(model.State, model.Evaluation, quest, BlockerNamesOf(), lastStates);
        }

        if (quest.IconSpecial != 0)
        {
            ImGui.SameLine();
            var size = MathF.Min(UiMetrics.BannerBadgeSize, UiMetrics.Px(22f));
            var badgeMin = ImGui.GetCursorScreenPos() + new Vector2(0f, (box - size) * 0.5f);
            ImGui.Dummy(new Vector2(size, box));
            if (DrawSpecialBadge(dl, quest, badgeMin, size) && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(BadgeTooltip(quest));
            }
        }

        if (!stacked)
        {
            ImGui.SameLine();
        }

        // The name wraps between words at the card's inner edge; the caption line's segments wrap whole.
        using (ImRaii.Group())
        {
            var room = RoomTo(cardRight);
            using (Typography.Display())
            {
                TextFlow.Wrapped(model.DisplayName, room, Theme.U32(Theme.Surface.Text));
            }

            SegmentFlow(model.HeaderSegments, BlockerText.Separator, room, Theme.Surface.TextSecondary);
            if (ArtworkWithheld(quest))
            {
                TextFlow.Wrapped(Strings.ArtworkHidden, room, Theme.U32(Theme.Surface.TextSecondary));
            }

            var pillPos = ImGui.GetCursorScreenPos();
            var pillSize = StatePill(dl, pillPos, room);
            ImGui.Dummy(pillSize);
        }

        Chrome.EndCard();
    }

    /// <summary>
    /// Under the hero: the name-reveal line when the shield masks it, what the pill cannot say (the blocker, the step,
    /// the job that can take it), the journal path and the filing line.
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

        // The "Not yet" callout already says why a quest cannot be taken (L8); the status line is for the rest. With
        // the Moon Road hero there is no state pill: the line spells the state out beside the rising moon.
        if (model.Callout is null && Theme.ShowRules)
        {
            DrawStateLine();
        }
        else if (model.Callout is null && (model.StatusReason.Length > 0 || model.StateNote is not null))
        {
            TextFlow.Wrapped(model.StatusReason.Length > 0 ? model.StatusReason : model.StateNote!, RoomTo(bodyRight), Theme.U32(StateTextColor(model.State)));
            if (model.StatusReason.Length > 0 && model.StateNote is { } note)
            {
                TextFlow.Wrapped(note, RoomTo(bodyRight), Theme.U32(Theme.Surface.TextDisabled));
            }
        }

        // The journal path wraps on its "›", never inside a name (L5).
        SegmentFlow(model.JournalSegments, JournalSeparator, RoomTo(bodyRight), Theme.Surface.TextDisabled);
        if (model.FilingLine is { } filing)
        {
            TextFlow.Wrapped(filing, RoomTo(bodyRight), Theme.U32(Theme.Surface.TextSecondary));
        }

        if (model.Quest is { } quest)
        {
            DrawQuestionableLine(session, quest);
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
        var tile = MathF.Max(UiMetrics.Px(40f), UiMetrics.Icon(34f) + UiMetrics.Px(8f));
        var iconSize = tile - UiMetrics.Px(8f);
        var gap = UiMetrics.Px(6f);
        var markSize = ImGui.GetFontSize() * MarkScale;
        var obtained = Theme.ShowRules && model.State is QuestState.Completed or QuestState.DoneThisCycle;
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
            if (reward.Reward.Icon != 0)
            {
                var iconMin = min + new Vector2((tile - iconSize) * 0.5f);
                GameIcon.DrawAt(dl, textures, reward.Reward.Icon, iconMin, iconMin + new Vector2(iconSize), UiMetrics.Px(4f));
            }

            if (reward.Unique)
            {
                dl.AddRect(min, max, Theme.MoonU32, rounding, ImDrawFlags.None, MathF.Max(1.5f, UiMetrics.Px(1.5f)));
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
        dl.AddCircleFilled(center, r, Theme.MoonU32, 12);
        dl.AddCircleFilled(center + new Vector2(r * 0.45f, -r * 0.35f), r * 0.8f, Theme.U32(Theme.Surface.Raised), 12);
    }

    /// <summary>
    /// The user's Moonlit verdict: restore an override, or vouch for a quest the shipped data does not list. "Mark as
    /// unique…" opens the shared <see cref="VerdictPrompt"/> (Shift and click, or hold, to confirm) and an Undo line
    /// follows for eight seconds.
    /// </summary>
    private void DrawUnique(IUniqueOverrides overrides, uint rowId)
    {
        // Every line wraps between words and each button moves to the next line rather than run off the card (L5).
        if (overrides.Get(rowId) is { } stored)
        {
            TextFlow.Wrapped(stored.Unique ? Strings.MarkedUniqueByYou : Strings.MarkedNotUniqueByYou, RoomTo(cardRight), Theme.U32(stored.Unique ? Theme.Surface.Text : Theme.Surface.TextSecondary));
            if (stored.Note is { Length: > 0 } note)
            {
                TextFlow.Wrapped(note, RoomTo(cardRight), Theme.U32(Theme.Surface.TextDisabled));
            }

            SameLineOrWrap(SmallButtonWidth(Strings.RestoreOverride), cardRight);
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
            TextFlow.Wrapped(Strings.ListedInMoonlit, RoomTo(cardRight), Theme.U32(Theme.Surface.TextDisabled));
        }
        else
        {
            TextFlow.Wrapped(Strings.NotListedInMoonlit, RoomTo(cardRight), Theme.U32(Theme.Surface.TextDisabled));
            SameLineOrWrap(SmallButtonWidth(Strings.MarkUnique), cardRight);
            if (ImGui.SmallButton(Strings.MarkUnique))
            {
                verdict.Open(rowId, true, model.DisplayName);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.MarkUniqueTooltip);
            }
        }

        // Begun on every path so a popup opened for one quest is not orphaned when the selection moves on.
        verdict.Draw(overrides);
        verdict.DrawUndo(overrides, rowId);
    }

    /// <summary>
    /// "Chain: name · N of M done · next: quest" with a filling halo at its left, at the top of the Path card (the only
    /// place the chain is shown); the next quest's name selects it. The chain text wraps between words beside the halo,
    /// and "next: quest" follows on the same line while it fits, else on its own line under the text, the name ending
    /// in an ellipsis when even that is too narrow (L5). Nothing is drawn for a quest outside every chain.
    /// </summary>
    private void DrawChain()
    {
        if (model.ChainText is not { } text)
        {
            return;
        }

        var lineHeight = ImGui.GetTextLineHeight();
        var size = UiMetrics.HaloBoxSize(lineHeight);
        MoonGlyph.DrawHaloInline(model.ChainFraction, size, onCard: true);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(model.ChainHaloTooltip);
        }

        // The glyph box is taller than a text line; centre the text's first line on it.
        ImGui.SameLine();
        var textLeft = ImGui.GetCursorScreenPos().X;
        var room = RoomTo(cardRight);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((size - lineHeight) * 0.5f));
        TextFlow.Wrapped(text, room, Theme.U32(Theme.Surface.TextSecondary));
        var oneLine = ImGui.GetItemRectSize().Y <= lineHeight + 0.5f;
        var textEnd = ImGui.GetItemRectMax().X;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var tail = model.ChainNextName is { } next
            ? ImGui.CalcTextSize(Strings.DetailChainNext).X + spacing + MathF.Min(ImGui.CalcTextSize(next).X, UiMetrics.Px(80f))
            : ImGui.CalcTextSize(Strings.DetailChainComplete).X;
        if (oneLine && textEnd + spacing + tail <= cardRight)
        {
            ImGui.SameLine();
        }
        else
        {
            ImGui.SetCursorScreenPos(new Vector2(textLeft, ImGui.GetCursorScreenPos().Y));
        }

        if (model.ChainNextName is not { } nextName)
        {
            using var done = Theme.PushText(Theme.AccentDim);
            ImGui.TextUnformatted(Strings.DetailChainComplete);
            return;
        }

        ImGui.TextDisabled(Strings.DetailChainNext);
        ImGui.SameLine();
        var nameWidth = ImGui.CalcTextSize(nextName).X;
        var nameRoom = MathF.Max(UiMetrics.Px(24f), MathF.Min(nameWidth, RoomTo(cardRight)));
        var min = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton("##chainNext", new Vector2(nameRoom, lineHeight));
        var hovered = ImGui.IsItemHovered();
        var ink = hovered ? Theme.Surface.Text : Theme.Moon;
        var cut = Chrome.EllipsisTextAt(ImGui.GetWindowDrawList(), min, nameRoom, nextName, Theme.U32(ink), nameWidth);
        Chrome.FocusRing(UiMetrics.Px(3f));
        if (clicked)
        {
            RevealRow(model.ChainNextRowId);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (cut)
            {
                UiMetrics.Tooltip(nextName, Strings.DetailChainNextTooltip);
            }
            else
            {
                UiMetrics.Tooltip(Strings.DetailChainNextTooltip);
            }
        }
    }

    private void DrawGiver()
    {
        if (model.GiverName is not { } giver)
        {
            ImGui.TextDisabled(Strings.NoGiver);
            return;
        }

        // The giver's name ends in an ellipsis when it is too long for the card, with the whole name on hover (L5).
        if (Chrome.EllipsisText(giver, RoomTo(cardRight), Theme.U32(Theme.Surface.Text)) && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(giver);
        }

        TextFlow.Wrapped(model.PlaceLine ?? Strings.DetailNoGiverPlace, RoomTo(cardRight), Theme.U32(Theme.Surface.TextSecondary));
    }

    // ------------------------------------------------------------------ action bar

    private static readonly string FlagIcon = FontAwesomeIcon.Flag.ToIconString();
    private static readonly string TeleportIcon = FontAwesomeIcon.PaperPlane.ToIconString();
    private static readonly string PinIcon = FontAwesomeIcon.Thumbtack.ToIconString();
    private static readonly string ShowPathIcon = FontAwesomeIcon.Route.ToIconString();
    private static readonly string RouteIcon = FontAwesomeIcon.MapSigns.ToIconString();
    private static readonly string LinkIcon = FontAwesomeIcon.Link.ToIconString();
    private static readonly string CopyIcon = FontAwesomeIcon.Copy.ToIconString();
    private static readonly string JournalIcon = FontAwesomeIcon.BookOpen.ToIconString();
    private static readonly string ReportIcon = FontAwesomeIcon.Bug.ToIconString();

    private static readonly string WalkIcon = FontAwesomeIcon.Walking.ToIconString();
    private static readonly string GoToIcon = FontAwesomeIcon.LocationArrow.ToIconString();
    private static readonly string HopIcon = FontAwesomeIcon.ExchangeAlt.ToIconString();
    private static readonly string StopIcon = FontAwesomeIcon.Stop.ToIconString();

    // This frame's travel checks for the quest shown (PrepareTravel), read by the bar's layout and its buttons.
    private TeleportCheck teleportCheck;
    private WalkCheck walkCheck;
    private GoToCheck goToCheck;
    private HopCheck hopCheck;
    private uint hopLabelShard = uint.MaxValue;
    private string hopLabel = string.Empty;

    /// <summary>
    /// Round buttons after the primary action: Pin, Show path, Route to this, Link, Copy, Journal, Report (when
    /// attached), Flag (when Teleport leads) or Teleport (disabled, naming Lifestream, when Flag leads), Walk and Go to
    /// giver (when shown in Settings), the aethernet hop (in the giver's city), "…" ("Open on…" since 1.8.0, and the
    /// Questionable hand-off when shown).
    /// </summary>
    private int IconButtonCount => 8 + (Diagnostics is null ? 0 : 1) + (links.WalkShown ? 1 : 0) + (links.GoToShown ? 1 : 0)
        + (hopCheck.Visible ? 1 : 0);

    /// <summary>Reads the travel checks once per frame, before the bar's height is planned.</summary>
    private void PrepareTravel(QuestRecord quest)
    {
        teleportCheck = links.CheckTeleport(quest);
        walkCheck = links.WalkShown ? links.CheckWalk(quest) : default;
        goToCheck = links.GoToShown ? links.CheckGoTo(quest) : default;
        hopCheck = links.CheckHop(quest);
    }

    /// <summary>Under the pane's floor (<see cref="DetailTier.Compact"/>) the primary action is an icon button, its label in the tooltip (L5).</summary>
    private bool PrimaryIconOnly => tier == DetailTier.Compact;

    private float PrimaryWidth()
    {
        if (PrimaryIconOnly)
        {
            return UiMetrics.MinTarget;
        }

        var label = links.TeleportAvailable ? Strings.ActionTeleport : Strings.FlagOnMap;
        ImGui.PushFont(UiBuilder.IconFont);
        var icon = ImGui.CalcTextSize(TeleportIcon).X;
        ImGui.PopFont();
        return UiMetrics.Px(12f) + icon + UiMetrics.Px(6f) + ImGui.CalcTextSize(label).X + UiMetrics.Px(14f);
    }

    /// <summary>
    /// The gap between round buttons: half the usual item spacing, so the primary action and every round button fit
    /// one row at the default right-column width (each button keeps its full <see cref="UiMetrics.MinTarget"/>).
    /// </summary>
    private static float RoundGap => UiMetrics.Px(4f);

    /// <summary>
    /// The bar's rows as <see cref="DrawActionBar"/> flows them: the round buttons follow the primary action and wrap
    /// onto as many rows as the pane's width needs, so on a narrow column every button stays reachable.
    /// </summary>
    private int ActionRows(float width)
    {
        var rows = 1;
        var used = PrimaryWidth();
        for (var i = 0; i < IconButtonCount; i++)
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
    /// each action row and its spacing, then the provenance line (a caption).
    /// </summary>
    private float ActionBarHeight()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var rows = ActionRows(ImGui.GetContentRegionAvail().X);
        return UiMetrics.Hairline + ((rows + 2) * spacing) + (rows * UiMetrics.MinTarget) + Typography.CaptionSize;
    }

    /// <summary>
    /// The sticky action bar: one labelled primary action, gold (Flag on map, or Teleport when Lifestream is loaded;
    /// Teleport goes quiet when the player already stands closer to the giver), then round buttons for Pin, Show path,
    /// Route to this, Link in chat, Copy coordinates, Open journal, Report, Flag on map with Teleport leading (or
    /// Teleport, greyed and naming Lifestream, without it), Walk to giver, Go to giver and, in the giver's city, the
    /// aethernet hop. Disabled buttons say why on hover. All are focusable items (accessibility A6).
    /// </summary>
    private void DrawActionBar(QuestRecord quest, uint rowId)
    {
        Chrome.Hairline();
        var width = ImGui.GetContentRegionAvail().X;
        var used = PrimaryWidth();
        var teleportLeads = links.TeleportAvailable;
        if (teleportLeads)
        {
            var check = teleportCheck;
            if (PrimaryButton("##teleport", TeleportIcon, PrimaryIconOnly ? null : Strings.ActionTeleport, check.Ready, null, quiet: check.AlreadyHere))
            {
                links.TeleportToGiver(quest);
            }

            // Composed only on hover: the cost and the "already here" line follow the attuned list and the player.
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(links.TeleportTooltip(quest, check));
            }
        }
        else
        {
            var canFlag = links.CanFlagMap(quest);
            if (PrimaryButton("##flag", FlagIcon, PrimaryIconOnly ? null : Strings.FlagOnMap, canFlag, canFlag ? Strings.FlagOnMap : Strings.ActionFlagUnavailable))
            {
                links.FlagMap(quest);
            }
        }

        NextRound(ref used, width);
        var pinned = model.Pinned;
        var canPin = runner.CanPin;
        if (Chrome.IconButtonRound("##pin", PinIcon, !canPin ? Strings.ActionPinUnavailable : pinned ? Strings.ActionUnpinTooltip : Strings.ActionPinTooltip, pinned, canPin))
        {
            runner.TogglePin(rowId);
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
        if (Chrome.IconButtonRound("##journal", JournalIcon, canOpen ? Strings.OpenJournal : Strings.OpenJournalUnavailable, enabled: canOpen))
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

        if (teleportLeads)
        {
            NextRound(ref used, width);
            var canFlag = links.CanFlagMap(quest);
            if (Chrome.IconButtonRound("##flagIcon", FlagIcon, canFlag ? Strings.FlagOnMap : Strings.ActionFlagUnavailable, enabled: canFlag))
            {
                links.FlagMap(quest);
            }
        }
        else
        {
            // Without Lifestream Teleport stays in view, greyed, and its tooltip names Lifestream (decision 2).
            NextRound(ref used, width);
            Chrome.IconButtonRound("##teleportIcon", TeleportIcon, null, enabled: false);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(links.TeleportTooltip(quest, teleportCheck));
            }
        }

        DrawTravelButtons(quest, ref used, width);
        DrawMoreMenu(ref used, width, quest, rowId);
    }

    /// <summary>
    /// Walk to giver and Go to giver (each Stop while moving; greyed, naming vnavmesh, without it) and, in the giver's
    /// city, the aethernet hop. Their tooltips are composed on hover only.
    /// </summary>
    private void DrawTravelButtons(QuestRecord quest, ref float used, float width)
    {
        if (links.WalkShown)
        {
            NextRound(ref used, width);
            var walk = walkCheck;
            if (Chrome.IconButtonRound("##walk", walk.Stoppable ? StopIcon : WalkIcon, null, active: walk.Stoppable, enabled: walk.Ready || walk.Stoppable))
            {
                if (walk.Stoppable)
                {
                    links.StopTravel();
                }
                else
                {
                    links.WalkToGiver(quest);
                }
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(links.WalkTooltip(quest, walk));
            }
        }

        if (links.GoToShown)
        {
            NextRound(ref used, width);
            var go = goToCheck;
            if (Chrome.IconButtonRound("##goTo", go.Stoppable ? StopIcon : GoToIcon, null, active: go.Stoppable, enabled: go.Ready || go.Stoppable))
            {
                if (go.Stoppable)
                {
                    links.StopTravel();
                }
                else
                {
                    links.GoToGiver(quest);
                }
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(links.GoToTooltip(quest, go));
            }
        }

        if (hopCheck.Visible)
        {
            NextRound(ref used, width);
            var hop = hopCheck;
            if (Chrome.IconButtonRound("##hop", HopIcon, null, enabled: hop.Ready))
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
    }

    /// <summary>
    /// The labelled primary action: a pill of <see cref="UiMetrics.MinTarget"/> height, Moon at 16 % with Moon text
    /// (22 % hovered, 28 % held); disabled, a neutral pill with the reason in the tooltip. Focusable, with the ring.
    /// Without a <paramref name="label"/> it is a round icon button of the same height (the compact tier). A
    /// <paramref name="quiet"/> one (Teleport when the player already stands closer to the giver) stays clickable but
    /// drops the gold: the neutral pill with the primary text tone. A null <paramref name="tooltip"/> leaves the hover
    /// text to the caller.
    /// </summary>
    private static bool PrimaryButton(string id, string icon, string? label, bool enabled, string? tooltip, bool quiet = false)
    {
        var height = UiMetrics.MinTarget;
        ImGui.PushFont(UiBuilder.IconFont);
        var iconSize = ImGui.CalcTextSize(icon);
        ImGui.PopFont();
        var labelSize = label is null ? Vector2.Zero : ImGui.CalcTextSize(label);
        var padX = label is null ? (height - iconSize.X) * 0.5f : UiMetrics.Px(12f);
        var size = label is null ? new Vector2(height, height) : new Vector2(padX + iconSize.X + UiMetrics.Px(6f) + labelSize.X + UiMetrics.Px(14f), height);
        var min = ImGui.GetCursorScreenPos();
        ImGui.BeginDisabled(!enabled);
        var clicked = ImGui.InvisibleButton(id, size);
        ImGui.EndDisabled();
        var hovered = enabled && ImGui.IsItemHovered();
        var held = enabled && ImGui.IsItemActive();

        var dl = ImGui.GetWindowDrawList();
        var max = min + size;
        var rounding = height * 0.5f;
        var s = Theme.Surface;
        if (enabled && !quiet)
        {
            dl.AddRectFilled(min, max, Theme.WithAlpha(Theme.Moon, held ? 0.28f : hovered ? 0.22f : 0.16f), rounding);
            dl.AddRect(min, max, Theme.WithAlpha(Theme.Moon, 0.45f), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        }
        else
        {
            dl.AddRectFilled(min, max, enabled && (held || hovered) ? Theme.WithAlpha(s.Hover, 1f) : Theme.U32(s.Raised), rounding);
        }

        var ink = !enabled ? Theme.U32(s.TextDisabled) : quiet ? Theme.U32(hovered ? s.Text : s.TextSecondary) : Theme.AccentU32;
        ImGui.PushFont(UiBuilder.IconFont);
        dl.AddText(new Vector2(min.X + padX, min.Y + ((height - iconSize.Y) * 0.5f)), ink, icon);
        ImGui.PopFont();
        if (label is not null)
        {
            dl.AddText(new Vector2(min.X + padX + iconSize.X + UiMetrics.Px(6f), min.Y + ((height - labelSize.Y) * 0.5f)), ink, label);
        }

        Chrome.FocusRing(rounding);
        if (tooltip is not null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(tooltip);
        }

        return clicked && enabled;
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
        model.StateNote = null;
        model.QuirkNote = null;
        model.ChainText = null;
        model.ChainNextName = null;
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
                model.Requirements.Add(UnmetLine(session, bundle, quest, result, ReferenceEquals(result, shown.NextStep), detail));
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

    /// <summary>The reward tiles: which rewards are unique for this quest (shipped entries), and which the store sells or a duty drops.</summary>
    private void BuildRewards(SessionState session, QuestRecord quest)
    {
        uniqueByQuest.TryGetValue(quest.RowId, out var entries);
        var unique = 0;
        foreach (var reward in quest.Rewards)
        {
            var isUnique = false;
            if (entries is not null)
            {
                foreach (var entry in entries)
                {
                    if (entry.Kind == reward.Kind && (entry.RewardId == reward.Id || (entry.ItemId != 0 && entry.ItemId == reward.ItemId)))
                    {
                        isUnique = true;
                        break;
                    }
                }
            }

            // The reward tooltip says where else it comes from; the tile's caption only names the kind of source.
            var mark = session.StoreResells.Contains(reward) ? Strings.MoonlitStoreOnly
                : session.StoreResells.DropWhere(reward) is not null ? Strings.MoonlitAlsoDrops
                : null;
            unique += isUnique ? 1 : 0;
            model.Rewards.Add(new RewardTile(reward, isUnique, mark));
        }

        model.RewardsCaption = unique == 0 ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.DetailRewardsUniqueFormat, unique);
    }

    /// <summary>
    /// The chain line for a quest that belongs to one: a curated or genre chain ("Chain: Hildibrand · 12 of 57 done")
    /// or a side story ("Story: &lt;first quest&gt; · 3 of 7 done"), from the session's chain catalog.
    /// </summary>
    private void BuildChain(SessionState session, CatalogBundle bundle, uint rowId)
    {
        if (session.Chains.ForQuest(rowId) is not { } chain)
        {
            return;
        }

        var progress = ChainCatalog.Progress(chain, session.States);
        if (progress.IsEmpty)
        {
            // Nothing in the chain counts for this character: no "0 of 0 done" line.
            return;
        }

        model.ChainFraction = progress.Fraction;
        var name = ChainCatalog.DisplayName(chain, id => session.Spoilers.DisplayName(bundle.Catalog, id, id.ToString(CultureInfo.InvariantCulture)));
        model.ChainText = string.Format(CultureInfo.CurrentCulture, chain.IsStory ? Strings.DetailStoryFormat : Strings.DetailChainFormat, name, progress.Done, progress.Total);
        model.ChainHaloTooltip = string.Format(CultureInfo.CurrentCulture, Strings.DetailChainHaloTooltipFormat, progress.Done, progress.Total);
        if (progress.NextRowId is { } next)
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
