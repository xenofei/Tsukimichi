using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// Do first (feature plan v7 P4, spec-1.21 "Do first"): one card per tier in tier order, each with its heading and "N
/// left", a one-line why, its first rows and "N more ›"; High-end and Another job or society fold to one line each
/// until opened. Rows are two lines in a fixed height (44 px at 100 %, scaled with the text): the moon, the name and the
/// kind chips, then "Ready · Mor Dhona · Good Intentions needs it", with a reserved action slot (Teleport and "…")
/// that shows on hover or keyboard focus, so a hover never moves or covers the text.
/// </summary>
public sealed partial class PlanPane
{
    /// <summary>The reserved action slot of a two-line row, logical px.</summary>
    private const float DoFirstSlotLogical = 104f;

    /// <summary>The row height at 100 %, logical px.</summary>
    private const float DoFirstRowLogical = 44f;

    /// <summary>Rows a tier card lists before "N more ›".</summary>
    private const int DoFirstRows = 5;

    private const string TierRowMenuId = "##tierRowMenu";

    // Tiers the player opened (High-end and Another job start folded) and cards showing every row.
    private readonly HashSet<UnlockTier> openedTiers = [];
    private readonly HashSet<UnlockTier> expandedTiers = [];

    // Line 2 of each row, built when first drawn for the current view.
    private readonly Dictionary<uint, string> tierLines = [];

    // The row whose slot holds keyboard focus (its buttons stay shown while focus is in them).
    private uint focusedTierRow;

    private static bool FoldsByDefault(UnlockTier tier) => tier is UnlockTier.HighEnd or UnlockTier.AnotherJob;

