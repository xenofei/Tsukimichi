using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Characters;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unlocks;

namespace Tsukimichi.Ui;

/// <summary>
/// Characters › All characters (plan v7, 1.21.0 P3 and N11; spec-1.21 P3): one fixed-header table of every character,
/// this folder's and the linked launcher folders', two lines a row (44 px at 100 %): Character, Job, Story, Goal, Ready,
/// Today, Moonlit and Last seen. Sorted by any header; by default Story, starred characters first. Below 1,000 px the
/// Moonlit and Today columns hide (the header's right-click turns them back on) rather than every column squeezing.
/// A click opens the character's dashboard; the right-click stars it, names its role and nickname and sets its goal.
/// Rows logged in on another client or read from another folder are read only: they say where they are, and nothing
/// here starts a hand-off for them. Row text is built when the rows, the minute or the language change, never per frame.
/// </summary>
public sealed partial class CharactersPane
{
    private const string GoalPopupId = "##rosterGoal";
    private const string LabelPopupId = "##rosterLabel";
    private const float RosterRowLogical = 44f;

    private static readonly string StarGlyph = FontAwesomeIcon.Star.ToIconString();

    /// <summary>Every character's standing, goals and states (1.21.0 P3); set by the plugin. Null shows the view empty.</summary>
    public RosterSource? Board { get; set; }

    private (int Revision, long Minute, int Language, RosterColumn Column, bool Descending) rosterKey;
    private RosterLine[] rosterLines = [];
    private RosterColumn rosterColumn = RosterColumn.Story;
    private bool rosterDescending;
    private string rosterCaption = string.Empty;
    private bool openGoalPopup;
    private bool openLabelPopup;

    // The label popup: which character and which label (true: nickname, false: role), and the text being typed.
    private ulong labelFor;
    private bool labelNickname;
    private string labelText = string.Empty;

    /// <summary>One row's words, built once per rows, minute and language.</summary>
    private sealed record RosterLine(
        RosterRow Row,
        string Name,
        string Detail,
        string Job,
        uint JobIcon,
        string Story1,
        string Story2,
        string Goal1,
        string Goal2,
        bool GoalLink,
        bool GoalReached,
        string Ready,
        string Today1,
        string Today2,
        string Moonlit,
        string Seen1,
        string Seen2,
        string Tooltip);

