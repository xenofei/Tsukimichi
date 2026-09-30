using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Config;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// "Clear my blues" (P3), the My blues tab: every unlock quest the viewed character has left, by expansion and zone
/// in story order (<see cref="UnlockPlan"/>). <see cref="DrawLeft"/> holds the summary, the filters (one toggle chip
/// per kind with its count, Ready only, Sprout mode) and the expansion list; <see cref="DrawMain"/> the "Copy as
/// checklist" button and one card per expansion, folded but for the one opened, with the zone groups and a row per
/// quest: state moon, name (click shows it in the detail pane), kind pills, the status line, Flag and Reveal. Each
/// card can pin its expansion's block to the todo overlay.
/// <para>
/// Sprout mode is the plan's own switch, turned on whenever the tab opens while the Journal's Sprout mode quick view
/// is on, so revealing a quest in the Journal (which clears quick views) does not widen the plan. The filtered plan,
/// the chip counts and the card strings are rebuilt only when the plan's revision or a filter changes; rows outside
/// the scrolled view draw a spacer only.
/// </para>
/// </summary>
public sealed class PlanPane
{
    private static readonly string FoldedGlyph = Chrome.Icon(FontAwesomeIcon.CaretRight);
    private static readonly string OpenGlyph = Chrome.Icon(FontAwesomeIcon.CaretDown);

    /// <summary>How long "Copied N quests" stays beside the button.</summary>
    private const double CopiedSeconds = 2.5;

    private readonly SessionState session;
    private readonly PlanSource source;
    private readonly GameLinks links;
    private readonly Configuration settings;
    private readonly Action save;

    // Filters (session only).
    private ushort kinds = UnlockKinds.AllMask;
    private bool readyOnly;
    private bool sprout;
    private int lastDrawFrame = -2;

    // Folded state per expansion (true = open) and the card the left list asked to scroll to.
    private readonly Dictionary<byte, bool> open = [];
    private byte? scrollTo;

    // The view built from the plan and the filters.
    private (int Revision, ushort Kinds, bool Ready, int MaxExpansion) viewKey = (-1, 0, false, -1);
    private UnlockPlan view = UnlockPlan.Empty;
    private readonly int[] kindCounts = new int[UnlockKinds.All.Length];
    private readonly string[] kindLabels = new string[UnlockKinds.All.Length];
    private int hiddenExpansions;
    private string summary = string.Empty;
    private string showing = string.Empty;
    private readonly Dictionary<byte, (string Header, string Ready, string Count)> cardText = [];
    private readonly Dictionary<uint, string> zoneNames = [];

    private string copied = string.Empty;
    private double copiedAt = double.NegativeInfinity;

