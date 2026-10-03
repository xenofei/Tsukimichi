using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail pane at narrow widths (feature plan v4 L5) and the requirements the character does not meet (L8).
/// <para>
/// <b>Narrow.</b> The pane's own width picks a <see cref="DetailTier"/> once per frame. Every text wraps between words
/// (<see cref="TextFlow"/>, measured once per text and width) at an explicit right edge (the body's, or a card's inner
/// edge), never inside a word; a caption line or a journal path is a flow of whole segments
/// (<see cref="SegmentFlow"/>); a button that no longer fits beside its text moves to the next line.
/// </para>
/// <para>
/// <b>Unmet.</b> A quest the character cannot take on the current job opens with the "Not yet" callout
/// (<see cref="NotYetText.Callout"/>) under the header, tinted with the Blocked or Locked out tone. Each unmet
/// requirement line has an eclipse cross (<see cref="Mark.Unmet"/>), its value in the unmet tone, a gap meter for a
/// numeric requirement ("52 → 56") and a jump button to the quest that clears it; met lines go quiet (a small check,
/// the secondary tone). Everything is composed when the selection or the session changes, never per frame.
/// </para>
/// </summary>
public sealed partial class DetailPane
{
    private static readonly string JumpIcon = FontAwesomeIcon.ArrowRight.ToIconString();

    /// <summary>A requirement line's game icon, logical px (spec-1.15 B4).</summary>
    private const float RequirementIconLogical = 18f;

    /// <summary>Between the journal path's genre and category (<see cref="Strings.JournalPathFormat"/>'s own separator).</summary>
    private const string JournalSeparator = " › ";

    // This frame's tier and right edges: the body's content edge, and a card's inner edge (Chrome's 10 px padding).
    private DetailTier tier = DetailTier.Full;
    private float bodyRight;
    private float cardRight;

    /// <summary>The room from the cursor to <paramref name="right"/>, at least one pixel.</summary>
    private static float RoomTo(float right) => MathF.Max(1f, right - ImGui.GetCursorScreenPos().X);

    /// <summary>A <c>SmallButton</c>'s width for <paramref name="label"/>.</summary>
    private static float SmallButtonWidth(string label) => ImGui.CalcTextSize(label).X + (ImGui.GetStyle().FramePadding.X * 2f);

    /// <summary>
    /// Continues the line when an item <paramref name="width"/> wide still fits before <paramref name="right"/> (a
    /// card's inner edge, not the window's), else starts a new one; after a text that wrapped onto several lines the
    /// item goes under it (<see cref="Chrome.SameLineOrWrap(float, float)"/>).
    /// </summary>
    private static void SameLineOrWrap(float width, float right) => Chrome.SameLineOrWrap(width, right);

