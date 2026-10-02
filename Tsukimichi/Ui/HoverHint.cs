using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.HandIn;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unique;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// A small borderless window shown while the game's item tooltip is up for an item that is a quest-exclusive reward
/// (V2-14): one line per quest with its state moon, "Quest reward: name" and either "done" in Moon or the next step
/// in Dusk; for rewards the client can report (mounts, minions, rolls, cards, ornaments) a second line says owned,
/// not owned or veiled. It is drawn from <c>UiBuilder.Draw</c>, outside the window system, so it has no chrome and
/// takes no input; the game tooltip keeps the mouse, so nothing here is clickable.
/// <para>
/// 1.6.0: an item an open quest asks for (in the journal or ready to take; <see cref="HandInIndex"/>) gets a line per
/// quest too, "Needed for: name" with its state and status, while <see cref="NeededForEnabled"/> says so. The two kinds
/// of line share <see cref="MaxQuestLines"/>.
/// </para>
/// <para>
/// Placement: beside the <c>ItemDetail</c> addon when it is visible (right of it, else left, below or above, whichever
/// fits the viewport), otherwise beside a small box at the cursor. The hint model is memoized per item id, session
/// version and lookup, so hovering costs no allocation after the first frame of a new item.
/// </para>
/// <para>
/// Behind the addon kill switch (T20): on a game version newer than the tested one <see cref="Draw"/> returns before
/// reading <see cref="IGameGui.HoveredItem"/> or the addon, unless the player enabled hooks on this untested version.
/// </para>
/// </summary>
public sealed class HoverHint
{
    /// <summary>Name of the game's item tooltip addon.</summary>
    public const string ItemDetailAddon = "ItemDetail";

    /// <summary>Quests listed before the hint folds the rest into "and N more".</summary>
    public const int MaxQuestLines = 5;

    /// <summary>Frames a new model is drawn transparent while ImGui settles its auto-resized size.</summary>
    private const int SettleFrames = 2;

    private const float GapPx = 6f;
    private const float CursorBoxPx = 24f;
    private const float RoundingPx = 4f;

    private const ImGuiWindowFlags HintFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking |
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoInputs |
        ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus;

    private static readonly Vector4 HintBackground = Theme.Night with { W = 0.96f };

    private readonly IGameGui gameGui;
    private readonly SessionState session;
    private readonly RewardUnlockReader unlocks;
    private readonly RewardLookupSource lookup;
    private readonly HookGate gate;
    private readonly IPluginLog log;
    private readonly List<Line> lines = [];
    private readonly HashSet<uint> seen = [];

    private uint modelItem;
    private int modelVersion = -1;
    private RewardLookup? modelLookup;
    private HandInIndex? modelHandIns;
    private bool modelNeededFor;
    private string moreText = string.Empty;
    private Vector2 size;
    private int settled;
    private bool warned;

