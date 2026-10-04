using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// My blues' 1.21.0 state and Set aside (feature plan v7 P4, spec-1.21 "Set aside"): the view switch and the sort, the
/// summary with its "N set aside ›" link, the Set aside filter, the row menus' Set aside for later / Not for me, the
/// quiet line a moved row keeps in place with Undo, and a group's "Set aside these N…" with its confirmation.
/// </summary>
public sealed partial class PlanPane
{
    private const int ClearView = 0;
    private const int StoryView = 1;
    private const int SortStory = 0;
    private const int SortDoFirst = 1;
    private const string GroupConfirmId = "##bluesGroupConfirm";
    private const string GroupMenuId = "##bluesGroupMenu";

    private static readonly LocArray ViewLabels = new(static () => [Strings.BluesViewClear, Strings.BluesViewStory]);
    private static readonly LocArray ViewTooltips = new(static () => [Strings.BluesViewClearTooltip, Strings.BluesViewStoryTooltip]);
    private static readonly LocArray SortLabels = new(static () => [Strings.BluesSortStory, Strings.BluesSortDoFirst]);
    private static readonly LocArray SortTooltips = new(static () => [Strings.BluesSortStoryTooltip, Strings.BluesSortDoFirstTooltip]);

    // Session only, like the filters.
    private int pageView = ClearView;
    private int sort = SortStory;
    private bool showSetAside;

    // Rows moved to the other side (set aside, or brought back) since the view was built, for the character on view:
    // they keep their place as a quiet line until the list is rebuilt or another character is viewed (P4).
    private readonly KeptInPlace kept = new();
    private string setAsideLink = string.Empty;

    // Per frame, a group's rows that can still be set aside, by the group's entries; built once per view.
    private readonly Dictionary<IReadOnlyList<PlanEntry>, uint[]> groupRows = new(ReferenceEqualityComparer.Instance);

    // The group "Set aside these N…" is asking about, and the character it was asked for.
    private uint[] confirmRows = [];
    private ulong? confirmOwner;
    private string confirmTitle = string.Empty;
    private string confirmBody = string.Empty;
    private string confirmButton = string.Empty;
    private bool openConfirm;

    /// <summary>The Clear my blues · Your story switch at the top of the centre column, as Characters has its own.</summary>
    private void DrawViewSwitch()
    {
        var labels = ViewLabels.Value;
        Chrome.Segmented("##bluesView", ref pageView, labels, Chrome.SegmentedWidth(labels, ImGui.GetContentRegionAvail().X), ViewTooltips.Value);
        ImGui.Spacing();
    }

    /// <summary>The list is rebuilt: rows kept in place go.</summary>
    private void ClearKeep() => kept.Clear();

    private void KeepInPlace(uint rowId) => kept.Keep(rowId);

    /// <summary>"42 left · 18 Ready", then "· 6 set aside ›", a link that turns the Set aside filter on.</summary>
    private void DrawSummary()
    {
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextWrapped(summary);
        }

        if (setAsideLink.Length == 0)
        {
            return;
        }