    public PlanPane(SessionState session, PlanSource source, GameLinks links, Configuration settings, Action save)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
    }

    /// <summary>Left column: summary, filters and the expansion list.</summary>
    public void DrawLeft(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("planLeft");
        Refresh(ui);

        using (Typography.Display())
        {
            ImGui.TextUnformatted(Strings.PlanTitle);
        }

        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextWrapped(summary);
        }

        ImGui.Spacing();
        ImGui.TextDisabled(Strings.PlanShow);
        var first = true;
        if (FlowChip("##ready", Strings.PlanReadyOnly, readyOnly, Strings.PlanReadyOnlyTooltip, ref first, enabled: session.ViewedSnapshot is not null))
        {
            readyOnly = !readyOnly;
        }

        if (FlowChip("##sprout", Strings.PlanSprout, sprout, Strings.PlanSproutTooltip, ref first))
        {
            sprout = !sprout;
        }

        ImGui.Spacing();
        ImGui.TextDisabled(Strings.PlanKinds);
        first = true;
        for (var i = 0; i < UnlockKinds.All.Length; i++)
        {
            var bit = UnlockKinds.Bit(UnlockKinds.All[i]);
            var on = kinds != UnlockKinds.AllMask && (kinds & bit) != 0;
            if (FlowChip(KindChipId(i), kindLabels[i], on, Strings.PlanKindChipTooltip, ref first))
            {
                // From "all kinds" the first click narrows to that kind; clearing the last kind shows all again.
                kinds = kinds == UnlockKinds.AllMask ? bit : (ushort)(kinds ^ bit);
                if (kinds == 0)
                {
                    kinds = UnlockKinds.AllMask;
                }
            }
        }

        if (kinds != UnlockKinds.AllMask && FlowChip("##allKinds", Strings.PlanAllKinds, false, Strings.PlanAllKindsTooltip, ref first))
        {
            kinds = UnlockKinds.AllMask;
        }

        Refresh(ui);
        ImGui.Spacing();
        Chrome.Hairline();
        ImGui.TextDisabled(Strings.PlanExpansions);
        foreach (var block in view.Expansions)
        {
            var text = cardText[block.Expansion];
            var isOpen = IsOpen(block);
            if (ImGui.Selectable(block.Name + "##exp" + block.Expansion.ToString(CultureInfo.InvariantCulture), isOpen))
            {
                open[block.Expansion] = true;
                scrollTo = block.Expansion;
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.PlanExpansionClickHint);
            }

            var countWidth = ImGui.CalcTextSize(text.Count).X;
            ImGui.SameLine(MathF.Max(ImGui.GetCursorPosX(), ImGui.GetWindowContentRegionMax().X - countWidth));
            ImGui.TextDisabled(text.Count);
        }

        if (hiddenExpansions > 0)
        {
            using var dusk = Theme.PushText(Theme.Dusk);
            ImGui.TextWrapped(string.Format(CultureInfo.CurrentCulture, Strings.PlanSproutHiddenFormat, hiddenExpansions));
        }
    }

    /// <summary>Centre column: the copy button, then one card per expansion.</summary>
    public void DrawMain(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("planMain");
        Refresh(ui);

        if (session.Bundle is null)
        {
            ImGui.TextDisabled(Strings.PlanLoading);
            return;
        }

        using (ImRaii.Disabled(view.IsEmpty))
        {
            if (ImGui.Button(Strings.PlanCopy))
            {
                CopyChecklist();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.PlanCopyTooltip);
        }

        ImGui.SameLine();
        if (ImGui.GetTime() - copiedAt < CopiedSeconds)
        {
            using var gold = Theme.PushText(Theme.Accent);
            ImGui.TextUnformatted(copied);
        }
        else
        {
            ImGui.TextDisabled(showing);
        }

        if (view.IsEmpty)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(source.Plan.IsEmpty && session.ViewedSnapshot is not null ? Strings.PlanEmptyAllDone : Strings.PlanEmptyFiltered);
            return;
        }

        using var list = ImRaii.Child("##planCards", Vector2.Zero);
        if (!list)
        {
            return;
        }

        foreach (var block in view.Expansions)
        {
            if (scrollTo == block.Expansion)
            {
                ImGui.SetScrollHereY(0f);
                scrollTo = null;
            }

            DrawCard(ui, block);
            ImGui.Spacing();
        }
    }

    private void DrawCard(UiState ui, PlanExpansion block)
    {
        var text = cardText[block.Expansion];
        var isOpen = IsOpen(block);
        Chrome.BeginCard(block.Expansion);

        // Header: fold caret and title (one click target), the Ready count in gold, the pin button right-aligned.
        var pinned = settings.TodoPlanExpansion == block.Expansion;
        var pinLabel = pinned ? Strings.PlanUnpin : Strings.PlanPin;
        var right = ImGui.GetWindowContentRegionMax().X - UiMetrics.Px(10f);
        var pinWidth = ImGui.CalcTextSize(pinLabel).X + ImGui.GetStyle().FramePadding.X * 2f;
        var headerStart = ImGui.GetCursorPos();
        var titleWidth = MathF.Max(1f, right - pinWidth - UiMetrics.Px(8f) - headerStart.X);
        if (ImGui.InvisibleButton("##fold", new Vector2(titleWidth, ImGui.GetFrameHeight())))
        {
            open[block.Expansion] = !isOpen;
            isOpen = !isOpen;
        }

        var hovered = ImGui.IsItemHovered();
        Chrome.FocusRing();
        if (hovered)
        {
            UiMetrics.Tooltip(Strings.PlanCardToggleTooltip);
        }

        var min = ImGui.GetItemRectMin();
        var dl = ImGui.GetWindowDrawList();
        var textY = min.Y + (ImGui.GetFrameHeight() - ImGui.GetTextLineHeight()) * 0.5f;
        ImGui.PushFont(UiBuilder.IconFont);
        dl.AddText(new Vector2(min.X, textY), Theme.U32(Theme.Surface.TextSecondary), isOpen ? OpenGlyph : FoldedGlyph);
        ImGui.PopFont();

        var x = min.X + UiMetrics.Px(16f);
        dl.AddText(new Vector2(x, textY), Theme.U32(Theme.Surface.Text), text.Header);
        x += ImGui.CalcTextSize(text.Header).X + UiMetrics.Px(10f);
        if (block.ReadyCount > 0)
        {
            dl.AddText(new Vector2(x, textY), Theme.AccentU32, text.Ready);
        }

        ImGui.SetCursorPos(new Vector2(right - pinWidth, headerStart.Y));
        if (ImGui.Button(pinLabel))
        {
            if (pinned)
            {
                settings.TodoPlanExpansion = -1;
            }
            else
            {
                settings.TodoPlanExpansion = block.Expansion;
                settings.TodoShowPlan = true;
                settings.TodoOverlayEnabled = true;
            }

            save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(pinned ? Strings.PlanUnpinTooltip : Strings.PlanPinTooltip);
        }

        if (isOpen)
        {
            foreach (var zone in block.Zones)
            {
                ImGui.Spacing();
                using (Theme.PushText(Theme.Dusk))
                {
                    ImGui.TextUnformatted(ZoneLabel(zone));
                }

                for (var i = 0; i < zone.Entries.Count; i++)
                {
                    DrawRow(ui, zone.Entries[i]);
                }
            }
        }

        Chrome.EndCard();
    }

    private void DrawRow(UiState ui, PlanEntry entry)
    {
        var quest = entry.Quest;
        var line = ImGui.GetTextLineHeight();
        var glyph = UiMetrics.InlineGlyphSize(line);
        var height = MathF.Max(ImGui.GetFrameHeight(), glyph) + UiMetrics.Px(2f);
        var start = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X - UiMetrics.Px(10f) - start.X);
        var size = new Vector2(width, height);
        if (!ImGui.IsRectVisible(size))
        {
            ImGui.Dummy(size);
            return;
        }

        using var id = ImRaii.PushId((int)quest.RowId);
        var style = ImGui.GetStyle();
        var gap = UiMetrics.Px(8f);
        var padding = style.FramePadding.X * 2f;
        var actionsWidth = ImGui.CalcTextSize(Strings.PlanFlag).X + ImGui.CalcTextSize(Strings.PlanReveal).X + padding * 2f + style.ItemSpacing.X;
        var nameWidth = MathF.Max(UiMetrics.Px(80f), width * 0.36f);
        var pillsWidth = MathF.Max(UiMetrics.Px(60f), width * 0.22f);
        var statusX = start.X + glyph + gap + nameWidth + gap + pillsWidth + gap;
        var actionsX = start.X + width - actionsWidth;
        var textY = start.Y + (height - line) * 0.5f;
        var dl = ImGui.GetWindowDrawList();

        // State moon.
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + (height - glyph) * 0.5f));
        MoonGlyph.DrawInline(entry.State, glyph);
        if (ImGui.IsItemHovered())
        {
            session.States.TryGetValue(quest.RowId, out var evaluation);
            UiMetrics.StateTooltip(entry.State, evaluation, quest, session.Names, session.States);
        }

        // Name: selects the quest for the detail pane.
        ImGui.SetCursorScreenPos(new Vector2(start.X + glyph + gap, textY));
        if (ImGui.Selectable(entry.Name, ui.SelectedRowId == quest.RowId, ImGuiSelectableFlags.None, new Vector2(nameWidth, line)))
        {
            ui.SelectedRowId = quest.RowId;
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(entry.Name, Strings.PlanRowClickHint);
        }

        // Kind pills (painted; one tooltip lists every unlock).
        var pillX = start.X + glyph + gap + nameWidth + gap;
        var pillEnd = pillX + pillsWidth;
        var pillMin = new Vector2(pillX, start.Y);
        using (Typography.Caption())
        {
            var last = (UnlockKind?)null;
            var tone = Theme.Surface.TextSecondary;
            foreach (var unlock in entry.Unlocks)
            {
                if (unlock.Kind == last)
                {
                    continue;
                }

                last = unlock.Kind;
                var label = Strings.PlanKindName(unlock.Kind);
                var pill = ImGui.CalcTextSize(label) + new Vector2(UiMetrics.Px(7f) * 2f, UiMetrics.Px(2f) * 2f);
                if (pillX + pill.X > pillEnd)
                {
                    break;
                }

                Chrome.PillAt(dl, new Vector2(pillX, start.Y + (height - pill.Y) * 0.5f), pill, label,
                    Theme.WithAlpha(tone, 0.12f), Theme.WithAlpha(tone, 0.45f), Theme.U32(tone));
                pillX += pill.X + UiMetrics.Px(4f);
            }
        }

        if (ImGui.IsMouseHoveringRect(pillMin, new Vector2(pillEnd, start.Y + height)) && ImGui.IsWindowHovered())
        {
            UiMetrics.Tooltip(UnlocksTooltip(entry));
        }

        // Status line, clipped short of the buttons.
        var statusEnd = actionsX - gap;
        if (statusEnd > statusX && entry.StatusText.Length > 0)
        {
            dl.PushClipRect(new Vector2(statusX, start.Y), new Vector2(statusEnd, start.Y + height), true);
            dl.AddText(new Vector2(statusX, textY), Theme.DuskU32, entry.StatusText);
            dl.PopClipRect();
            if (ImGui.IsMouseHoveringRect(new Vector2(statusX, start.Y), new Vector2(statusEnd, start.Y + height)) && ImGui.IsWindowHovered())
            {
                UiMetrics.Tooltip(entry.StatusText);
            }
        }

        // Flag the giver; Reveal in the Journal.
        ImGui.SetCursorScreenPos(new Vector2(actionsX, start.Y + (height - ImGui.GetFrameHeight()) * 0.5f));
        using (ImRaii.Disabled(!links.CanFlagMap(quest)))
        {
            if (ImGui.SmallButton(Strings.PlanFlag))
            {
                links.FlagMap(quest);
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.PlanFlagTooltip);
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.PlanReveal))
        {
            ui.Reveal(quest);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.PlanRevealTooltip);
        }

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(size);
    }

    private static string UnlocksTooltip(PlanEntry entry)
    {
        var text = PlanChecklist.UnlocksText(entry);
        return text.Length > 0 ? text : Strings.PlanKindName(entry.PrimaryKind);
    }

    /// <summary>
    /// A toggle chip flowing onto the next line when the row is full: a pill painted gold-tinted when on, raised when
    /// off, with the focus ring; returns true on the click that flips it.
    /// </summary>
    private static bool FlowChip(string id, string label, bool on, string tooltip, ref bool first, bool enabled = true)
    {
        var padX = UiMetrics.Px(9f);
        var height = MathF.Max(ImGui.GetTextLineHeight() + UiMetrics.Px(6f), UiMetrics.Px(22f));
        var size = new Vector2(ImGui.CalcTextSize(label).X + padX * 2f, height);
        if (!first)
        {
            ImGui.SameLine(0f, UiMetrics.Px(4f));
            if (ImGui.GetCursorPosX() + size.X > ImGui.GetWindowContentRegionMax().X)
            {
                ImGui.NewLine();
            }
        }

        first = false;
        bool clicked;
        using (ImRaii.Disabled(!enabled))
        {
            clicked = ImGui.InvisibleButton(id, size);
        }

        var hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
        var hover = Motion.Lerp(ImGuiP.GetItemID(), hovered && enabled ? 1f : 0f);
        var s = Theme.Surface;
        uint fill, border, ink;
        if (on)
        {
            fill = Theme.WithAlpha(Theme.Accent, 0.16f + 0.08f * hover);
            border = Theme.WithAlpha(Theme.Accent, 0.6f);
            ink = Theme.U32(s.Text);
        }
        else
        {
            fill = Theme.U32(Vector4.Lerp(s.Raised, s.Hover, hover));
            border = Theme.U32(s.Line);
            ink = Theme.U32(enabled ? (hovered ? s.Text : s.TextSecondary) : s.TextDisabled);
        }

        Chrome.PillAt(ImGui.GetWindowDrawList(), ImGui.GetItemRectMin(), size, label, fill, border, ink);
        Chrome.FocusRing(height * 0.5f);
        if (hovered)
        {
            UiMetrics.Tooltip(enabled ? tooltip : tooltip + "\n" + Strings.NeedsSnapshot);
        }

        return clicked && enabled;
    }

    private static readonly string[] KindChipIds = BuildKindChipIds();

    private static string KindChipId(int index) => KindChipIds[index];

    private static string[] BuildKindChipIds()
    {
        var ids = new string[UnlockKinds.All.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            ids[i] = "##kind" + i.ToString(CultureInfo.InvariantCulture);
        }

        return ids;
    }

    /// <summary>Whether a card is unfolded: the first expansion shown opens by default, the rest start folded.</summary>
    private bool IsOpen(PlanExpansion block) =>
        open.TryGetValue(block.Expansion, out var isOpen) ? isOpen : view.Expansions.Count > 0 && view.Expansions[0].Expansion == block.Expansion;

    private string ZoneLabel(PlanZone zone)
    {
        var name = ZoneName(zone);
        return name.Length > 0 ? name : Strings.PlanUnknownZone;
    }

    /// <summary>The zone's place name from its giver's map, memoized; empty when unknown.</summary>
    private string ZoneName(PlanZone zone)
    {
        if (zone.MapId == 0)
        {
            return string.Empty;
        }

        if (!zoneNames.TryGetValue(zone.MapId, out var name))
        {
            name = links.Map(zone.MapId)?.PlaceName ?? string.Empty;
            zoneNames[zone.MapId] = name;
        }

        return name;
    }

    private void CopyChecklist()
    {
        ImGui.SetClipboardText(PlanChecklist.Write(view, ZoneName));
        copied = string.Format(CultureInfo.CurrentCulture, Strings.PlanCopiedFormat, view.Count);
        copiedAt = ImGui.GetTime();
    }

    /// <summary>Sprout sync on becoming visible, then the filtered view, the chip counts and the strings when an input moved.</summary>
    private void Refresh(UiState ui)
    {
        var frame = ImGui.GetFrameCount();
        if (frame != lastDrawFrame)
        {
            if (frame != lastDrawFrame + 1 && ui.Filters.Preset == Preset.Sprout)
            {
                sprout = true;
            }

            lastDrawFrame = frame;
        }

        if (session.ViewedSnapshot is null)
        {
            readyOnly = false;
        }

        var plan = source.Plan;
        var maxExpansion = sprout ? source.Reach : byte.MaxValue;
        var key = (source.Revision, kinds, readyOnly, (int)maxExpansion);
        if (key == viewKey)
        {
            return;
        }

        viewKey = key;
        var reachFilter = new PlanFilter(UnlockKinds.AllMask, readyOnly, sprout ? maxExpansion : null);
        var reached = plan.Filter(reachFilter);
        view = reached.Filter(reachFilter with { Kinds = kinds });

        Array.Clear(kindCounts);
        foreach (var entry in reached.Entries)
        {
            var mask = entry.KindMask;
            for (var i = 0; i < kindCounts.Length; i++)
            {
                if ((mask & UnlockKinds.Bit(UnlockKinds.All[i])) != 0)
                {
                    kindCounts[i]++;
                }
            }
        }

        for (var i = 0; i < kindLabels.Length; i++)
        {
            kindLabels[i] = string.Format(CultureInfo.CurrentCulture, Strings.PlanKindChipFormat, Strings.PlanKindName(UnlockKinds.All[i]), kindCounts[i]);
        }

        hiddenExpansions = 0;
        if (sprout)
        {
            foreach (var block in plan.Expansions)
            {
                if (block.Expansion > maxExpansion)
                {
                    hiddenExpansions++;
                }
            }
        }

        summary = session.ViewedSnapshot is null
            ? Strings.PlanSummaryBrowse
            : string.Format(CultureInfo.CurrentCulture, Strings.PlanSummaryFormat, plan.Count, plan.ReadyCount);
        showing = string.Format(CultureInfo.CurrentCulture, Strings.PlanShowingFormat, view.Count, plan.Count);

        cardText.Clear();
        foreach (var block in view.Expansions)
        {
            cardText[block.Expansion] = (
                string.Format(CultureInfo.CurrentCulture, Strings.PlanCardFormat, block.Name, block.Count),
                string.Format(CultureInfo.CurrentCulture, Strings.PlanCardReadyFormat, block.ReadyCount),
                string.Format(CultureInfo.CurrentCulture, Strings.PlanExpansionCountFormat, block.Count, block.ReadyCount));
        }
    }
}
