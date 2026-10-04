using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Journal;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Make room (feature plan v7 C9; spec-1.19 "Make room", make-room.png): a 520 px popover anchored to its trigger (the
/// status bar's Make room, the hero's "Make room to accept it"), as tall as its content up to 70% of the window and
/// scrolling after. The title ("Make room": the Eyebrow face in gold at Full, Text semibold at Quiet and Plain), the
/// subtitle ("30 of 30 slots. Finish one, or drop one in the game's journal."), then the groups of
/// <see cref="MakeRoom.Plan"/> in order (Finish now, Needs a duty, Needs an item, Needs a group, Safe to drop), each
/// with its semibold header and Tertiary sub-label, empty ones hidden. A row is 30 px: the In journal moon (16 px), the
/// name, what finishing takes in Tertiary, "safe to drop" where it applies, and a reserved fifth column (112 px) that
/// holds Open in journal on the hovered or keyboard-focused row, so nothing shifts. The footer says that Tsukimichi never
/// abandons a quest: Open in journal opens the game's Journal at the quest (<c>AgentQuestJournal.OpenForQuest</c>),
/// where the player drops it with the game's own Abandon and its confirmation. The Characters dashboard draws the same
/// body in its journal section. The rows are rebuilt when the session, the duty index or the language move, and every
/// two seconds while shown (the inventory counts). Framework thread only.
/// </summary>
public sealed class MakeRoomView
{
    /// <summary>The popover's ImGui id; each trigger opens it in its own ID scope and draws it right after.</summary>
    public const string PopupId = "##makeRoom";

    private const float WidthLogical = 520f;
    private const float RowLogical = 30f;
    private const float SlotLogical = 112f;
    private const float GlyphColumnLogical = 24f;
    private const float GapLogical = 8f;
    private const uint HoverTag = 0x4D52_4F57; // "MROW"
    private const double CountsSeconds = 2.0;

    private readonly SessionState session;
    private readonly GameLinks links;

    private (int Version, int Language, DutyRunIndex? Index, long Tick) key = (-1, -1, null, -1);
    private Group[] groups = [];
    private string subtitle = string.Empty;
    private double openedAt = double.NegativeInfinity;
    private float detailWidth;
    private float chipWidth;

    public MakeRoomView(SessionState session, GameLinks links)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
    }

    /// <summary>The duty index (which duty a journal quest is cleared in); null leaves the duty groups empty.</summary>
    public Func<DutyRunIndex?>? DutyRuns { get; set; }

    /// <summary>A quest's reward entries (the duties it unlocks, for <see cref="QuestDuties.For"/>).</summary>
    public Func<uint, IReadOnlyList<UniqueRewardEntry>>? RewardEntries { get; set; }

    /// <summary>The logged-in character's item counts; null (or a stored character on view) leaves the item group empty.</summary>
    public Func<HandInStock?>? Stock { get; set; }

    /// <summary>One group as drawn: the header, its sub-label and its rows.</summary>
    private sealed record Group(string Title, string Sub, Row[] Rows);

    /// <summary>One row as drawn: the quest, its name, what finishing takes, and the safe-to-drop chip's tooltip (null for none).</summary>
    private sealed record Row(QuestRecord Quest, string Name, string Detail, string? SafeTooltip);

    /// <summary>
    /// The trigger was clicked: opens the popover in the caller's ID scope, which then draws it with
    /// <see cref="DrawPopover"/> in the same scope.
    /// </summary>
    public void Open()
    {
        openedAt = ImGui.GetTime();
        key = (-1, -1, null, -1);
        ImGui.OpenPopup(PopupId);
    }

    /// <summary>
    /// The popover, while open, anchored at <paramref name="anchor"/>: under it with <paramref name="above"/> false (the
    /// hero's link), over it otherwise (the status bar's button). It rises 4 px into place over Rise, never under Reduce
    /// motion; the fade is <see cref="PopupFade"/>'s.
    /// </summary>
    public void DrawPopover(Vector2 anchor, bool above, float windowHeight)
    {
        if (!ImGui.IsPopupOpen(PopupId))
        {
            return;
        }

        var width = MathF.Round(UiMetrics.Px(WidthLogical));
        var rise = UiMetrics.ReduceMotion || !Motion.Enabled
            ? 0f
            : (1f - MotionMath.EaseOutCubic(Math.Clamp((float)((ImGui.GetTime() - openedAt) / MotionTokens.Rise), 0f, 1f))) * UiMetrics.Px(4f);
        ImGui.SetNextWindowPos(new Vector2(anchor.X, anchor.Y + rise), ImGuiCond.Always, new Vector2(0f, above ? 1f : 0f));
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0f), new Vector2(width, MathF.Max(UiMetrics.Px(160f), windowHeight * 0.7f)));
        using var popup = ImRaii.Popup(PopupId, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup)
        {
            return;
        }

        DrawBody(ImGui.GetContentRegionAvail().X, title: true);
    }

    /// <summary>
    /// The body in the current window, <paramref name="width"/> wide: with <paramref name="title"/> the title and the
    /// subtitle first (the popover), without them the groups and the footer alone (the Characters dashboard).
    /// </summary>
    public void DrawBody(float width, bool title)
    {
        Refresh();
        var s = Theme.Surface;
        if (title)
        {
            DrawTitle();
            if (subtitle.Length > 0)
            {
                using (Typography.Caption())
                {
                    TextFlow.Wrapped(subtitle, width, Theme.U32(s.TextSecondary));
                }
            }
        }

        if (groups.Length == 0)
        {
            ImGui.Dummy(new Vector2(1f, UiMetrics.Px(4f)));
            TextFlow.Wrapped(Strings.MakeRoomNothing, width, Theme.U32(s.TextSecondary));
        }

        for (var g = 0; g < groups.Length; g++)
        {
            using var id = ImRaii.PushId(g);
            DrawGroup(groups[g], width);
        }

        ImGui.Dummy(new Vector2(1f, UiMetrics.Px(6f)));
        var rule = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddLine(rule, rule + new Vector2(width, 0f), Theme.U32(s.Line), UiMetrics.Hairline);
        ImGui.Dummy(new Vector2(1f, UiMetrics.Px(6f)));
        using (Typography.Caption())
        {
            TextFlow.Wrapped(Strings.MakeRoomFooter, width, Theme.U32(s.TextSecondary));
        }
    }

    /// <summary>"Make room": the Eyebrow face in gold at Full, Text semibold at Quiet and Plain.</summary>
    private static void DrawTitle()
    {
        if (Theme.MoonRoadArt)
        {
            var label = SectionHeading.Label(Strings.MakeRoom);
            using (Typography.Eyebrow(label))
            using (Theme.PushText(Theme.StateText(QuestState.Ready)))
            {
                ImGui.TextUnformatted(label);
            }

            return;
        }

        Chrome.SemiboldText(Strings.MakeRoom, Theme.Surface.Text);
    }

    private void DrawGroup(Group group, float width)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        ImGui.Dummy(new Vector2(1f, UiMetrics.Px(4f)));
        var head = ImGui.GetCursorScreenPos();
        Chrome.SemiboldText(group.Title, s.Text);
        ImGui.SameLine(0f, UiMetrics.Px(GapLogical));
        using (Typography.Caption())
        {
            var sub = ImGui.GetCursorScreenPos();
            Chrome.EllipsisText(group.Sub, MathF.Max(1f, head.X + width - sub.X), Theme.U32(s.TextTertiary));
        }

        var ruleY = MathF.Round(ImGui.GetCursorScreenPos().Y + UiMetrics.Px(1f));
        dl.AddLine(new Vector2(head.X, ruleY), new Vector2(head.X + width, ruleY), Theme.U32(s.Line), UiMetrics.Hairline);
        ImGui.SetCursorScreenPos(new Vector2(head.X, ruleY + UiMetrics.Px(2f)));
        for (var i = 0; i < group.Rows.Length; i++)
        {
            using var id = ImRaii.PushId(i);
            DrawRow(group.Rows[i], width);
        }
    }

    /// <summary>
    /// One 30 px row on a fixed grid: the moon, the name (ellipsised), what finishing takes, the chip column and the
    /// reserved Open in journal slot, every column's width taken from the widest row so nothing shifts on hover.
    /// </summary>
    private void DrawRow(Row row, float width)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var height = UiMetrics.Px(RowLogical);
        var max = min + new Vector2(width, height);
        var gap = UiMetrics.Px(GapLogical);
        var slot = UiMetrics.Px(SlotLogical);
        var line = ImGui.GetTextLineHeight();
        var textY = MathF.Round(min.Y + ((height - line) * 0.5f));

        // The reserved slot is an item of its own, so the keyboard reaches each row; shown while the row is hovered or it has focus.
        var slotMin = new Vector2(max.X - slot, min.Y + ((height - UiMetrics.Px(24f)) * 0.5f));
        var slotSize = new Vector2(slot, UiMetrics.Px(24f));
        var rowHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows) && ImGui.IsMouseHoveringRect(min, max);
        var hover = Motion.Hover(Motion.Key(HoverTag, row.Quest.RowId), rowHovered);
        if (hover > 0.01f)
        {
            dl.AddRectFilled(min - new Vector2(UiMetrics.Px(4f), 0f), max + new Vector2(UiMetrics.Px(4f), 0f), Theme.WithAlpha(s.Hover, s.Hover.W * hover), UiMetrics.Px(6f));
        }

        var glyphRadius = UiMetrics.Px(8f);
        MoonGlyph.Draw(dl, new Vector2(min.X + (UiMetrics.Px(GlyphColumnLogical) * 0.5f), min.Y + (height * 0.5f)), glyphRadius, QuestState.Accepted);

        var right = max.X - slot - gap;
        var chipX = right - chipWidth;
        var detailX = chipX - (chipWidth > 0f ? gap : 0f) - detailWidth;
        var nameX = min.X + UiMetrics.Px(GlyphColumnLogical);
        ImGui.SetCursorScreenPos(new Vector2(nameX, textY));
        Chrome.EllipsisText(row.Name, MathF.Max(1f, detailX - gap - nameX), Theme.U32(s.Text));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(row.Name, row.Detail);
        }

        using (Typography.Caption())
        {
            var captionY = MathF.Round(min.Y + ((height - ImGui.GetTextLineHeight()) * 0.5f));
            var detailSize = ImGui.CalcTextSize(row.Detail).X;
            ImGui.SetCursorScreenPos(new Vector2(MathF.Max(detailX, chipX - (chipWidth > 0f ? gap : 0f) - detailSize), captionY));
            Chrome.EllipsisText(row.Detail, MathF.Max(1f, detailWidth), Theme.U32(s.TextTertiary), detailSize);
            if (row.SafeTooltip is { } safe)
            {
                var chip = ImGui.CalcTextSize(Strings.MakeRoomSafeChip);
                var chipMin = new Vector2(chipX + chipWidth - chip.X - UiMetrics.Px(14f), captionY - UiMetrics.Px(1f));
                var chipMax = new Vector2(chipX + chipWidth, captionY + chip.Y + UiMetrics.Px(1f));
                var rounding = (chipMax.Y - chipMin.Y) * 0.5f;
                dl.AddRect(chipMin, chipMax, Theme.U32(Theme.Glyphs.HighContrast ? s.StrongLine : s.Line), rounding, ImDrawFlags.None, Theme.Glyphs.HighContrast ? MathF.Max(1.5f, UiMetrics.Hairline) : UiMetrics.Hairline);
                dl.AddText(new Vector2(chipMin.X + UiMetrics.Px(7f), captionY), Theme.U32(s.Text), Strings.MakeRoomSafeChip);
                if (rowHovered && ImGui.IsMouseHoveringRect(chipMin, chipMax))
                {
                    UiMetrics.Tooltip(Strings.MakeRoomSafeChip, safe);
                }
            }
        }

        ImGui.SetCursorScreenPos(slotMin);
        var clicked = ImGui.InvisibleButton("##openJournal", slotSize);
        var focused = ImGui.IsItemFocused();
        var buttonHovered = ImGui.IsItemHovered();
        if (rowHovered || focused)
        {
            DrawOpenInJournal(dl, slotMin, slotSize, buttonHovered);
            if (buttonHovered)
            {
                UiMetrics.Tooltip(session.IsLive ? Strings.MakeRoomOpenInJournalTooltip : Strings.MakeRoomOpenInJournalStored);
            }

            if (clicked && session.IsLive)
            {
                links.OpenJournal(row.Quest);
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y));
        ImGui.Dummy(new Vector2(width, 0f));
    }

    /// <summary>Open in journal: the quiet button in its primary form (Text in a <c>--vline</c> outline), the focus ring, dimmed for a stored character.</summary>
    private void DrawOpenInJournal(ImDrawListPtr dl, Vector2 min, Vector2 size, bool hovered)
    {
        var s = Theme.Surface;
        var live = session.IsLive;
        var rounding = Theme.Flair == Flair.Plain ? UiMetrics.Px(2f) : size.Y * 0.5f;
        if (hovered && live)
        {
            dl.AddRectFilled(min, min + size, Theme.U32(s.Hover), rounding);
        }

        dl.AddRect(min, min + size, Theme.U32(live ? s.StrongLine : s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        using (Typography.Caption())
        {
            var text = ImGui.CalcTextSize(Strings.MakeRoomOpenInJournal);
            dl.AddText(min + ((size - text) * 0.5f), Theme.U32(live ? s.Text : s.TextTertiary), Strings.MakeRoomOpenInJournal);
        }

        Chrome.FocusRing(rounding);
    }

    /// <summary>Rebuilds the groups when the session, the duty index or the language moved, and every two seconds for the item counts.</summary>
    private void Refresh()
    {
        var index = DutyRuns?.Invoke();
        var stock = session.IsLive ? Stock?.Invoke() : null;
        var tick = stock is not null ? (long)(ImGui.GetTime() / CountsSeconds) : 0L;
        var next = (session.Version, Localization.Loc.Version, index, tick);
        if (next == key)
        {
            return;
        }

        key = next;
        if (session.ViewedSnapshot is not { } snapshot || session.Bundle is not { } bundle)
        {
            groups = [];
            subtitle = string.Empty;
            return;
        }

        var journal = JournalSlots.Of(snapshot, bundle.Catalog);
        subtitle = string.Format(CultureInfo.CurrentCulture, Strings.MakeRoomSubtitleFormat, journal.Used, journal.Cap);

        Func<HandInItem, int?>? held = stock is null ? null : item => stock.For(item.ItemId).UsableFor(item);
        Func<QuestRecord, DutyRunInfo?>? dutyOf = index is null ? null : quest => DutyOf(quest, index);
        var plan = MakeRoom.Plan(snapshot, bundle.Catalog, dutyOf, held);

        var built = new List<Group>(5);
        var rows = new List<Row>();
        RoomGroup? current = null;
        foreach (var entry in plan)
        {
            if (current != entry.Group)
            {
                Flush(built, current, rows);
                current = entry.Group;
            }

            rows.Add(new Row(entry.Quest, session.Spoilers.DisplayName(entry.Quest), Detail(entry), entry.SafeToDrop ? SafeTooltip(entry.Quest) : null));
        }

        Flush(built, current, rows);
        groups = [.. built];
        MeasureColumns();
    }

    /// <summary>The detail and chip columns' widths: the widest row's, at caption size, the detail capped at a third of the popover.</summary>
    private void MeasureColumns()
    {
        detailWidth = 0f;
        chipWidth = 0f;
        using (Typography.Caption())
        {
            foreach (var group in groups)
            {
                foreach (var row in group.Rows)
                {
                    detailWidth = MathF.Max(detailWidth, ImGui.CalcTextSize(row.Detail).X);
                    if (row.SafeTooltip is not null)
                    {
                        chipWidth = MathF.Ceiling(ImGui.CalcTextSize(Strings.MakeRoomSafeChip).X + UiMetrics.Px(14f));
                    }
                }
            }
        }

        detailWidth = MathF.Min(MathF.Ceiling(detailWidth), UiMetrics.Px(WidthLogical) * 0.36f);
    }

    private static void Flush(List<Group> into, RoomGroup? group, List<Row> rows)
    {
        if (group is not { } kind || rows.Count == 0)
        {
            return;
        }

        var (title, sub) = kind switch
        {
            RoomGroup.FinishNow => (Strings.MakeRoomFinishNow, Strings.MakeRoomFinishNowSub),
            RoomGroup.NeedsDuty => (Strings.MakeRoomNeedsDuty, Strings.MakeRoomNeedsDutySub),
            RoomGroup.NeedsItem => (Strings.MakeRoomNeedsItem, Strings.MakeRoomNeedsItemSub),
            RoomGroup.NeedsGroup => (Strings.MakeRoomNeedsGroup, Strings.MakeRoomNeedsGroupSub),
            _ => (Strings.MakeRoomSafeToDrop, Strings.MakeRoomSafeToDropSub),
        };
        into.Add(new Group(title, sub, [.. rows]));
        rows.Clear();
    }

    /// <summary>The duty a journal quest is cleared in: the first it unlocks (<see cref="QuestDuties.For"/>); null for none.</summary>
    private DutyRunInfo? DutyOf(QuestRecord quest, DutyRunIndex index)
    {
        foreach (var duty in QuestDuties.For(quest, index, session.Curated, RewardEntries?.Invoke(quest.RowId)))
        {
            if (duty.Relation == QuestDutyRelation.Unlocks)
            {
                return duty.Duty;
            }
        }

        return null;
    }

    /// <summary>What finishing takes, in Tertiary: "a talk", "a solo duty · The Aery", "Mythrite Ore · 0 in your inventory", "step 1 · Central Shroud".</summary>
    private string Detail(RoomEntry entry)
    {
        var culture = CultureInfo.CurrentCulture;
        switch (entry.Group)
        {
            case RoomGroup.FinishNow:
                return entry.IsDelivery ? Strings.MakeRoomDelivery : Strings.MakeRoomTalk;
            case RoomGroup.NeedsDuty when entry.Duty is { } duty:
                return string.Format(culture, duty.Players == 1 ? Strings.MakeRoomSoloDutyFormat : Strings.MakeRoomNpcDutyFormat, duty.Name);
            case RoomGroup.NeedsGroup when entry.Duty is { } group:
                return group.Players > 1
                    ? string.Format(culture, Strings.MakeRoomGroupFormat, group.Name, group.Players)
                    : string.Format(culture, Strings.MakeRoomGroupUnknownFormat, group.Name);
            case RoomGroup.NeedsItem when entry.Item is { } item:
                return string.Format(culture, Strings.MakeRoomItemFormat, item.Name, entry.Held);
            default:
                return PlaceOf(entry.Quest) is { Length: > 0 } place
                    ? string.Format(culture, Strings.MakeRoomStepOneFormat, place)
                    : Strings.MakeRoomStepOne;
        }
    }

    private string? PlaceOf(QuestRecord quest) => quest.Issuer is { } issuer ? links.Map(issuer.MapId)?.PlaceName : null;

    private static string SafeTooltip(QuestRecord quest) =>
        string.Format(CultureInfo.CurrentCulture, Strings.MakeRoomSafeChipTooltipFormat, quest.Issuer?.Name ?? string.Empty);
}