    /// <param name="gate">The shared addon kill switch (T20): while it pauses game hooks the hint reads nothing from the game.</param>
    public HoverHint(IGameGui gameGui, SessionState session, RewardUnlockReader unlocks, RewardLookupSource lookup, HookGate gate, IPluginLog log)
    {
        this.gameGui = gameGui ?? throw new ArgumentNullException(nameof(gameGui));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
        this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Follows <c>Configuration.ItemHintsEnabled</c>; off draws nothing and forgets the current model.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Item to the quests that ask for it; null leaves the "Needed for" lines out.</summary>
    public HandInIndexSource? HandIns { get; set; }

    /// <summary>Reads Settings › "Say which open quests need an item"; null reads as on.</summary>
    public Func<bool>? NeededForEnabled { get; set; }

    /// <summary>
    /// Whether the hint reads the hovered item and the <c>ItemDetail</c> addon this frame: the setting is on and the
    /// <see cref="HookGate"/> allows game hooks on the running game version.
    /// </summary>
    public bool IsActive => Enabled && gate.HooksAllowed;

    /// <summary>Whether the reward kind is one <see cref="RewardUnlockReader"/> reads from an unlock flag, so an owned line makes sense.</summary>
    public static bool IsUnlockable(RewardKind kind) => Core.Unique.Collectibles.IsStored(kind);

    /// <summary><c>UiBuilder.Draw</c> handler.</summary>
    public void Draw()
    {
        // Paused by the kill switch or turned off: return before the first read of game state.
        if (!IsActive)
        {
            Forget();
            return;
        }

        var hovered = gameGui.HoveredItem;
        if (hovered == 0)
        {
            return;
        }

        var itemId = RewardLookup.NormalizeItemId(hovered);
        var current = lookup.Current;
        var handIns = HandIns?.Current;
        var neededFor = NeededForEnabled?.Invoke() != false;
        if (itemId != modelItem || modelVersion != session.Version || !ReferenceEquals(modelLookup, current)
            || !ReferenceEquals(modelHandIns, handIns) || modelNeededFor != neededFor)
        {
            modelHandIns = handIns;
            modelNeededFor = neededFor;
            Rebuild(itemId, current);
        }

        if (lines.Count == 0)
        {
            return;
        }

        DrawWindow();
    }

    private void Forget()
    {
        if (modelItem == 0)
        {
            return;
        }

        lines.Clear();
        modelItem = 0;
        modelVersion = -1;
        modelLookup = null;
        modelHandIns = null;
    }

    private void Rebuild(uint itemId, RewardLookup current)
    {
        lines.Clear();
        moreText = string.Empty;
        modelItem = itemId;
        modelVersion = session.Version;
        modelLookup = current;
        settled = 0;

        var entries = current.ByItem(itemId);
        var quests = 0;
        seen.Clear();
        foreach (var entry in entries)
        {
            var quest = current.QuestFor(entry);
            if (quest is null || !seen.Add(quest.RowId))
            {
                continue;
            }

            quests++;
            if (quests > MaxQuestLines)
            {
                continue;
            }

            // The item is in the logged-in character's inventory: its state and blocker follow that character (and its
            // spoiler shield), falling back to the viewed character when nobody is logged in.
            var states = session.LiveStates.Count > 0 ? session.LiveStates : session.States;
            var names = session.LiveStates.Count > 0 ? session.LiveNames : session.Names;
            var evaluation = states.GetValueOrDefault(quest.RowId);
            var state = evaluation?.State ?? QuestState.Unknown;
            var done = state == QuestState.Completed;
            var status = done ? Strings.ItemsDone : BlockerText.StatusText(evaluation, quest, names, states);
            var line = new Line(quest.RowId, state, string.Format(CultureInfo.CurrentCulture, Strings.ItemsQuestRewardFormat, session.LiveSpoilers.DisplayName(quest)), status, done, entry.SoldOnOnlineStore,
                entry.DropsInDuty ? Strings.AlsoDropsLine(entry.DropWhere) : string.Empty);
            if (IsUnlockable(entry.Kind))
            {
                // The reader answers for the viewed character; while another one is viewed, the logged-in character's
                // flag is not what it reads, so the line says unknown rather than speak for the wrong character. The
                // hint only shows over an item in game, so someone is logged in: until the poller's first pass commits
                // (no live content id yet) the session cannot tell whether the viewed character is that one either.
                var forLive = session.IsLive;
                line.SetObtained(forLive ? unlocks.IsObtained(entry) : null);
            }

            lines.Add(line);
        }

        quests += AddNeededLines(itemId, quests);
        if (quests > MaxQuestLines)
        {
            moreText = string.Format(CultureInfo.CurrentCulture, Strings.ItemsMoreFormat, quests - MaxQuestLines);
        }
    }

    /// <summary>
    /// "Needed for: quest" for each open quest that asks for the item and has no reward line already; returns how many
    /// quests that was (lines past <see cref="MaxQuestLines"/> are counted, not added).
    /// </summary>
    private int AddNeededLines(uint itemId, int shown)
    {
        if (!modelNeededFor || modelHandIns is not { } handIns)
        {
            return 0;
        }

        // The logged-in character holds the item; while nobody is (cannot happen over a game item) the viewed one.
        var states = session.LiveStates.Count > 0 ? session.LiveStates : session.States;
        var names = session.LiveStates.Count > 0 ? session.LiveNames : session.Names;
        var added = 0;
        foreach (var quest in handIns.NeededFor(itemId, states))
        {
            if (!seen.Add(quest.RowId))
            {
                continue;
            }

            added++;
            if (shown + added > MaxQuestLines)
            {
                continue;
            }

            var evaluation = states.GetValueOrDefault(quest.RowId);
            var state = evaluation?.State ?? QuestState.Unknown;
            var status = BlockerText.StatusText(evaluation, quest, names, states);
            lines.Add(new Line(quest.RowId, state, string.Format(CultureInfo.CurrentCulture, Strings.ItemsNeededForFormat, session.LiveSpoilers.DisplayName(quest)), status, false, false, string.Empty));
        }

        return added;
    }

    private void DrawWindow()
    {
        var viewport = ImGuiHelpers.MainViewport;
        var bounds = new ScreenRect(viewport.Pos, viewport.Pos + viewport.Size);
        var target = TooltipRect() ?? ScreenRect.FromSize(ImGui.GetMousePos(), new Vector2(CursorBoxPx * UiMetrics.Scale));
        var gap = GapPx * UiMetrics.Scale;
        var pos = settled >= SettleFrames
            ? OverlayGeometry.PlaceCard(in target, size, in bounds, gap, out _)
            : new Vector2(target.Max.X + gap, target.Min.Y);
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);

        using var colors = ImRaii.PushColor(ImGuiCol.WindowBg, HintBackground)
                                 .Push(ImGuiCol.Border, Theme.Veil)
                                 .Push(ImGuiCol.Text, Theme.Silver)
                                 .Push(ImGuiCol.TextDisabled, Theme.Dusk);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, RoundingPx * UiMetrics.Scale)
                                 .Push(ImGuiStyleVar.WindowBorderSize, 1f)
                                 .Push(ImGuiStyleVar.WindowPadding, new Vector2(8f, 6f) * UiMetrics.Scale)
                                 .Push(ImGuiStyleVar.Alpha, 0f, settled < SettleFrames);