        Chrome.SameLineOrWrap(ImGui.CalcTextSize(setAsideLink).X);
        if (TextLink("##setAsideLink", setAsideLink, Strings.BluesSetAsideLinkTooltip, underline: true))
        {
            showSetAside = true;
            ClearKeep();
        }
    }

    /// <summary>
    /// A text link: <paramref name="label"/> in Secondary, Text on hover, underlined when <paramref name="underline"/>,
    /// with the focus ring. True on click.
    /// </summary>
    private static bool TextLink(string id, string label, string? tooltip, bool underline = false)
    {
        var size = ImGui.CalcTextSize(label);
        var min = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, new Vector2(MathF.Max(1f, size.X), MathF.Max(1f, size.Y)));
        var hovered = ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        var ink = Theme.U32(hovered ? Theme.Surface.Text : Theme.Surface.TextSecondary);
        dl.AddText(min, ink, label);
        if (underline || hovered)
        {
            var y = MathF.Round(min.Y + size.Y);
            dl.AddLine(new Vector2(min.X, y), new Vector2(min.X + size.X, y), ink, UiMetrics.Hairline);
        }

        Chrome.FocusRing(UiMetrics.Px(3f));
        if (hovered && tooltip is not null)
        {
            UiMetrics.Tooltip(tooltip);
        }

        return clicked;
    }

    /// <summary>Set aside for later and Not for me (or Bring back) at the end of a row's menu.</summary>
    private void SetAsideMenuItems(PlanEntry entry)
    {
        if (SetAside is not { CanSetAside: true } actions)
        {
            return;
        }

        ImGui.Separator();
        if (entry.IsSetAside)
        {
            if (ImGui.MenuItem(Strings.BluesBringBack))
            {
                BringBack(actions, entry);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.BluesBringBackTooltip);
            }

            return;
        }

        if (ImGui.MenuItem(Strings.BluesSetAsideForLater))
        {
            actions.SetAside(entry.Quest, notForMe: false);
            KeepInPlace(entry.Quest.RowId);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.BluesSetAsideForLaterTooltip);
        }

        if (ImGui.MenuItem(Strings.BluesNotForMe))
        {
            actions.SetAside(entry.Quest, notForMe: true);
            KeepInPlace(entry.Quest.RowId);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.BluesNotForMeTooltip);
        }
    }

    private void BringBack(SetAsideActions actions, PlanEntry entry)
    {
        actions.BringBack(entry.Quest);
        KeepInPlace(entry.Quest.RowId);
    }

    /// <summary>
    /// A row as one quiet line in its own height (P4): the moon and the name in Secondary, then what happened ("Set aside
    /// for later · it leaves your counts", "Brought back · it counts again") on the second line when the row has one,
    /// and Undo (or, in the Set aside view, Bring back) in the action slot at the right.
    /// </summary>
    private void DrawQuietRow(UiState ui, PlanEntry entry, Vector2 start, float width, float height, float firstLine)
    {
        var quest = entry.Quest;
        var line = ImGui.GetTextLineHeight();
        var glyph = UiMetrics.InlineGlyphSize(line);
        var gap = UiMetrics.Px(8f);
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();

        // What the row says, and its action: Undo for a row just moved, Bring back for a set-aside row in its view.
        var moved = kept.Contains(quest.RowId);
        var notForMe = SetAside?.IsNotForMe(quest.RowId) == true;
        var words = !entry.IsSetAside ? Strings.BluesBroughtBackLine : notForMe ? Strings.BluesNotForMeLine : Strings.BluesSetAsideLine;
        var action = moved ? Strings.BluesUndo : Strings.BluesBringBack;
        var actionTip = moved ? Strings.BluesUndoTooltip : Strings.BluesBringBackTooltip;
        var actionWidth = ImGui.CalcTextSize(action).X;
        var slot = MathF.Max(actionWidth, UiMetrics.Px(DoFirstSlotLogical * 0.5f));
        var textRight = start.X + width - slot - gap;

        var textY = start.Y + ((firstLine - line) * 0.5f);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + ((firstLine - glyph) * 0.5f)));
        using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * 0.6f))
        {
            MoonGlyph.DrawInline(entry.State, glyph);
        }

        var nameX = start.X + glyph + gap;
        var twoLines = height >= firstLine + line;
        var nameWidth = ImGui.CalcTextSize(entry.Name).X;
        var nameRoom = MathF.Max(1f, (twoLines ? textRight : textRight - ImGui.CalcTextSize(words).X - gap) - nameX);
        ImGui.SetCursorScreenPos(new Vector2(nameX, textY));
        if (Chrome.EllipsisSelectable(entry.Name, ui.SelectedRowId == quest.RowId, MathF.Min(nameWidth, nameRoom), out _))
        {
            ui.SelectedRowId = quest.RowId;
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(entry.Name, words);
        }

        var wordsPos = twoLines
            ? new Vector2(nameX, start.Y + firstLine + UiMetrics.Px(1f))
            : new Vector2(nameX + MathF.Min(nameWidth, nameRoom) + gap, textY);
        Chrome.EllipsisTextAt(dl, wordsPos, MathF.Max(0f, textRight - wordsPos.X), words, Theme.U32(s.TextSecondary));

        // The action, right-aligned in the slot and centred on the row.
        var actionSize = ImGui.CalcTextSize(action);
        ImGui.SetCursorScreenPos(new Vector2(start.X + width - actionWidth, start.Y + ((height - actionSize.Y) * 0.5f)));
        if (TextLink("##quietAction", action, actionTip, underline: moved) && SetAside is { } actions)
        {
            if (!moved)
            {
                BringBack(actions, entry);
            }
            else
            {
                // Undo of the move: the quest back exactly as it was before it (SetAsideEdits.Restore).
                actions.Undo(quest.RowId);
            }
        }

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(width, height));
    }

    /// <summary>
    /// A zone's label in Story order, with the group's "…" (Set aside these N…) at the right. A zone the story has not
    /// reached reads as its placeholder, whose hover and right-click (spec-1.20 N6) cover the label's own text only,
    /// never the "…".
    /// </summary>
    private void DrawZoneLabel(PlanZone zone)
    {
        var label = ZoneLabel(zone);
        var rows = GroupRows(zone.Entries);
        var start = ImGui.GetCursorScreenPos();
        var labelSize = ImGui.CalcTextSize(label);
        if (rows.Length < 2 || showSetAside || SetAside is not { CanSetAside: true })
        {
            var cut = Chrome.FitText(label, Theme.U32(Theme.Surface.TextTertiary), tooltip: HiddenZone(zone) is null);
            ShieldZoneLabel(zone, label, start, MathF.Min(labelSize.X, ImGui.GetItemRectMax().X - start.X), labelSize.Y, cut);
            return;
        }

        var size = MathF.Round(ImGui.GetTextLineHeight() + UiMetrics.Px(2f));
        var right = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X - UiMetrics.Px(10f);
        var dl = ImGui.GetWindowDrawList();
        var room = MathF.Max(1f, right - size - UiMetrics.Px(8f) - start.X);
        var labelCut = Chrome.EllipsisTextAt(dl, start, room, label, Theme.U32(Theme.Surface.TextTertiary));
        ShieldZoneLabel(zone, label, start, MathF.Min(labelSize.X, room), labelSize.Y, labelCut);
        using (ImRaii.PushId((int)zone.TerritoryId))
        {
            DrawGroupMenu(new Vector2(right - size, start.Y - UiMetrics.Px(1f)), size, rows, label);
        }

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(MathF.Max(1f, right - start.X), size));
    }

    /// <summary>The quests of a group that can still be set aside (not already set aside), built once per view.</summary>
    private uint[] GroupRows(IReadOnlyList<PlanEntry> entries)
    {
        if (groupRows.TryGetValue(entries, out var known))
        {
            return known;
        }

        var count = 0;
        foreach (var entry in entries)
        {
            count += entry.IsSetAside ? 0 : 1;
        }

        var rows = new uint[count];
        var i = 0;
        foreach (var entry in entries)
        {
            if (!entry.IsSetAside)
            {
                rows[i++] = entry.Quest.RowId;
            }
        }

        groupRows[entries] = rows;
        return rows;
    }

    /// <summary>A group's "…" and its menu: "Set aside these N…", which asks first (<see cref="DrawGroupConfirm"/>).</summary>
    private void DrawGroupMenu(Vector2 min, float size, uint[] rows, string groupName)
    {
        Keyboard.MoreButton("##groupMore", GroupMenuId, min, size);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.BluesGroupMenuTooltip);
        }

        using var popup = ImRaii.Popup(GroupMenuId);
        if (!popup)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        if (ImGui.MenuItem(string.Format(CultureInfo.CurrentCulture, Strings.BluesSetAsideGroupFormat, rows.Length)))
        {
            confirmRows = rows;
            confirmOwner = session.ViewedContentId;
            confirmTitle = string.Format(CultureInfo.CurrentCulture, Strings.BluesSetAsideConfirmTitleFormat, rows.Length);
            confirmBody = string.Format(CultureInfo.CurrentCulture, Strings.BluesSetAsideConfirmBodyFormat, groupName);
            confirmButton = string.Format(CultureInfo.CurrentCulture, Strings.BluesSetAsideConfirmButtonFormat, rows.Length);
            openConfirm = true;
        }
    }

    /// <summary>
    /// The 1.18 confirmation for a whole group (P4): "Set aside 5 quests?", what it does, then Set aside 5 (a plain
    /// button, not gold: it is not a call to act) and Cancel. Drawn once per frame in the cards' scope.
    /// </summary>
    private void DrawGroupConfirm()
    {
        if (openConfirm)
        {
            ImGui.OpenPopup(GroupConfirmId);
            openConfirm = false;
        }

        using var popup = ImRaii.Popup(GroupConfirmId);
        if (!popup)
        {
            return;
        }

        // Asked for one character: another on view closes the question rather than act on its list.
        if (confirmOwner != session.ViewedContentId || confirmOwner is not { } owner)
        {
            ImGui.CloseCurrentPopup();
            return;
        }

        UiMetrics.ApplyFontScale();
        ImGui.TextUnformatted(confirmTitle);
        using (Typography.Caption())
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + UiMetrics.Px(320f));
            ImGui.TextUnformatted(confirmBody);
            ImGui.PopTextWrapPos();
        }

        ImGui.Spacing();
        if (ImGui.Button(confirmButton))
        {
            SetAside?.SetAside(owner, confirmRows);
            foreach (var row in confirmRows)
            {
                KeepInPlace(row);
            }

            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button(Strings.Cancel))
        {
            ImGui.CloseCurrentPopup();
        }
    }

    /// <summary>The tier chip's size for <paramref name="label"/> in the caption role (a neutral pill, like the kind pills).</summary>
    private static Vector2 TierChipSize(string label) =>
        ImGui.CalcTextSize(label) + new Vector2(UiMetrics.Px(7f) * 2f, UiMetrics.Px(2f) * 2f);

    /// <summary>
    /// Paints the tier chip ending at <paramref name="end"/> (P4: one more chip on a Story order row) and gives it an
    /// item whose hover explains the five tiers; returns where it ends.
    /// </summary>
    private static float DrawTierChip(ImDrawListPtr dl, string label, float end, float top, float height)
    {
        Vector2 size;
        using (Typography.Caption())
        {
            size = TierChipSize(label);
            var min = new Vector2(end - size.X, top + ((height - size.Y) * 0.5f));
            var tone = Theme.Surface.TextSecondary;
            Chrome.PillAt(dl, min, size, label, Theme.WithAlpha(tone, 0.06f), Theme.WithAlpha(tone, 0.45f), Theme.U32(tone));
            ImGui.SetCursorScreenPos(min);
            ImGui.InvisibleButton("##tier", size);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(label, Strings.BluesTierTooltip);
        }

        return end;
    }
}
