using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Model;
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
/// Placement: beside the <c>ItemDetail</c> addon when it is visible (right of it, else left, below or above, whichever
/// fits the viewport), otherwise beside a small box at the cursor. The hint model is memoized per item id, session
/// version and lookup, so hovering costs no allocation after the first frame of a new item.
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
    private readonly IPluginLog log;
    private readonly List<Line> lines = [];

    private uint modelItem;
    private int modelVersion = -1;
    private RewardLookup? modelLookup;
    private string moreText = string.Empty;
    private Vector2 size;
    private int settled;
    private bool warned;

    public HoverHint(IGameGui gameGui, SessionState session, RewardUnlockReader unlocks, RewardLookupSource lookup, IPluginLog log)
    {
        this.gameGui = gameGui ?? throw new ArgumentNullException(nameof(gameGui));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
        this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Follows <c>Configuration.ItemHintsEnabled</c>; off draws nothing and forgets the current model.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Whether the reward kind is one <see cref="RewardUnlockReader"/> reads from the live client, so an owned line makes sense.</summary>
    public static bool IsUnlockable(RewardKind kind) => kind is
        RewardKind.Emote or RewardKind.Minion or RewardKind.Mount or RewardKind.Orchestrion or RewardKind.Ornament or
        RewardKind.TripleTriadCard or RewardKind.AetherCurrent or RewardKind.Instance or RewardKind.DutyUnlock;

    /// <summary><c>UiBuilder.Draw</c> handler.</summary>
    public void Draw()
    {
        if (!Enabled)
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
        if (itemId != modelItem || modelVersion != session.Version || !ReferenceEquals(modelLookup, current))
        {
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
        foreach (var entry in entries)
        {
            var quest = current.QuestFor(entry);
            if (quest is null || Listed(quest.RowId))
            {
                continue;
            }

            quests++;
            if (quests > MaxQuestLines)
            {
                continue;
            }

            var evaluation = session.States.GetValueOrDefault(quest.RowId);
            var state = evaluation?.State ?? QuestState.Unknown;
            var done = state == QuestState.Completed;
            var status = done ? Strings.ItemsDone : evaluation?.NextStep?.Detail ?? Strings.StateName(state);
            var line = new Line(quest.RowId, state, string.Format(CultureInfo.CurrentCulture, Strings.ItemsQuestRewardFormat, quest.Name), status, done);
            if (IsUnlockable(entry.Kind))
            {
                line.SetObtained(unlocks.IsObtained(entry));
            }

            lines.Add(line);
        }

        if (quests > MaxQuestLines)
        {
            moreText = string.Format(CultureInfo.CurrentCulture, Strings.ItemsMoreFormat, quests - MaxQuestLines);
        }
    }

    private bool Listed(uint rowId)
    {
        foreach (var line in lines)
        {
            if (line.QuestRowId == rowId)
            {
                return true;
            }
        }

        return false;
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
        var lineHeight = ImGui.GetTextLineHeight();
        var glyph = UiMetrics.InlineGlyphSize(lineHeight);
        var indent = glyph + ImGui.GetStyle().ItemSpacing.X;

        ImGui.TextDisabled(Strings.ItemsHintTitle);
        foreach (var line in lines)
        {
            MoonGlyph.DrawInline(line.State, glyph);
            ImGui.SameLine();
            ImGui.TextUnformatted(line.QuestText);
            ImGui.SameLine();
            using (Theme.PushText(line.Done ? Theme.Moon : Theme.Dusk))
            {
                ImGui.TextUnformatted(line.StatusText);
            }

            if (!line.HasObtained)
            {
                continue;
            }

            ImGui.Indent(indent);
            MoonGlyph.DrawInline(line.ObtainedGlyph, glyph);
            ImGui.SameLine();
            using (Theme.PushText(line.ObtainedColor))
            {
                ImGui.TextUnformatted(line.ObtainedText);
            }

            ImGui.Unindent(indent);
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
    private sealed class Line(uint questRowId, QuestState state, string questText, string statusText, bool done)
    {
        public uint QuestRowId { get; } = questRowId;
        public QuestState State { get; } = state;
        public string QuestText { get; } = questText;
        public string StatusText { get; } = statusText;
        public bool Done { get; } = done;

        public bool HasObtained { get; private set; }
        public QuestState ObtainedGlyph { get; private set; } = QuestState.Unknown;
        public string ObtainedText { get; private set; } = string.Empty;
        public Vector4 ObtainedColor { get; private set; } = Theme.Dusk;

        public void SetObtained(bool? obtained)
        {
            HasObtained = true;
            (ObtainedGlyph, ObtainedText, ObtainedColor) = obtained switch
            {
                true => (QuestState.Completed, Strings.ItemsOwned, Theme.Moon),
                false => (QuestState.Blocked, Strings.ItemsNotOwned, Theme.Dusk),
                null => (QuestState.Unknown, Strings.ItemsVeiled, Theme.Dusk),
            };
        }
    }
}