    private void DrawDoFirst(UiState ui)
    {
        var groups = view.TierGroups;
        var folded = new List<PlanTierGroup>(2);
        foreach (var group in groups)
        {
            if (FoldsByDefault(group.Tier) && !openedTiers.Contains(group.Tier))
            {
                folded.Add(group);
                continue;
            }

            DrawTierCard(ui, group);
            ImGui.Spacing();
        }

        // The folded tiers, one line each: "High-end · 3 left", a click opens the card.
        foreach (var group in folded)
        {
            var count = showSetAside ? group.Entries.Count - group.Count : group.Count;
            var label = string.Format(CultureInfo.CurrentCulture, Strings.BluesTierFoldedFormat, UnlockTiers.Name(group.Tier), count);
            Chrome.SameLineOrWrap(ImGui.CalcTextSize(label).X + UiMetrics.Px(16f));
            using (ImRaii.PushId((int)group.Tier))
            {
                if (ImGui.Selectable(label, false, ImGuiSelectableFlags.None, new Vector2(ImGui.CalcTextSize(label).X + UiMetrics.Px(8f), 0f)))
                {
                    openedTiers.Add(group.Tier);
                }
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.BluesTierFoldedTooltip, Strings.BluesTierTooltip);
            }
        }
    }

    private void DrawTierCard(UiState ui, PlanTierGroup group)
    {
        Chrome.BeginCard(1000 + (int)group.Tier);
        var name = UnlockTiers.Name(group.Tier);
        var count = showSetAside ? group.Entries.Count - group.Count : group.Count;
        var caption = string.Format(CultureInfo.CurrentCulture, Strings.BluesTierLeftFormat, count);
        var rows = GroupRows(group.Entries);
        var menu = !showSetAside && rows.Length >= 2 && SetAside is { CanSetAside: true };
        var moreSize = MathF.Round(ImGui.GetTextLineHeight() + UiMetrics.Px(2f));
        var headingStart = ImGui.GetCursorScreenPos();
        SectionHeading.Draw(name, caption, menu ? moreSize + UiMetrics.Px(8f) : 0f);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(name, Strings.BluesTierTooltip);
        }

        if (menu)
        {
            var after = ImGui.GetCursorScreenPos();
            var right = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X - UiMetrics.Px(10f);
            DrawGroupMenu(new Vector2(right - moreSize, headingStart.Y), moreSize, rows, name);
            ImGui.SetCursorScreenPos(after);
        }

        // The tier folds back from its heading's line when it was opened from the folded list.
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            Chrome.FitText(TierWhy(group.Tier), Theme.U32(Theme.Surface.TextSecondary));
        }

        var expanded = expandedTiers.Contains(group.Tier);
        var shown = expanded ? group.Entries.Count : Math.Min(DoFirstRows, group.Entries.Count);
        for (var i = 0; i < shown; i++)
        {
            DrawTierRow(ui, group.Entries[i]);
        }

        var hidden = group.Entries.Count - shown;
        if (hidden > 0 || expanded && group.Entries.Count > DoFirstRows || FoldsByDefault(group.Tier))
        {
            ImGui.Spacing();
            if (hidden > 0)
            {
                if (TextLink("##more", string.Format(CultureInfo.CurrentCulture, Strings.BluesMoreFormat, hidden), null))
                {
                    expandedTiers.Add(group.Tier);
                }
            }
            else if (expanded && group.Entries.Count > DoFirstRows)
            {
                if (TextLink("##fewer", Strings.BluesFewer, null))
                {
                    expandedTiers.Remove(group.Tier);
                }
            }

            if (FoldsByDefault(group.Tier))
            {
                if (hidden > 0 || expanded && group.Entries.Count > DoFirstRows)
                {
                    ImGui.SameLine(0f, UiMetrics.Px(16f));
                }

                if (TextLink("##fold", Strings.PlanCardToggleTooltip, null))
                {
                    openedTiers.Remove(group.Tier);
                }
            }
        }

        Chrome.EndCard();
    }

    private static string TierWhy(UnlockTier tier) => tier switch
    {
        UnlockTier.StoryNeedsIt => Strings.BluesTierWhyStory,
        UnlockTier.OpensContent => Strings.BluesTierWhyContent,
        UnlockTier.Systems => Strings.BluesTierWhySystems,
        UnlockTier.HighEnd => Strings.BluesTierWhyHighEnd,
        _ => Strings.BluesTierWhyAnotherJob,
    };

    /// <summary>The row's height: 44 px at 100 %, never less than its two lines and their padding.</summary>
    private static float TierRowHeight()
    {
        var line = ImGui.GetTextLineHeight();
        var firstLine = MathF.Max(ImGui.GetFrameHeight(), UiMetrics.InlineGlyphSize(line)) + UiMetrics.Px(2f);
        return MathF.Max(UiMetrics.Px(DoFirstRowLogical), firstLine + line + UiMetrics.Px(6f));
    }

    /// <summary>One two-line row of a tier card (see the class summary).</summary>
    private void DrawTierRow(UiState ui, PlanEntry entry)
    {
        var quest = entry.Quest;
        var line = ImGui.GetTextLineHeight();
        var glyph = UiMetrics.InlineGlyphSize(line);
        var height = TierRowHeight();
        var firstLine = MathF.Max(ImGui.GetFrameHeight(), glyph) + UiMetrics.Px(2f);
        var start = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X - UiMetrics.Px(10f) - start.X);
        var size = new Vector2(width, height);
        if (!ImGui.IsRectVisible(size))
        {
            ImGui.Dummy(size);
            return;
        }

        using var id = ImRaii.PushId((int)quest.RowId);
        if (showSetAside || entry.IsSetAside)
        {
            DrawQuietRow(ui, entry, start, width, height, firstLine);
            return;
        }

        var gap = UiMetrics.Px(8f);
        var slot = UiMetrics.Px(DoFirstSlotLogical);
        var textRight = start.X + width - slot - gap;
        var nameX = start.X + glyph + gap;
        var textY = start.Y + ((firstLine - line) * 0.5f);
        var dl = ImGui.GetWindowDrawList();
        var hoveredRow = ImGui.IsMouseHoveringRect(start, start + size) && ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        if (hoveredRow)
        {
            dl.AddRectFilled(start, start + size, Theme.WithAlpha(Theme.Surface.Hover, 0.6f), UiMetrics.Px(4f));
        }

        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + ((firstLine - glyph) * 0.5f)));
        MoonGlyph.DrawInline(entry.State, glyph);
        if (ImGui.IsItemHovered())
        {
            session.States.TryGetValue(quest.RowId, out var evaluation);
            UiMetrics.StateTooltip(entry.State, evaluation, quest, session.Names, session.States);
        }

        // Line 1: the name, then the kind pills and the clear badges while they fit (RowFit drops them first).
        Span<UnlockKind> kinds = stackalloc UnlockKind[MaxKinds];
        Span<float> parts = stackalloc float[MaxKinds + 1];
        Span<bool> shown = stackalloc bool[MaxKinds + 1];
        var kindCount = Kinds(entry, MaxKinds, kinds);
        var badges = Badges?.ForQuest(quest, Core.Companions.DutyBadgeSurface.MyBlues);
        var partCount = kindCount;
        using (Typography.Caption())
        {
            for (var i = 0; i < kindCount; i++)
            {
                parts[i] = PillSize(kinds[i], Strings.PlanKindName(kinds[i])).X + (i == 0 ? gap : UiMetrics.Px(4f));
            }
        }

        if (badges is { Length: > 0 })
        {
            parts[partCount++] = DutyBadges.RunWidth(badges) + (kindCount == 0 ? gap : DutyBadges.RunGap);
        }

        var nameWidth = ImGui.CalcTextSize(entry.Name).X;
        var fit = RowFit.Fit(textRight - nameX, nameWidth, UiMetrics.Px(LayoutBudgets.RowNameMinLogical), parts[..partCount], shown);
        var nameRoom = MathF.Min(nameWidth, fit.NameRoom);
        ImGui.SetCursorScreenPos(new Vector2(nameX, textY));
        if (Chrome.EllipsisSelectable(entry.Name, ui.SelectedRowId == quest.RowId, MathF.Max(1f, nameRoom), out _))
        {
            ui.SelectedRowId = quest.RowId;
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(entry.Name, Strings.PlanRowClickHint);
        }

        using (var context = ImRaii.ContextPopupItem(RowContextId))
        {
            if (context)
            {
                UiMetrics.ApplyFontScale();
                TierRowMenuItems(ui, entry);
            }
        }

        var pillX = nameX + nameRoom;
        var pillEnd = nameX + nameRoom + fit.PartsWidth;
        var pillsEnd = DrawPills(dl, kinds[..kindCount], parts, pillX, pillEnd, start.Y, firstLine);
        var kindsWidth = 0f;
        for (var i = 0; i < kindCount; i++)
        {
            kindsWidth += parts[i];
        }

        if (badges is { Length: > 0 } && pillsEnd >= pillX + kindsWidth - 0.5f)
        {
            DutyBadges.DrawRun(badges, pillsEnd + (kindCount == 0 ? gap : DutyBadges.RunGap), start.Y, firstLine, pillEnd, Textures);
        }

        // Line 2: the state word, the zone and why it is in this tier.
        var status = TierLine(entry);
        if (status.Length > 0)
        {
            var s = Theme.Surface;
            var live = session.ViewedSnapshot is not null;
            ImGui.SetCursorScreenPos(new Vector2(nameX, start.Y + firstLine + UiMetrics.Px(1f)));
            Chrome.StatusText(status, MathF.Max(1f, textRight - nameX), live ? s.Text : s.TextSecondary, s.TextSecondary);
        }

        // The reserved slot: Teleport and "…", shown on hover or while one of them holds keyboard focus.
        var showSlot = hoveredRow || focusedTierRow == quest.RowId;
        var moreSize = MathF.Min(UiMetrics.MinTarget, height);
        var slotY = start.Y + ((height - ImGui.GetFrameHeight()) * 0.5f);
        var focused = false;
        using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, showSlot ? ImGui.GetStyle().Alpha : 0f))
        {
            if (links.TeleportShown)
            {
                var teleportWidth = Chrome.ActionPillWidth(ActionIcons.TeleportIcon, Strings.PlanTeleport, PillLayout.Row);
                ImGui.SetCursorScreenPos(new Vector2(start.X + width - moreSize - UiMetrics.Px(6f) - teleportWidth, slotY));
                TravelControls.TeleportButton(links, quest, Strings.PlanTeleport);
                focused |= ImGui.IsItemFocused();
            }

            Keyboard.MoreButton("##more", TierRowMenuId, new Vector2(start.X + width - moreSize, start.Y + ((height - moreSize) * 0.5f)), moreSize);
            focused |= ImGui.IsItemFocused();
        }

        if (focused)
        {
            focusedTierRow = quest.RowId;
        }
        else if (focusedTierRow == quest.RowId && !hoveredRow)
        {
            focusedTierRow = 0;
        }

        using (var popup = ImRaii.Popup(TierRowMenuId))
        {
            if (popup)
            {
                UiMetrics.ApplyFontScale();
                TierRowMenuItems(ui, entry);
            }
        }

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(size);
    }

    /// <summary>The row's menu (right-click and "…"): Flag, travel, Show in the Journal, Route to this, Open on, then Set aside.</summary>
    private void TierRowMenuItems(UiState ui, PlanEntry entry)
    {
        var quest = entry.Quest;
        if (ImGui.MenuItem(Strings.PlanFlag, enabled: links.CanFlagMap(quest)))
        {
            links.FlagMap(quest);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.PlanFlagTooltip);
        }

        TravelControls.MenuItems(links, quest, Strings.PlanTeleport);
        if (ImGui.MenuItem(Strings.PlanReveal))
        {
            ui.Reveal(quest);
        }

        if (ImGui.MenuItem(Strings.PlanRouteToThis))
        {
            OpenRouteTo(ui, entry);
        }

        links.DrawOpenOnMenu(quest, session.Spoilers.IsMasked(quest), entry.Name);
        SetAsideMenuItems(entry);
    }

    /// <summary>
    /// Line 2 of a Do first row, memoized for the view: the status ("Ready", or the state and its blocker), the zone,
    /// and why: "Good Intentions needs it" for the story's quests, else what the quest opens.
    /// </summary>
    private string TierLine(PlanEntry entry)
    {
        if (tierLines.TryGetValue(entry.Quest.RowId, out var text))
        {
            return text;
        }

        var parts = new List<string>(3);
        if (entry.StatusText.Length > 0)
        {
            parts.Add(entry.StatusText);
        }

        if (entry.Quest.Issuer is { MapId: > 0 } issuer)
        {
            var zone = ZoneName(new PlanZone(issuer.TerritoryId, issuer.MapId, []));
            if (zone.Length > 0)
            {
                parts.Add(zone);
            }
        }

        if (entry.Tier == UnlockTier.StoryNeedsIt && source.StoryQuestNeeding(entry.Quest.RowId) is { } story)
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.BluesNeedsItFormat, session.Names.QuestName(story)));
        }
        else if (entry.Unlocks.Count > 0 && entry.Unlocks[0] is { Name.Length: > 0, Inherited: false } unlock)
        {
            parts.Add(unlock.Name);
        }

        text = string.Join(BlockerText.Separator, parts);
        tierLines[entry.Quest.RowId] = text;
        return text;
    }
}
