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
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail pane for <see cref="UiState.SelectedRowId"/> as a card stack on a Night panel (ui-revamp §2.5, game UX
/// panel finding 8): a hero at most 96 px tall (the journal banner cover-cropped under a scrim, the state pill and the
/// special badge, the name and a caption line; the Night card when the artwork is hidden or missing), then the
/// Requirements card first with one next-step marker, Rewards as tiles, the Moonlit verdict, the Path card (the chain
/// line once, then the star chart, <see cref="PathChart"/>), the Giver card; under the scrolling stack a sticky action
/// bar with one labelled primary action (Flag on map, or Teleport when Lifestream is loaded) and round buttons for Pin,
/// Show path, Route to this (the unlock route window), Link in chat, Copy coordinates, Open journal and Report, and a plain provenance line. Every action is a
/// focusable item, so the pane works without a mouse (accessibility A6). Everything shown is materialized when the
/// selection or the session version changes, so drawing allocates nothing.
/// </summary>
public sealed class DetailPane
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

    private sealed record RequirementLine(bool Met, bool IsNext, string Label, string Detail);

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

        /// <summary>The quest has a banner the shield hides; the Night card says the art comes later.</summary>
        public bool ArtworkHidden;
        public string JournalPath = string.Empty;

        /// <summary>Provenance of a refiled or removed quest ("Filed under … (rule 4: …)", "Removed from the game in patch 6.3"); null for an ordinary quest.</summary>
        public string? FilingLine;

        /// <summary>"Expansion · Lv N · job", the hero's caption.</summary>
        public string HeaderLine = string.Empty;
        public string StateName = string.Empty;

        /// <summary>What the state pill cannot say: the blocker, the step, the date; empty when the state says it all.</summary>
        public string StatusReason = string.Empty;
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
        DrawHero(quest);
        DrawUnderHero(session, rowId);

        Gap();
        var width = ImGui.GetContentRegionAvail().X;
        var start = ImGui.GetCursorScreenPos();
        Chrome.BeginCard("##requirements", Strings.Requirements, RequirementsIcon);
        CardCaption(model.RequirementsCaption, start.X + width);
        DrawRequirements(start.X);
        Chrome.EndCard();
        ui.RecordItem(UiRects.DetailRequirements);

        Gap();
        start = ImGui.GetCursorScreenPos();
        Chrome.BeginCard("##rewards", Strings.Rewards, RewardsIcon);
        CardCaption(model.RewardsCaption, start.X + width);
        DrawRewards(start.X + width - UiMetrics.Px(10f));
        Chrome.EndCard();

        if (Overrides is { } overrides)
        {
            Gap();
            Chrome.BeginCard("##moonlit", Strings.UniqueSection, MoonlitIcon);
            DrawUnique(overrides, rowId);
            Chrome.EndCard();
        }

        Gap();
        if (pathScrollFrames > 0)
        {
            pathScrollFrames--;
            ImGui.SetScrollHereY(0f);
        }

        start = ImGui.GetCursorScreenPos();
        var pad = UiMetrics.Px(10f);
        Chrome.BeginCard("##path", Strings.Path, PathIcon);
        CardCaption(chart.HeaderCaption, start.X + width);
        DrawChain();
        chart.Draw(width - (2f * pad), 0.4f * detailHeight, pad);
        Chrome.EndCard();
        ui.RecordItem(UiRects.DetailPath);

        Gap();
        Chrome.BeginCard("##giver", Strings.Giver, GiverIcon);
        DrawGiver();
        Chrome.EndCard();
        ui.RecordItem(UiRects.DetailGiver);
        Gap();
    }

    private static void Gap() => ImGui.Dummy(new Vector2(0f, UiMetrics.Px(2f)));

    /// <summary>A card header's right-aligned caption (Dusk, 0.85×) on the title line; draw-list only, so the layout is untouched.</summary>
    private static void CardCaption(string caption, float cardRight)
    {
        if (caption.Length == 0)
        {
            return;
        }

        var titleMin = ImGui.GetItemRectMin();
        var titleHeight = ImGui.GetItemRectSize().Y;
        using var role = Typography.Caption();
        var size = ImGui.GetFontSize();
        var width = ImGui.CalcTextSize(caption).X;
        var pos = new Vector2(cardRight - UiMetrics.Px(10f) - width, titleMin.Y + ((titleHeight - size) * 0.5f));
        ImGui.GetWindowDrawList().AddText(pos, Theme.U32(Theme.Surface.TextTertiary), caption);
    }

    // ------------------------------------------------------------------ hero

    /// <summary>The banner hero, or the Night card when the artwork is hidden, missing or still loading.</summary>
    private void DrawHero(QuestRecord quest)
    {
        if (model.ArtworkHidden || !DrawBanner(quest))
        {
            DrawHeaderCard(quest);
        }
    }

    /// <summary>
    /// The journal banner at the column's width and at most 96 px tall, cover-cropped, a scrim from 30 % of its height
    /// to the bottom, the state pill top left, the special badge top right, and the name over its caption line bottom
    /// left. False when the quest has no banner or it is not loaded yet.
    /// </summary>
    private bool DrawBanner(QuestRecord quest)
    {
        if (quest.Icon == 0 || !textures.GetFromGameIcon(new GameIconLookup(quest.Icon)).TryGetWrap(out var wrap, out _) || wrap.Width <= 0 || wrap.Height <= 0)
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

        // Name (display role) over the caption line (caption role), bottom left.
        var wrapWidth = MathF.Max(UiMetrics.Px(40f), width - (pad * 2f));
        float captionY;
        using (Typography.Caption())
        {
            captionY = max.Y - pad - ImGui.GetFontSize();
            dl.AddText(new Vector2(min.X + pad, captionY), Theme.U32(Theme.Surface.TextSecondary), model.HeaderLine);
        }

        using (Typography.Display())
        {
            var nameHeight = ImGui.CalcTextSize(model.DisplayName, false, wrapWidth).Y;
            var nameY = captionY - UiMetrics.Px(2f) - nameHeight;
            dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), new Vector2(min.X + pad, nameY), Theme.U32(Theme.Surface.Text), model.DisplayName, wrapWidth);
        }

        // State pill top left; its item carries the state tooltip.
        var pillMin = min + new Vector2(pad, pad);
        var pillSize = StatePill(dl, pillMin);
        ImGui.SetCursorScreenPos(pillMin);
        ImGui.InvisibleButton("##heroState", pillSize);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.StateTooltip(model.State, model.Evaluation, quest, BlockerNamesOf(), lastStates);
        }

        if (quest.IconSpecial != 0)
        {
            var size = MathF.Min(UiMetrics.BannerBadgeSize, UiMetrics.Px(22f));
            var badgeMin = new Vector2(max.X - pad - size, min.Y + pad);
            if (DrawSpecialBadge(dl, quest, badgeMin, size))
            {
                ImGui.SetCursorScreenPos(badgeMin);
                ImGui.InvisibleButton("##heroBadge", new Vector2(size, size));
                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(BadgeTooltip(quest));
                }
            }
        }

        ImGui.SetCursorScreenPos(after);
        return true;
    }

    private BlockerNames BlockerNamesOf() => lastNames ?? BlockerNames.Default;

    // The session's names and states as of the last refresh, for the state tooltip's blocker line.
    private BlockerNames? lastNames;
    private IReadOnlyDictionary<uint, QuestEvaluation>? lastStates;

    /// <summary>
    /// The state pill (ui-revamp §2.5): the state colour at 18 % over a dark base, a 1 px border at 70 %, a 12 px moon and
    /// the state's name in its text tone. Returns its size.
    /// </summary>
    private Vector2 StatePill(ImDrawListPtr dl, Vector2 min)
    {
        using var caption = Typography.Caption();
        var captionSize = ImGui.GetFontSize();
        var moon = UiMetrics.Icon(6f);
        var height = MathF.Max(UiMetrics.Px(20f), MathF.Max(captionSize + UiMetrics.Px(6f), (moon * 2f) + UiMetrics.Px(6f)));
        var textWidth = ImGui.CalcTextSize(model.StateName).X;
        var size = new Vector2(UiMetrics.Px(5f) + (moon * 2f) + UiMetrics.Px(6f) + textWidth + UiMetrics.Px(8f), height);
        var max = min + size;
        var tone = Theme.StateColor(model.State);
        var rounding = height * 0.5f;
        dl.AddRectFilled(min, max, Theme.WithAlpha(Theme.Night, 0.6f), rounding);
        dl.AddRectFilled(min, max, Theme.WithAlpha(tone, 0.18f), rounding);
        dl.AddRect(min, max, Theme.WithAlpha(tone, 0.7f), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        var center = new Vector2(min.X + UiMetrics.Px(5f) + moon, min.Y + (height * 0.5f));
        MoonGlyph.Draw(dl, center, moon, model.State);
        dl.AddText(new Vector2(center.X + moon + UiMetrics.Px(6f), min.Y + ((height - captionSize) * 0.5f)), Theme.U32(StateTextColor(model.State)), model.StateName);
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
    /// false (nothing drawn) while the texture is still loading.
    /// </summary>
    private bool DrawSpecialBadge(ImDrawListPtr dl, QuestRecord quest, Vector2 min, float size)
    {
        if (!textures.GetFromGameIcon(new GameIconLookup(quest.IconSpecial)).TryGetWrap(out var wrap, out _))
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
        var radius = UiMetrics.Icon(20f);
        var box = radius * 2.3f;
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(box, box));
        MoonGlyph.Draw(dl, pos + new Vector2(box * 0.5f), radius, model.State);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.StateTooltip(model.State, model.Evaluation, quest, BlockerNamesOf(), lastStates);
        }

        ImGui.SameLine();
        if (quest.IconSpecial != 0)
        {
            var size = MathF.Min(UiMetrics.BannerBadgeSize, UiMetrics.Px(22f));
            var badgeMin = ImGui.GetCursorScreenPos() + new Vector2(0f, (box - size) * 0.5f);
            ImGui.Dummy(new Vector2(size, box));
            if (DrawSpecialBadge(dl, quest, badgeMin, size) && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(BadgeTooltip(quest));
            }

            ImGui.SameLine();
        }

        // The name wraps at the card's inner edge (Chrome pushes the wrap position).
        using (ImRaii.Group())
        {
            using (Typography.Display())
            using (Theme.PushText(Theme.Surface.Text))
            {
                ImGui.TextWrapped(model.DisplayName);
            }

            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextWrapped(model.HeaderLine);
                if (model.ArtworkHidden)
                {
                    ImGui.TextWrapped(Strings.ArtworkHidden);
                }
            }

            var pillPos = ImGui.GetCursorScreenPos();
            var pillSize = StatePill(dl, pillPos);
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
            ImGui.TextDisabled(Strings.SpoilerMaskedNote);
            ImGui.SameLine();
            if (ImGui.SmallButton(Strings.SpoilerRevealName))
            {
                session.RevealName(rowId);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.SpoilerRevealNameTooltip);
            }
        }

        if (model.StatusReason.Length > 0 || model.StateNote is not null)
        {
            using (Theme.PushText(StateTextColor(model.State)))
            {
                ImGui.TextWrapped(model.StatusReason.Length > 0 ? model.StatusReason : model.StateNote!);
            }

            if (model.StatusReason.Length > 0 && model.StateNote is { } note)
            {
                ImGui.TextDisabled(note);
            }
        }

        ImGui.TextDisabled(model.JournalPath);
        if (model.FilingLine is { } filing)
        {
            using var mist = Theme.PushText(Theme.Surface.TextSecondary);
            ImGui.TextWrapped(filing);
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
            using var mist = Theme.PushText(Theme.Surface.TextSecondary);
            ImGui.TextWrapped(Strings.RequirementsNeedSnapshot);
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
            using var dusk = Theme.PushText(Theme.Surface.TextSecondary);
            ImGui.TextWrapped(note);
        }
    }

    private void DrawRequirementLines(float cardLeft)
    {
        var dl = ImGui.GetWindowDrawList();
        var lineHeight = ImGui.GetTextLineHeight();
        var mark = UiMetrics.RequirementMarkSize;
        var box = MathF.Max(lineHeight, mark);
        foreach (var line in model.Requirements)
        {
            // Met is a check, unmet a cross: a moon means a quest state or a fraction only (accessibility B2).
            var pos = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(box, lineHeight));
            Marks.Draw(dl, pos + new Vector2(box * 0.5f, lineHeight * 0.5f), mark, line.Met ? Mark.Check : Mark.Cross);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(line.Met ? Strings.MetTooltip : Strings.UnmetTooltip);
            }

            ImGui.SameLine();
            using (Theme.PushText(line.IsNext ? Theme.Moon : Theme.Surface.Text))
            {
                ImGui.TextUnformatted(line.Label);
            }

            if (line.Detail.Length > 0)
            {
                ImGui.SameLine();
                using var mist = Theme.PushText(Theme.Surface.TextSecondary);
                ImGui.TextWrapped(line.Detail);
            }

            if (line.IsNext)
            {
                var bottom = ImGui.GetItemRectMax().Y;
                var x = cardLeft + UiMetrics.Px(1f);
                dl.AddRectFilled(new Vector2(x, pos.Y - UiMetrics.Px(1f)), new Vector2(x + MathF.Max(2f, UiMetrics.Px(2f)), bottom + UiMetrics.Px(1f)), Theme.MoonU32);
            }
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
                var wrap = textures.GetFromGameIcon(new GameIconLookup(reward.Reward.Icon)).GetWrapOrEmpty();
                var iconMin = min + new Vector2((tile - iconSize) * 0.5f);
                dl.AddImageRounded(wrap.Handle, iconMin, iconMin + new Vector2(iconSize), Vector2.Zero, Vector2.One, 0xFFFFFFFFu, UiMetrics.Px(4f));
            }

            if (reward.Unique)
            {
                dl.AddRect(min, max, Theme.MoonU32, rounding, ImDrawFlags.None, MathF.Max(1.5f, UiMetrics.Px(1.5f)));
                Crescent(dl, new Vector2(max.X - UiMetrics.Px(1f), min.Y + UiMetrics.Px(1f)), MathF.Max(3f, UiMetrics.Px(4f)));
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
        if (overrides.Get(rowId) is { } stored)
        {
            using (Theme.PushText(stored.Unique ? Theme.Surface.Text : Theme.Surface.TextSecondary))
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
    /// place the chain is shown); the next quest's name selects it. Nothing is drawn for a quest outside every chain.
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

        // The glyph box is taller than a text line; centre the text on it.
        ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((size - lineHeight) * 0.5f));
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextUnformatted(text);
        }

        ImGui.SameLine();
        if (model.ChainNextName is not { } next)
        {
            using var done = Theme.PushText(Theme.AccentDim);
            ImGui.TextUnformatted(Strings.DetailChainComplete);
            return;
        }

        ImGui.TextDisabled(Strings.DetailChainNext);
        ImGui.SameLine();
        using (Theme.PushText(Theme.Moon))
        {
            if (ImGui.Selectable(next, false, ImGuiSelectableFlags.None, ImGui.CalcTextSize(next)))
            {
                RevealRow(model.ChainNextRowId);
            }
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            UiMetrics.Tooltip(Strings.DetailChainNextTooltip);
        }
    }

    private void DrawGiver()
    {
        if (model.GiverName is null)
        {
            ImGui.TextDisabled(Strings.NoGiver);
            return;
        }

        ImGui.TextUnformatted(model.GiverName);
        using var mist = Theme.PushText(Theme.Surface.TextSecondary);
        if (model.PlaceLine is { } place)
        {
            ImGui.TextWrapped(place);
        }
        else
        {
            ImGui.TextUnformatted(Strings.DetailNoGiverPlace);
        }
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

    private uint teleportTipRowId = uint.MaxValue;
    private string teleportTip = string.Empty;

    /// <summary>Round buttons after the primary action: Pin, Show path, Route to this, Link, Copy, Journal, Report (when attached), Flag (when Teleport leads).</summary>
    private int IconButtonCount => 6 + (Diagnostics is null ? 0 : 1) + (links.TeleportAvailable ? 1 : 0);

    private float PrimaryWidth()
    {
        var label = links.TeleportAvailable ? Strings.ActionTeleport : Strings.FlagOnMap;
        ImGui.PushFont(UiBuilder.IconFont);
        var icon = ImGui.CalcTextSize(TeleportIcon).X;
        ImGui.PopFont();
        return UiMetrics.Px(12f) + icon + UiMetrics.Px(6f) + ImGui.CalcTextSize(label).X + UiMetrics.Px(14f);
    }

    /// <summary>The bar's rows: one when everything fits the pane's width, else the round buttons wrap to a second.</summary>
    private int ActionRows(float width)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var total = PrimaryWidth() + (IconButtonCount * (UiMetrics.MinTarget + spacing));
        return total <= width ? 1 : 2;
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
    /// The sticky action bar: one labelled primary action, gold (Flag on map, or Teleport when Lifestream is loaded),
    /// then round buttons for Pin, Show path, Route to this, Link in chat, Copy coordinates, Open journal, Report and, with Teleport
    /// leading, Flag on map. Disabled buttons say why on hover. All are focusable items (accessibility A6).
    /// </summary>
    private void DrawActionBar(QuestRecord quest, uint rowId)
    {
        Chrome.Hairline();
        var width = ImGui.GetContentRegionAvail().X;
        var wrap = ActionRows(width) > 1;
        var teleportLeads = links.TeleportAvailable;
        if (teleportLeads)
        {
            var canTeleport = links.CanTeleport(quest);
            if (teleportTipRowId != rowId)
            {
                // The aetheryte is fixed per quest: named once per selection, not per frame.
                teleportTipRowId = rowId;
                teleportTip = links.NearestAetheryte(quest) is { } aetheryte
                    ? string.Format(CultureInfo.CurrentCulture, Strings.ActionTeleportTooltipFormat, aetheryte.Name)
                    : Strings.TeleportNoAetheryte;
            }

            var tip = links.TeleportBusy ? Strings.TeleportBusy : teleportTip;
            if (PrimaryButton("##teleport", TeleportIcon, Strings.ActionTeleport, canTeleport, tip))
            {
                links.TeleportToGiver(quest);
            }
        }
        else
        {
            var canFlag = links.CanFlagMap(quest);
            if (PrimaryButton("##flag", FlagIcon, Strings.FlagOnMap, canFlag, canFlag ? Strings.FlagOnMap : Strings.ActionFlagUnavailable))
            {
                links.FlagMap(quest);
            }
        }

        if (!wrap)
        {
            ImGui.SameLine();
        }

        var pinned = model.Pinned;
        var canPin = runner.CanPin;
        if (Chrome.IconButtonRound("##pin", PinIcon, !canPin ? Strings.ActionPinUnavailable : pinned ? Strings.ActionUnpinTooltip : Strings.ActionPinTooltip, pinned, canPin))
        {
            runner.TogglePin(rowId);
        }

        ImGui.SameLine();
        if (Chrome.IconButtonRound("##showPath", ShowPathIcon, Strings.ActionShowPathTooltip))
        {
            ui.ShowPath(rowId);
        }

        ImGui.SameLine();
        if (Chrome.IconButtonRound("##route", RouteIcon, Strings.RouteToThisTooltip))
        {
            ui.OpenRoute(Core.Route.RouteTarget.ForQuest(rowId, model.DisplayName));
        }

        ImGui.SameLine();
        if (Chrome.IconButtonRound("##link", LinkIcon, Strings.LinkInChat))
        {
            links.PrintQuestLink(quest);
        }

        ImGui.SameLine();
        var hasCoordinates = model.CoordinateText is not null;
        if (Chrome.IconButtonRound("##copy", CopyIcon, hasCoordinates ? Strings.CopyCoordinatesTooltip : Strings.ActionCopyCoordinatesUnavailable, enabled: hasCoordinates)
            && links.CoordinateText(quest) is { } coordinates)
        {
            ImGui.SetClipboardText(coordinates);
        }

        ImGui.SameLine();
        var canOpen = GameLinks.CanOpenJournal(quest, model.State);
        if (Chrome.IconButtonRound("##journal", JournalIcon, canOpen ? Strings.OpenJournal : Strings.OpenJournalUnavailable, enabled: canOpen))
        {
            links.OpenJournal(quest);
        }

        if (Diagnostics is { } diagnostics)
        {
            ImGui.SameLine();
            if (Chrome.IconButtonRound("##report", ReportIcon, Strings.ReportTooltip))
            {
                var copied = DiagnosticBuilder.TryCopy(diagnostics.Compose(quest), log ?? Plugin.Log);
                reportNoteUntil = copied ? ImGui.GetTime() + ReportNoteSeconds : 0.0;
                reportNoteRowId = rowId;
            }
        }

        if (teleportLeads)
        {
            ImGui.SameLine();
            var canFlag = links.CanFlagMap(quest);
            if (Chrome.IconButtonRound("##flagIcon", FlagIcon, canFlag ? Strings.FlagOnMap : Strings.ActionFlagUnavailable, enabled: canFlag))
            {
                links.FlagMap(quest);
            }
        }
    }

    /// <summary>
    /// The labelled primary action: a pill of <see cref="UiMetrics.MinTarget"/> height, Moon at 16 % with Moon text
    /// (22 % hovered, 28 % held); disabled, a neutral pill with the reason in the tooltip. Focusable, with the ring.
    /// </summary>
    private static bool PrimaryButton(string id, string icon, string label, bool enabled, string tooltip)
    {
        var height = UiMetrics.MinTarget;
        ImGui.PushFont(UiBuilder.IconFont);
        var iconSize = ImGui.CalcTextSize(icon);
        ImGui.PopFont();
        var labelSize = ImGui.CalcTextSize(label);
        var padX = UiMetrics.Px(12f);
        var size = new Vector2(padX + iconSize.X + UiMetrics.Px(6f) + labelSize.X + UiMetrics.Px(14f), height);
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
        if (enabled)
        {
            dl.AddRectFilled(min, max, Theme.WithAlpha(Theme.Moon, held ? 0.28f : hovered ? 0.22f : 0.16f), rounding);
            dl.AddRect(min, max, Theme.WithAlpha(Theme.Moon, 0.45f), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        }
        else
        {
            dl.AddRectFilled(min, max, Theme.U32(s.Raised), rounding);
        }

        var ink = enabled ? Theme.AccentU32 : Theme.U32(s.TextDisabled);
        ImGui.PushFont(UiBuilder.IconFont);
        dl.AddText(new Vector2(min.X + padX, min.Y + ((height - iconSize.Y) * 0.5f)), ink, icon);
        ImGui.PopFont();
        dl.AddText(new Vector2(min.X + padX + iconSize.X + UiMetrics.Px(6f), min.Y + ((height - labelSize.Y) * 0.5f)), ink, label);
        Chrome.FocusRing(rounding);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
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

        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Surface.TextTertiary);
        ImGui.TextUnformatted(provenance);
        ImGui.PopStyleColor();
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
        model.RequirementsCaption = string.Empty;
        model.RewardsCaption = string.Empty;
        model.Evaluation = null;
        lastNames = session.Names;
        lastStates = session.States;

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
        model.ArtworkHidden = quest.Icon != 0 && !spoilers.ShowArtwork(quest, model.State);
        model.StateName = Strings.StateName(model.State, quest);
        var status = BlockerText.StatusText(evaluation, quest, session.Names, session.States);
        var prefix = model.StateName + BlockerText.Separator;
        model.StatusReason = status.StartsWith(prefix, StringComparison.Ordinal) ? status[prefix.Length..] : status == model.StateName ? string.Empty : status;
        model.HasUniqueEntries = HasShippedUniqueEntry(session.UniqueRewards, rowId);

        model.JournalPath = quest.IsUnlisted
            ? Strings.RemovedFromGame
            : string.Format(CultureInfo.CurrentCulture, Strings.JournalPathFormat, quest.Journal.GenreName, quest.Journal.CategoryName);
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

        if (evaluation is not null)
        {
            // The status line already carries the step ("In journal · step 3 of 7") and the blocker; only the job
            // that can take the quest is a note beside it.
            if (evaluation.ReadyOnJob is { } job)
            {
                model.StateNote = string.Format(CultureInfo.CurrentCulture, Strings.ReadyOnJobFormat, bundle.Names.ClassJobAbbreviation(job));
            }

            var unmet = 0;
            foreach (var result in evaluation.Requirements)
            {
                // The evaluator wrote the prerequisite's or lock's real name into the detail; the shield masks it here.
                var detail = result.Req switch
                {
                    PreviousQuestsRequirement p => spoilers.MaskNamesIn(result.Detail, bundle.Catalog, p.QuestIds),
                    ForeclosureRequirement f => spoilers.MaskNamesIn(result.Detail, bundle.Catalog, f.CompletedLockIds),
                    _ => result.Detail,
                };
                model.Requirements.Add(new RequirementLine(result.Met, ReferenceEquals(result, evaluation.NextStep), Strings.RequirementName(result.Req.Kind), detail));
                unmet += result.Met ? 0 : 1;
            }

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