        var visible = ImGui.Begin(Strings.ItemsHintWindowId, HintFlags);
        try
        {
            if (visible)
            {
                size = ImGui.GetWindowSize();
                if (settled < SettleFrames)
                {
                    settled++;
                }

                DrawLines();
            }
        }
        finally
        {
            ImGui.End();
        }
    }

    private void DrawLines()
    {
        UiMetrics.ApplyFontScale();
        // A long quest name or blocker wraps like any tooltip instead of stretching the hint across the screen.
        var wrapRight = ImGui.GetCursorPosX() + UiMetrics.TooltipWrapWidth;
        using var wrap = UiMetrics.TooltipWrap();
        var lineHeight = ImGui.GetTextLineHeight();
        var glyph = UiMetrics.InlineGlyphSize(lineHeight);
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var indent = glyph + spacing;

        ImGui.TextDisabled(Strings.ItemsHintTitle);
        foreach (var line in lines)
        {
            MoonGlyph.DrawInline(line.State, glyph);
            ImGui.SameLine();
            var questRight = ImGui.GetCursorPosX() + ImGui.CalcTextSize(line.QuestText).X;
            ImGui.TextUnformatted(line.QuestText);

            // The status follows the name while both fit the wrap width; otherwise (a long name, a long blocker, or a
            // name that wrapped) it takes its own line under the name, where it wraps across the whole width instead
            // of down the narrow column the name leaves.
            var beside = questRight + spacing + ImGui.CalcTextSize(line.StatusText).X <= wrapRight;
            if (beside)
            {
                ImGui.SameLine();
            }
            else
            {
                ImGui.Indent(indent);
            }

            using (Theme.PushText(line.Done ? Theme.Moon : Theme.Dusk))
            {
                ImGui.TextUnformatted(line.StatusText);
            }

            if (!beside)
            {
                ImGui.Unindent(indent);
            }

            if (line.HasObtained)
            {
                ImGui.Indent(indent);
                Marks.DrawInline(line.ObtainedGlyph, glyph);
                ImGui.SameLine();
                using (Theme.PushText(line.ObtainedColor))
                {
                    ImGui.TextUnformatted(line.ObtainedText);
                }

                ImGui.Unindent(indent);
            }

            if (line.StoreResell)
            {
                // Also on the FFXIV Online Store (curated/online_store.json): the hint takes no input, so no tooltip; the line says why.
                ImGui.Indent(indent);
                ImGui.TextDisabled(Strings.ItemsStoreOnly);
                ImGui.Unindent(indent);
            }

            if (line.DropLine.Length > 0)
            {
                // A duty drops it too (curated/other_sources.json): the same "Also drops in …" line as the reward tooltip.
                ImGui.Indent(indent);
                ImGui.TextDisabled(line.DropLine);
                ImGui.Unindent(indent);
            }
        }

        if (moreText.Length > 0)
        {
            ImGui.TextDisabled(moreText);
        }
    }

    /// <summary>Screen rectangle of the game's item tooltip, or null when it is not up (or cannot be read; logged once).</summary>
    private ScreenRect? TooltipRect()
    {
        try
        {
            var addon = gameGui.GetAddonByName(ItemDetailAddon);
            if (addon.IsNull || !addon.IsVisible)
            {
                return null;
            }

            var addonSize = addon.ScaledSize;
            if (addonSize.X <= 0f || addonSize.Y <= 0f)
            {
                return null;
            }

            return ScreenRect.FromSize(addon.Position, addonSize);
        }
        catch (Exception ex)
        {
            if (!warned)
            {
                warned = true;
                log.Warning(ex, "Item tooltip position unavailable; placing the hint at the cursor");
            }

            return null;
        }
    }

    /// <summary>One quest of the hint, with its strings built once.</summary>
    private sealed class Line(uint questRowId, QuestState state, string questText, string statusText, bool done, bool storeResell, string dropLine)
    {
        public uint QuestRowId { get; } = questRowId;
        public QuestState State { get; } = state;
        public string QuestText { get; } = questText;
        public string StatusText { get; } = statusText;
        public bool Done { get; } = done;

        /// <summary>The FFXIV Online Store also sells this reward (entry OtherSources carries OnlineStore).</summary>
        public bool StoreResell { get; } = storeResell;

        /// <summary>"Also drops in …" when a duty also drops this reward (entry OtherSources carries DungeonDrop); empty otherwise.</summary>
        public string DropLine { get; } = dropLine;

        public bool HasObtained { get; private set; }
        public Mark ObtainedGlyph { get; private set; } = Mark.Unknown;
        public string ObtainedText { get; private set; } = string.Empty;
        public Vector4 ObtainedColor { get; private set; } = Theme.Dusk;

        public void SetObtained(bool? obtained)
        {
            HasObtained = true;
            (ObtainedGlyph, ObtainedText, ObtainedColor) = obtained switch
            {
                true => (Mark.Check, Strings.ItemsOwned, Theme.Moon),
                false => (Mark.Cross, Strings.ItemsNotOwned, Theme.Dusk),
                null => (Mark.Unknown, Strings.ItemsVeiled, Theme.Dusk),
            };
        }
    }
}