    /// <summary>
    /// <paramref name="segments"/> as a flow in <paramref name="room"/> pixels: whole segments with
    /// <paramref name="separator"/> between them; a segment that does not fit the rest of a line starts the next one
    /// (the separator is dropped at the break), and one wider than a whole line wraps between its own words. One item
    /// spanning the flow. Measures with <c>CalcTextSize</c> only; nothing is allocated.
    /// </summary>
    private static void SegmentFlow(string[] segments, string separator, float room, Vector4 color)
    {
        if (segments.Length == 0)
        {
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var ink = Theme.U32(color);
        var origin = ImGui.GetCursorScreenPos();
        var line = ImGui.GetTextLineHeight();
        var separatorWidth = ImGui.CalcTextSize(separator).X;
        room = MathF.Max(1f, room);
        var x = 0f;
        var y = 0f;
        var widest = 0f;
        foreach (var segment in segments)
        {
            var width = ImGui.CalcTextSize(segment).X;
            if (x > 0f)
            {
                if (x + separatorWidth + width <= room)
                {
                    dl.AddText(origin + new Vector2(x, y), ink, separator);
                    x += separatorWidth;
                }
                else
                {
                    y += line;
                    x = 0f;
                }
            }

            if (x + width <= room)
            {
                dl.AddText(origin + new Vector2(x, y), ink, segment);
                x += width;
                widest = MathF.Max(widest, x);
                continue;
            }

            // Wider than a whole line: its own lines, wrapped between its words; the next segment starts a new line.
            ImGui.SetCursorScreenPos(origin + new Vector2(0f, y));
            var height = TextFlow.Height(segment, room);
            TextFlow.Wrapped(segment, room, ink);
            y += height - line;
            x = room;
            widest = room;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(widest, y + line));
    }

    // ------------------------------------------------------------------ "Not yet" callout (L8)

    /// <summary>
    /// The "Not yet" callout under the header for a quest the character cannot take on the current job: an eclipse
    /// cross, the line naming everything missing ("Not yet · level 56 (you're 52) and 1 previous quest"), and for a
    /// quest on another path who it is for. A Blocked or Ready-on-another-job quest is tinted with the Blocked tone, a
    /// Locked out one with the eclipse; the rule on its left edge is the eclipse either way. Hover gives the state.
    /// </summary>
    private void DrawNotYet(QuestRecord quest)
    {
        if (model.Callout is not { } callout)
        {
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, bodyRight - start.X);
        var padX = UiMetrics.Px(10f);
        var padY = UiMetrics.Px(7f);
        var rule = MathF.Max(2f, UiMetrics.Px(3f));
        var line = ImGui.GetTextLineHeight();
        var mark = UiMetrics.RequirementMarkSize;
        var box = MathF.Max(line, mark);
        var markLeft = start.X + rule + padX;
        var textLeft = markLeft + box + UiMetrics.Px(6f);
        var textRoom = MathF.Max(1f, start.X + width - padX - textLeft);
        var tone = callout.LockedOut ? Theme.Eclipse : Theme.StateColor(QuestState.Blocked);
        var ink = callout.LockedOut ? Theme.EclipseText : Theme.Surface.Text;

        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);
        Marks.Draw(dl, new Vector2(markLeft + (box * 0.5f), start.Y + padY + (line * 0.5f)), mark, Mark.Unmet);
        ImGui.SetCursorScreenPos(new Vector2(textLeft, start.Y + padY));
        TextFlow.Wrapped(callout.Text, textRoom, Theme.U32(ink));
        var bottom = ImGui.GetItemRectMax().Y;
        if (model.CalloutDetail is { } detail)
        {
            ImGui.SetCursorScreenPos(new Vector2(textLeft, bottom + UiMetrics.Px(2f)));
            TextFlow.Wrapped(detail, textRoom, Theme.U32(Theme.Surface.TextSecondary));
            bottom = ImGui.GetItemRectMax().Y;
        }

        var max = new Vector2(start.X + width, bottom + padY);
        dl.ChannelsSetCurrent(0);
        var rounding = UiMetrics.Px(6f);
        dl.AddRectFilled(start, max, Theme.WithAlpha(tone, 0.16f), rounding);
        dl.AddRect(start, max, Theme.WithAlpha(tone, 0.5f), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        dl.AddRectFilled(start, new Vector2(start.X + rule, max.Y), Theme.EclipseU32, rounding, ImDrawFlags.RoundCornersLeft);
        dl.ChannelsMerge();

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(width, max.Y - start.Y));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.StateTooltip(model.State, model.Evaluation, quest, BlockerNamesOf(), lastStates);
        }
    }

    // ------------------------------------------------------------------ requirement lines (L5, L8)

    /// <summary>
    /// A requirement line as the pane shows it: an unmet one gains its gap meter and the quest that clears it, with
    /// that quest's name (through the spoiler shield) in the jump button's tooltip.
    /// </summary>
    private RequirementLine UnmetLine(SessionState session, CatalogBundle bundle, QuestRecord quest, RequirementResult result, bool isNext, string detail)
    {
        var label = Strings.RequirementName(result.Req.Kind);
        if (result.Met)
        {
            return new RequirementLine(true, isNext, label, detail);
        }

        var gap = NotYetText.Gap(result.Req);
        var gapText = gap is null ? null : NotYetText.GapLabel(result.Req, session.Names);
        var jump = NotYetText.JumpTarget(result, quest, bundle.Catalog, session.States, job => JobUnlockQuest(bundle, job)) ?? 0u;
        var tooltip = jump == 0
            ? null
            : string.Format(CultureInfo.CurrentCulture, Strings.DetailJumpTooltipFormat, session.Spoilers.DisplayName(bundle.Catalog, jump, jump.ToString(CultureInfo.InvariantCulture)));
        return new RequirementLine(false, isNext, label, detail, gap?.Fraction ?? 0f, gapText, jump, tooltip);
    }

    /// <summary>The sheet icons of the requirement lines (UI-5e); null until the plugin attaches them, or while they are read.</summary>
    public Func<IPaneIconSheets?>? IconSheets { get; set; }

    // Every instance's icon by InstanceContent id, from the catalog's Instance rewards (UI-5b's chain), built once per catalog.
    private QuestCatalog? instanceIconsCatalog;
    private Dictionary<uint, uint> instanceIcons = [];

    /// <summary>The job a Level line speaks of: the Class or job line's pinned job, else the job the check was made for; 0 when none.</summary>
    private static uint LevelJobOf(IEnumerable<RequirementResult> results)
    {
        foreach (var result in results)
        {
            if (result.Req is ClassJobRequirement job)
            {
                return job.RequiredJob != 0 ? job.RequiredJob : job.Job;
            }
        }

        return 0;
    }

    /// <summary>A requirement line's icon (<see cref="ActionIcons.Requirement"/>); none while the sheet icons are read.</summary>
    private GameIconRef RequirementIcon(CatalogBundle bundle, Requirement requirement, uint levelJob, IPaneIconSheets? sheets)
    {
        if (sheets is null)
        {
            return GameIconRef.None;
        }

        var catalog = bundle.Catalog;
        if (!ReferenceEquals(instanceIconsCatalog, catalog))
        {
            instanceIconsCatalog = catalog;
            instanceIcons = [];
            foreach (var quest in catalog.All)
            {
                foreach (var reward in quest.Rewards)
                {
                    if (reward.Kind == RewardKind.Instance && reward.Icon != 0)
                    {
                        instanceIcons.TryAdd(reward.Id, reward.Icon);
                    }
                }
            }
        }

        return ActionIcons.Requirement(
            requirement,
            levelJob,
            id => catalog.GetByRowId(id)?.EventIconType,
            id => instanceIcons.GetValueOrDefault(id),
            sheets);
    }

    /// <summary>A job's unlock quest from the ClassJob sheet; 0 for none.</summary>
    private static uint JobUnlockQuest(CatalogBundle bundle, uint job)
    {
        foreach (var info in bundle.Names.ClassJobInfos)
        {
            if (info.RowId == job)
            {
                return info.UnlockQuestRowId;
            }
        }

        return 0;
    }

    /// <summary>
    /// Every requirement with its mark: met lines quiet (a small check, the secondary tone), unmet ones with an eclipse
    /// cross, the value in the unmet tone, a gap meter and a jump button. The one blocking the quest keeps its 2 px gold
    /// bar at the card's left edge and its label in gold (one marker, game UX panel finding 8). Label beside value while
    /// the value keeps ten ems of room and the pane is at least 320 wide; else the label over the value (L5).
    /// </summary>
    private void DrawRequirementLines(float cardLeft)
    {
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        var lineHeight = ImGui.GetTextLineHeight();
        var mark = UiMetrics.RequirementMarkSize;
        var box = MathF.Max(lineHeight, mark);
        var gapX = ImGui.GetStyle().ItemSpacing.X;
        var left = ImGui.GetCursorScreenPos().X;
        var right = cardRight;
        var jump = UiMetrics.MinTarget;
        var labelWidth = 0f;
        var anyJump = false;
        var anyIcon = false;
        foreach (var line in model.Requirements)
        {
            labelWidth = MathF.Max(labelWidth, ImGui.CalcTextSize(line.Label).X);
            anyJump |= line.JumpRowId != 0;
            anyIcon |= line.Icon.HasIcon;
        }

        // The game icon after the mark (UI-5e): one column for every line once any has one, so the labels stay aligned.
        var iconSide = anyIcon ? MathF.Round(UiMetrics.Px(RequirementIconLogical)) : 0f;
        var iconLeft = left + box + gapX;
        var textLeft = iconLeft + (anyIcon ? iconSide + gapX : 0f);
        var textRoom = MathF.Max(1f, right - textLeft);

        var jumpReserve = anyJump ? jump + gapX : 0f;
        var stacked = DetailTiers.Stacks(tier) || LayoutBudgets.StackLabelValue(textRoom - jumpReserve, labelWidth, gapX, ImGui.GetFontSize());
        for (var i = 0; i < model.Requirements.Count; i++)
        {
            var line = model.Requirements[i];
            using var id = ImRaii.PushId(i);
            var top = ImGui.GetCursorScreenPos().Y;
            var hasJump = line.JumpRowId != 0;

            // With a jump button the first line is the button's height, its text centred on it.
            var firstLine = MathF.Max(hasJump ? MathF.Max(lineHeight, jump) : lineHeight, iconSide);
            var textY = top + ((firstLine - lineHeight) * 0.5f);

            // Met is a small check, unmet an eclipse cross: a moon means a quest state or a fraction only (accessibility B2).
            ImGui.SetCursorScreenPos(new Vector2(left, textY));
            ImGui.Dummy(new Vector2(box, lineHeight));
            Marks.Draw(dl, new Vector2(left + (box * 0.5f), textY + (lineHeight * 0.5f)), line.Met ? mark * 0.85f : mark, line.Met ? Mark.Check : Mark.Unmet);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(line.Met ? Strings.MetTooltip : Strings.UnmetTooltip);
            }

            if (line.Icon.HasIcon)
            {
                var iconMin = new Vector2(iconLeft, MathF.Round(top + ((firstLine - iconSide) * 0.5f)));
                Chrome.DrawPillIcon(dl, line.Icon, iconMin, iconSide, Theme.U32(s.TextSecondary), enabled: true);
            }

            var labelInk = line.Met ? s.TextSecondary : line.IsNext ? Theme.Moon : s.Text;
            var labelRoom = stacked ? textRoom - (hasJump ? jump + gapX : 0f) : labelWidth;
            ImGui.SetCursorScreenPos(new Vector2(textLeft, textY));
            if (Chrome.EllipsisText(line.Label, labelRoom, Theme.U32(labelInk)) && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(line.Label);
            }

            var valueLeft = stacked ? textLeft : textLeft + labelWidth + gapX;
            var valueTop = stacked ? top + firstLine : textY;
            var valueRoom = MathF.Max(1f, right - valueLeft - (!stacked && hasJump ? jump + gapX : 0f));
            var bottom = top + firstLine;
            if (line.Detail.Length > 0)
            {
                ImGui.SetCursorScreenPos(new Vector2(valueLeft, valueTop));
                TextFlow.Wrapped(line.Detail, valueRoom, Theme.U32(line.Met ? s.TextSecondary : Theme.EclipseText));
                bottom = MathF.Max(bottom, ImGui.GetItemRectMax().Y);
            }

            if (!line.Met && line.GapText is { } gapText)
            {
                bottom = DrawGapMeter(dl, new Vector2(valueLeft, bottom + UiMetrics.Px(3f)), valueRoom, line.GapFraction, gapText);
            }

            if (hasJump)
            {
                ImGui.SetCursorScreenPos(new Vector2(right - jump, top));
                if (Chrome.IconButtonRound("##jump", JumpIcon, line.JumpTooltip))
                {
                    RevealRow(line.JumpRowId);
                }
            }

            if (line.IsNext)
            {
                var x = cardLeft + UiMetrics.Px(1f);
                dl.AddRectFilled(new Vector2(x, top - UiMetrics.Px(1f)), new Vector2(x + MathF.Max(2f, UiMetrics.Px(2f)), bottom + UiMetrics.Px(1f)), Theme.MoonU32);
            }

            // The row as one item, so the next row starts under all of it.
            ImGui.SetCursorScreenPos(new Vector2(left, top));
            ImGui.Dummy(new Vector2(MathF.Max(1f, right - left), bottom - top));
        }
    }

    /// <summary>
    /// A gap meter at <paramref name="pos"/>: a small bar filled to where the character stands, in the eclipse, and
    /// "52 → 56" in the unmet tone beside it; under the numbers when <paramref name="room"/> is too narrow for both.
    /// Returns the meter's bottom edge.
    /// </summary>
    private static float DrawGapMeter(ImDrawListPtr dl, Vector2 pos, float room, float fraction, string label)
    {
        using var caption = Typography.Caption();
        var labelSize = ImGui.CalcTextSize(label);
        var gap = UiMetrics.Px(6f);
        var barHeight = MathF.Max(3f, UiMetrics.Px(4f));
        var barMax = UiMetrics.Px(110f);
        var beside = MathF.Min(barMax, room - labelSize.X - gap);
        if (beside >= UiMetrics.Px(32f))
        {
            var rowHeight = MathF.Max(labelSize.Y, barHeight);
            GapBar(dl, new Vector2(pos.X, pos.Y + ((rowHeight - barHeight) * 0.5f)), beside, barHeight, fraction);
            dl.AddText(new Vector2(pos.X + beside + gap, pos.Y + ((rowHeight - labelSize.Y) * 0.5f)), Theme.EclipseTextU32, label);
            return pos.Y + rowHeight;
        }

        Chrome.EllipsisTextAt(dl, pos, room, label, Theme.EclipseTextU32, labelSize.X);
        var barTop = pos.Y + labelSize.Y + UiMetrics.Px(2f);
        GapBar(dl, new Vector2(pos.X, barTop), MathF.Max(1f, MathF.Min(room, barMax)), barHeight, fraction);
        return barTop + barHeight;
    }

    private static void GapBar(ImDrawListPtr dl, Vector2 min, float width, float height, float fraction)
    {
        var max = min + new Vector2(width, height);
        var rounding = height * 0.5f;
        dl.AddRectFilled(min, max, Theme.U32(Theme.Surface.Sunken), rounding);
        dl.AddRect(min, max, Theme.U32(Theme.Surface.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        if (fraction > 0f)
        {
            dl.AddRectFilled(min, new Vector2(min.X + MathF.Max(height, width * Math.Clamp(fraction, 0f, 1f)), max.Y), Theme.EclipseU32, rounding);
        }
    }
}