    /// <summary>"9 characters · 2 live in other clients", right of the view switch while the roster shows.</summary>
    private void DrawRosterCaption()
    {
        if (view != 1 || rosterCaption.Length == 0)
        {
            return;
        }

        var width = ImGui.CalcTextSize(rosterCaption).X;
        ImGui.SameLine();
        var room = ImGui.GetContentRegionAvail().X;
        if (room > width)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + room - width);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + MathF.Max(0f, (UiMetrics.MinTarget - ImGui.GetTextLineHeight()) * 0.5f));
            ImGui.TextColored(Theme.Surface.TextSecondary, rosterCaption);
        }
        else
        {
            ImGui.NewLine();
        }
    }

    /// <summary>The roster view: the table, its menus and the goal popover.</summary>
    private void DrawRoster(UiState ui)
    {
        if (Board is not { } board)
        {
            ImGui.TextDisabled(Strings.AltsGridNeedsCatalog);
            return;
        }

        RefreshRoster(board);
        if (rosterLines.Length == 0)
        {
            ImGui.TextWrapped(Strings.AltsGridNoCharacters);
            return;
        }

        var paneLogical = ImGui.GetContentRegionAvail().X / MathF.Max(0.01f, UiMetrics.Scale);
        var narrowOn = settings.RosterNarrowColumns;
        const ImGuiTableFlags flags = ImGuiTableFlags.ScrollY | ImGuiTableFlags.Sortable | ImGuiTableFlags.SizingStretchProp
            | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.PadOuterX;
        using (var table = ImRaii.Table("##roster", 8, flags, new Vector2(0f, MathF.Max(UiMetrics.Px(120f), ImGui.GetContentRegionAvail().Y))))
        {
            if (table)
            {
                DrawRosterTable(ui, board, paneLogical, narrowOn);
            }
        }

        if (openGoalPopup)
        {
            openGoalPopup = false;
            ImGui.OpenPopup(GoalPopupId);
        }

        if (openLabelPopup)
        {
            openLabelPopup = false;
            ImGui.OpenPopup(LabelPopupId);
        }

        DrawGoalPopover(board);
        DrawLabelPopup(board.Settings);
    }

    private void DrawRosterTable(UiState ui, RosterSource board, float paneLogical, bool narrowOn)
    {
        ImGui.TableSetupScrollFreeze(0, 1);
        var numberWidth = MathF.Max(ImGui.CalcTextSize("9999").X, ImGui.CalcTextSize(Strings.RosterColumnMoonlit).X) + UiMetrics.Px(22f);
        ImGui.TableSetupColumn(Strings.RosterColumnCharacter, ImGuiTableColumnFlags.WidthStretch, 1.6f);
        ImGui.TableSetupColumn(Strings.RosterColumnJob, ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("BRD 100").X + UiMetrics.Px(34f));
        ImGui.TableSetupColumn(Strings.RosterColumnStory, ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultSort, 2.4f);
        ImGui.TableSetupColumn(Strings.RosterColumnGoal, ImGuiTableColumnFlags.WidthStretch, 1.8f);
        ImGui.TableSetupColumn(Strings.RosterColumnReady, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.PreferSortDescending, numberWidth);
        ImGui.TableSetupColumn(Strings.RosterColumnToday, ImGuiTableColumnFlags.WidthStretch, 1.1f);
        ImGui.TableSetupColumn(Strings.RosterColumnMoonlit, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.PreferSortDescending, numberWidth);
        ImGui.TableSetupColumn(Strings.RosterColumnLastSeen, ImGuiTableColumnFlags.WidthStretch, 1.1f);
        ImGui.TableSetColumnEnabled((int)RosterColumn.Today, RosterBoard.Shows(RosterColumn.Today, paneLogical, narrowOn));
        ImGui.TableSetColumnEnabled((int)RosterColumn.Moonlit, RosterBoard.Shows(RosterColumn.Moonlit, paneLogical, narrowOn));
        var headerTop = ImGui.GetCursorScreenPos();
        ImGui.TableHeadersRow();
        var headerBottom = ImGui.GetItemRectMax().Y;
        if (ImGui.IsWindowHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right)
            && ImGui.IsMouseHoveringRect(headerTop, new Vector2(headerTop.X + ImGui.GetWindowWidth(), headerBottom)))
        {
            ImGui.OpenPopup("##rosterColumns");
        }

        using (var menu = ImRaii.Popup("##rosterColumns"))
        {
            if (menu)
            {
                UiMetrics.ApplyFontScale();
                if (ImGui.MenuItem(Strings.RosterNarrowColumns, string.Empty, narrowOn))
                {
                    settings.RosterNarrowColumns = !narrowOn;
                    saveSettings();
                }
            }
        }

        ReadRosterSort();
        var rowHeight = MathF.Max(UiMetrics.Px(RosterRowLogical), (ImGui.GetTextLineHeight() * 2f) + UiMetrics.Px(8f));
        for (var i = 0; i < rosterLines.Length; i++)
        {
            DrawRosterRow(ui, board, rosterLines[i], i, rowHeight);
        }
    }

    private void ReadRosterSort()
    {
        var specs = ImGui.TableGetSortSpecs();
        if (specs.IsNull || !specs.SpecsDirty)
        {
            return;
        }

        if (specs.SpecsCount > 0)
        {
            var spec = specs.Specs;
            rosterColumn = (RosterColumn)Math.Clamp((int)spec.ColumnIndex, 0, (int)RosterColumn.LastSeen);
            rosterDescending = spec.SortDirection == ImGuiSortDirection.Descending;
        }
        else
        {
            rosterColumn = RosterColumn.Story;
            rosterDescending = false;
        }

        specs.SpecsDirty = false;
    }

    private void DrawRosterRow(UiState ui, RosterSource board, RosterLine line, int index, float rowHeight)
    {
        var row = line.Row;
        var s = Theme.Surface;
        using var id = ImRaii.PushId(index);
        ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
        ImGui.TableSetColumnIndex(0);
        var selected = session.ViewedContentId == row.ContentId;
        var cellMin = ImGui.GetCursorScreenPos();
        ImGui.PushStyleColor(ImGuiCol.Header, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, Vector4.Zero);
        var clicked = ImGui.Selectable("##rosterRow", selected, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap, new Vector2(0f, rowHeight));
        ImGui.PopStyleColor(3);
        var hovered = ImGui.IsItemHovered();
        var rowMin = new Vector2(ImGui.GetItemRectMin().X, cellMin.Y);
        var rowMax = new Vector2(ImGui.GetItemRectMax().X, cellMin.Y + rowHeight);
        var dl = ImGui.GetWindowDrawList();

        // The selected row: a faint Moon wash and a gold 2 px left edge (1.14's selection); a hovered one the Hover tone.
        // The wash spans the row through the table's own row background (a cell's draw list is clipped to its column).
        if (selected)
        {
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, Theme.WithAlpha(Theme.Gold, Theme.IsLight ? 0.10f : 0.07f));
            dl.AddRectFilled(rowMin, new Vector2(rowMin.X + UiMetrics.Px(2f), rowMax.Y), Theme.U32(Theme.Gold));
        }
        else if (hovered)
        {
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, Theme.U32(s.Hover));
        }

        if (clicked)
        {
            if (row.Place == RosterPlace.OtherFolder)
            {
                ShowToast(string.Format(CultureInfo.CurrentCulture, Strings.RosterOtherFolderToastFormat, row.Name));
            }
            else
            {
                View(ui, row.ContentId);
                view = -1;
            }
        }

        if (hovered && line.Tooltip.Length > 0)
        {
            UiMetrics.Tooltip(line.Name, line.Tooltip);
        }

        if (ImGui.BeginPopupContextItem("##rosterMenu"))
        {
            UiMetrics.ApplyFontScale();
            DrawRosterMenu(ui, board, line);
            ImGui.EndPopup();
        }

        var line1 = ImGui.GetTextLineHeight();
        var top = cellMin.Y + MathF.Round((rowHeight - (line1 * 2f)) * 0.5f);
        var text = Theme.U32(s.Text);
        var secondary = Theme.U32(s.TextSecondary);

        // Character: a Text-coloured star for a starred one, the name; world · role under it.
        var x = cellMin.X + UiMetrics.Px(6f);
        var room = ImGui.GetContentRegionAvail().X - UiMetrics.Px(6f);
        if (row.Starred)
        {
            using (ImRaii.PushFont(Dalamud.Interface.UiBuilder.IconFont))
            {
                dl.AddText(ImGui.GetFont(), ImGui.GetFontSize() * 0.75f, new Vector2(x, top + UiMetrics.Px(3f)), text, StarGlyph);
            }

            x += UiMetrics.Px(14f);
            room -= UiMetrics.Px(14f);
        }

        Chrome.EllipsisTextAt(dl, new Vector2(x, top), MathF.Max(1f, room), line.Name, text);
        Chrome.EllipsisTextAt(dl, new Vector2(cellMin.X + UiMetrics.Px(6f), top + line1), MathF.Max(1f, room), line.Detail, secondary);

        // Job: the 20 px tile and "BRD 100".
        if (ImGui.TableSetColumnIndex((int)RosterColumn.Job))
        {
            var at = ImGui.GetCursorScreenPos();
            var icon = MathF.Round(UiMetrics.Px(20f));
            var iconTop = cellMin.Y + MathF.Round((rowHeight - icon) * 0.5f);
            if (line.JobIcon != 0 && textures is not null)
            {
                GameIcon.DrawAt(dl, textures, line.JobIcon, new Vector2(at.X, iconTop), new Vector2(at.X + icon, iconTop + icon), UiMetrics.Px(3f));
            }

            dl.AddText(new Vector2(at.X + icon + UiMetrics.Px(6f), cellMin.Y + MathF.Round((rowHeight - line1) * 0.5f)), text, line.Job);
        }

        TwoLines(RosterColumn.Story, line.Story1, line.Story2, text, secondary, cellMin.Y, top, line1);
        if (ImGui.TableSetColumnIndex((int)RosterColumn.Goal))
        {
            DrawGoalCell(board, line, dl, top, line1);
        }

        RightNumber(RosterColumn.Ready, line.Ready, text, cellMin.Y, rowHeight, line1);
        TwoLines(RosterColumn.Today, line.Today1, line.Today2, text, secondary, cellMin.Y, top, line1);
        RightNumber(RosterColumn.Moonlit, line.Moonlit, text, cellMin.Y, rowHeight, line1);
        if (ImGui.TableSetColumnIndex((int)RosterColumn.LastSeen))
        {
            var at = ImGui.GetCursorScreenPos();
            var seenX = at.X;
            if (row.Live && !Theme.Glyphs.HighContrast)
            {
                // Live: a 6 px Text dot and the word (the word alone under high contrast).
                var dot = UiMetrics.Px(3f);
                dl.AddCircleFilled(new Vector2(seenX + dot, top + (line1 * 0.5f)), dot, text, 12);
                seenX += (dot * 2f) + UiMetrics.Px(5f);
            }

            var seenRoom = MathF.Max(1f, ImGui.GetContentRegionAvail().X - (seenX - at.X));
            Chrome.EllipsisTextAt(dl, new Vector2(seenX, top), seenRoom, line.Seen1, text);
            Chrome.EllipsisTextAt(dl, new Vector2(at.X, top + line1), MathF.Max(1f, ImGui.GetContentRegionAvail().X), line.Seen2, secondary);
        }
    }

    private static void TwoLines(RosterColumn column, string first, string second, uint text, uint secondary, float rowTop, float top, float line)
    {
        _ = rowTop;
        if (!ImGui.TableSetColumnIndex((int)column))
        {
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var at = ImGui.GetCursorScreenPos();
        var room = MathF.Max(1f, ImGui.GetContentRegionAvail().X - UiMetrics.Px(4f));
        Chrome.EllipsisTextAt(dl, new Vector2(at.X, top), room, first, text);
        Chrome.EllipsisTextAt(dl, new Vector2(at.X, top + line), room, second, secondary);
    }

    /// <summary>A right-aligned count, with the 22 px gutter before the text column after it.</summary>
    private static void RightNumber(RosterColumn column, string value, uint ink, float rowTop, float rowHeight, float line)
    {
        if (!ImGui.TableSetColumnIndex((int)column))
        {
            return;
        }

        var at = ImGui.GetCursorScreenPos();
        var room = ImGui.GetContentRegionAvail().X - UiMetrics.Px(22f);
        var width = ImGui.CalcTextSize(value).X;
        ImGui.GetWindowDrawList().AddText(new Vector2(at.X + MathF.Max(0f, room - width), rowTop + MathF.Round((rowHeight - line) * 0.5f)), ink, value);
    }

    /// <summary>The Goal cell: the goal and what is left, "Goal reached" with "Set a new goal", or the "Set a goal…" link.</summary>
    private void DrawGoalCell(RosterSource board, RosterLine line, ImDrawListPtr dl, float top, float lineHeight)
    {
        _ = board;
        var s = Theme.Surface;
        var at = ImGui.GetCursorScreenPos();
        var room = MathF.Max(1f, ImGui.GetContentRegionAvail().X - UiMetrics.Px(4f));
        if (line.GoalLink)
        {
            // "Set a goal…": a dotted-underline link in Secondary.
            var width = MathF.Min(room, ImGui.CalcTextSize(line.Goal1).X);
            ImGui.SetCursorScreenPos(new Vector2(at.X, top));
            var clicked = ImGui.InvisibleButton("##setGoal", new Vector2(width, lineHeight));
            var hovered = ImGui.IsItemHovered();
            var ink = Theme.U32(hovered ? s.Text : s.TextSecondary);
            dl.AddText(new Vector2(at.X, top), ink, line.Goal1);
            DottedUnderline(dl, at.X, at.X + width, top + lineHeight - UiMetrics.Px(1f), ink);
            if (hovered)
            {
                UiMetrics.Tooltip(Strings.GoalSetTooltip);
            }

            if (clicked)
            {
                OpenGoal(board, line.Row);
            }

            return;
        }

        // A goal: its title, and what is left (or "Goal reached" in silver, semibold, with "Set a new goal").
        if (line.GoalReached)
        {
            ImGui.SetCursorScreenPos(new Vector2(at.X, top));
            Chrome.SemiboldText(line.Goal1, s.TextSecondary);
        }
        else
        {
            Chrome.EllipsisTextAt(dl, new Vector2(at.X, top), room, line.Goal1, Theme.U32(s.Text));
        }

        ImGui.SetCursorScreenPos(new Vector2(at.X, top + lineHeight));
        var detailWidth = MathF.Min(room, ImGui.CalcTextSize(line.Goal2).X);
        if (ImGui.InvisibleButton("##editGoal", new Vector2(MathF.Max(1f, detailWidth), lineHeight)))
        {
            OpenGoal(board, line.Row);
        }

        var over = ImGui.IsItemHovered();
        Chrome.EllipsisTextAt(dl, new Vector2(at.X, top + lineHeight), room, line.Goal2, Theme.U32(over ? s.Text : s.TextSecondary));
        if (over)
        {
            UiMetrics.Tooltip(Strings.GoalEditTooltip);
        }
    }

    private static void DottedUnderline(ImDrawListPtr dl, float from, float to, float y, uint ink)
    {
        var dash = UiMetrics.Px(2f);
        for (var x = from; x < to; x += dash * 2f)
        {
            dl.AddLine(new Vector2(x, y), new Vector2(MathF.Min(x + dash, to), y), ink, UiMetrics.Hairline);
        }
    }

    /// <summary>The row's menu: view, star, role, nickname, goal.</summary>
    private void DrawRosterMenu(UiState ui, RosterSource board, RosterLine line)
    {
        var row = line.Row;
        var book = board.Settings;
        if (ImGui.MenuItem(Strings.AltsMenuView, string.Empty, false, row.Place != RosterPlace.OtherFolder && session.ViewedContentId != row.ContentId))
        {
            View(ui, row.ContentId);
            view = -1;
        }

        if (ImGui.MenuItem(row.Starred ? Strings.RosterMenuUnstar : Strings.RosterMenuStar))
        {
            book.Edit(CharacterSettingChange.Star(row.ContentId, !row.Starred));
        }

        if (ImGui.MenuItem(Strings.RosterMenuRole))
        {
            BeginLabel(row, nickname: false);
        }

        if (ImGui.MenuItem(Strings.RosterMenuNickname))
        {
            BeginLabel(row, nickname: true);
        }

        ImGui.Separator();
        if (ImGui.MenuItem(row.Goal is null ? Strings.GoalSetLink : Strings.GoalChange))
        {
            OpenGoal(board, row);
        }

        if (row.Goal is { } goal && ImGui.MenuItem(Strings.GoalClear))
        {
            ClearGoal(board, row.ContentId, row.Name, goal);
        }
    }

    private void BeginLabel(RosterRow row, bool nickname)
    {
        labelFor = row.ContentId;
        labelNickname = nickname;
        labelText = (nickname ? row.Nickname : row.Role) ?? string.Empty;
        openLabelPopup = true;
    }

    /// <summary>The role or nickname editor: a text field, Save and Cancel; an empty field clears it.</summary>
    private void DrawLabelPopup(CharacterSettingsBook book)
    {
        using var popup = ImRaii.Popup(LabelPopupId);
        if (!popup)
        {
            return;
        }

        UiMetrics.ApplyFontScale();
        ImGui.TextUnformatted(labelNickname ? Strings.RosterNicknameTitle : Strings.RosterRoleTitle);
        ImGui.SetNextItemWidth(UiMetrics.Px(220f));
        if (ImGui.IsWindowAppearing())
        {
            ImGui.SetKeyboardFocusHere();
        }

        var enter = ImGui.InputTextWithHint("##label", labelNickname ? Strings.RosterNicknameHint : Strings.RosterRoleHint, ref labelText, CharacterSettings.MaxLabelLength, ImGuiInputTextFlags.EnterReturnsTrue);
        if (Chrome.ActionChip("##labelSave", Strings.RosterLabelSave, accent: true) || enter)
        {
            book.Edit(labelNickname ? CharacterSettingChange.SetNickname(labelFor, labelText) : CharacterSettingChange.SetRole(labelFor, labelText));
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (Chrome.ActionChip("##labelCancel", Strings.Cancel))
        {
            ImGui.CloseCurrentPopup();
        }
    }

    private void RefreshRoster(RosterSource board)
    {
        var rows = board.Rows;
        var key = (board.Revision, DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute, Localization.Loc.Version, rosterColumn, rosterDescending);
        if (key == rosterKey && rosterLines.Length == rows.Count)
        {
            return;
        }

        rosterKey = key;
        var sorted = RosterBoard.Sort(rows, rosterColumn, rosterDescending);
        var lines = new RosterLine[sorted.Count];
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = LineFor(board, sorted[i]);
        }

        rosterLines = lines;
        var (total, elsewhere) = RosterBoard.Counts(rows);
        var characters = total == 1 ? Strings.RosterCountOne : string.Format(CultureInfo.CurrentCulture, Strings.RosterCountFormat, total);
        rosterCaption = elsewhere == 0 ? characters
            : characters + Strings.GoalSeparator + (elsewhere == 1 ? Strings.RosterLiveElsewhereOne : string.Format(CultureInfo.CurrentCulture, Strings.RosterLiveElsewhereFormat, elsewhere));
    }

    private RosterLine LineFor(RosterSource board, RosterRow row)
    {
        var bundle = session.Bundle;
        var names = bundle?.Names;
        var name = row.Nickname is { } nickname ? string.Format(CultureInfo.CurrentCulture, Strings.RosterNicknameFormat, nickname, row.Name) : row.Name;
        var detail = row.Role is { } role ? row.WorldName + Strings.GoalSeparator + role : row.WorldName;
        var job = row.Job == 0 ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.RosterJobFormat, names?.ClassJobAbbreviation(row.Job) ?? string.Empty, row.JobLevel);

        string story1;
        string story2;
        if (row.Story is not { } story)
        {
            story1 = row.Ready is null ? Strings.RosterBeingRead : Strings.RosterUnreadable;
            story2 = string.Empty;
        }
        else if (story.CaughtUp)
        {
            story1 = story.Part;
            story2 = Strings.RosterCaughtUp;
        }
        else
        {
            // That character's own next quest: its current one, so its shield never hides it (spec-1.21 P3 Spoilers).
            story1 = string.Format(CultureInfo.CurrentCulture, Strings.RosterStoryNextFormat, story.Part, story.Next!.Name);
            story2 = story.LevelNeeded > 0
                ? string.Format(CultureInfo.CurrentCulture, Strings.RosterLevelNeededFormat, story.LevelNeeded)
                : string.Format(CultureInfo.CurrentCulture, Strings.RosterToLatestFormat, story.LeftToLatest);
        }

        string goal1;
        string goal2;
        var link = false;
        var reached = false;
        Func<byte, string>? expansion = names is null ? null : e => names.Expansion(e);
        if (row.Goal is not { } goal)
        {
            goal1 = Strings.GoalSetLink;
            goal2 = string.Empty;
            link = true;
        }
        else if (row.GoalProgress is { Reached: true })
        {
            goal1 = Strings.GoalReached;
            goal2 = GoalText.Title(goal, board, expansion) + Strings.GoalSeparator + Strings.GoalSetNew;
            reached = true;
        }
        else
        {
            goal1 = GoalText.Title(goal, board, expansion);
            goal2 = GoalText.Left(row.GoalProgress);
        }

        var today1 = row.Allowances is { } allowances ? string.Format(CultureInfo.CurrentCulture, allowances == 1 ? Strings.RosterAllowanceOne : Strings.RosterAllowancesFormat, allowances) : Strings.RosterNotRead;
        var today2 = row.LeveAllowances is { } leves ? string.Format(CultureInfo.CurrentCulture, Strings.RosterLevesFormat, leves) : string.Empty;

        (string seen1, string seen2) = row.Place switch
        {
            RosterPlace.Here => (Strings.RosterLive, Strings.RosterThisClient),
            RosterPlace.OtherClient => (Strings.RosterLive, Strings.RosterOtherClient),
            RosterPlace.OtherFolder when row.Live => (Strings.RosterLive, Strings.RosterOtherClient),
            RosterPlace.OtherFolder => (Age(row.TakenUtc), Strings.RosterOtherFolder),
            _ => (Age(row.TakenUtc), string.Empty),
        };

        var tooltip = row.Place switch
        {
            RosterPlace.OtherClient => Strings.RosterReadOnlyClientTooltip,
            RosterPlace.OtherFolder => string.Format(CultureInfo.CurrentCulture, Strings.RosterReadOnlyFolderTooltipFormat, row.Folder ?? string.Empty),
            _ => string.Empty,
        };

        return new RosterLine(
            row,
            name,
            detail,
            job,
            row.Job == 0 ? 0u : QuestUnlocks.JobIconBase + row.Job,
            story1,
            story2,
            goal1,
            goal2,
            link,
            reached,
            row.Ready?.ToString(CultureInfo.CurrentCulture) ?? string.Empty,
            today1,
            today2,
            row.MoonlitLeft?.ToString(CultureInfo.CurrentCulture) ?? string.Empty,
            seen1,
            seen2,
            tooltip);
    }
}
