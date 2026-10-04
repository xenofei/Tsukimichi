using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The 1.9.0 planning extras on the Characters dashboard (feature plan v5 "Planning extras"; R6 C, E, F), under the Jobs
/// table: "Levelling opens", the job rows again with what their next level opens ("52→56 opens 7 quests (2 unlock
/// quests, MSQ)"; hover for every level and the quests, click to show the first); the main scenario catch-up line
/// (hover for each expansion); and the allied society board (Settings › Display › Planning), each unlocked society
/// with its rank and reputation, today's dailies, its giver's zone with Teleport, the allowances left and the time to the
/// daily reset. Everything is <see cref="PlanningSource"/>'s, built once per session version; nothing allocates per frame.
/// </summary>
public sealed partial class CharactersPane
{
    /// <summary>The planning lines; set by the plugin. Null hides the three blocks.</summary>
    public PlanningSource? Planning { get; set; }

    private void DrawPlanning(UiState ui, Dashboard d)
    {
        if (Planning is not { } planning)
        {
            return;
        }

        using var id = ImRaii.PushId("planning");
        DrawLevelAdvice(ui, d, planning);
        DrawCatchUp(planning);
        if (settings.ShowAlliedSocietyBoard)
        {
            DrawAlliedBoard(planning);
        }
    }

    /// <summary>"Levelling opens": one row per job of the Jobs table whose next level opens something.</summary>
    private void DrawLevelAdvice(UiState ui, Dashboard d, PlanningSource planning)
    {
        if (!planning.HasAdvice || d.Jobs.Length == 0)
        {
            return;
        }

        Gap();
        SectionHeading.Draw(Strings.PlanningLevelHeading);
        using var table = ImRaii.Table("##levelAdvice", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        var line = ImGui.GetTextLineHeight();
        var iconSize = UiMetrics.Square(UiMetrics.JobIconSize);
        ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, MathF.Max(line * 1.4f, iconSize.X));
        ImGui.TableSetupColumn(Strings.CharactersColumnJob, ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("##opens", ImGuiTableColumnFlags.WidthStretch, 2f);
        foreach (var row in d.Jobs)
        {
            if (planning.AdviceFor(row.JobId) is not { } advice)
            {
                continue;
            }

            ImGui.TableNextRow();
            using var rowId = ImRaii.PushId((int)row.JobId);
            ImGui.TableNextColumn();
            DrawJobIcon(row.IconId, iconSize, row.Name, row.Level);
            ImGui.TableNextColumn();
            Chrome.FitText(row.Name, ImGui.GetColorU32(ImGuiCol.Text));
            ImGui.TableNextColumn();
            if (Chrome.EllipsisSelectable(advice.Row, false, 0f, out _) && advice.First is { } first)
            {
                ui.Reveal(first);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(advice.Tooltip, Strings.PlanningLevelRowTooltip);
            }
        }
    }

    /// <summary>"To reach the latest story: 143 quests, Lv 90–100, 6 duties"; hover for each expansion.</summary>
    private static void DrawCatchUp(PlanningSource planning)
    {
        var text = planning.CatchUpLine;
        if (text.Length == 0)
        {
            return;
        }

        Gap();
        SectionHeading.Draw(Strings.PlanningCatchUpHeading);
        TextFlow.Wrapped(text);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(planning.CatchUpTooltip);
        }

        // How the story's duties ahead can be cleared (1.19.0, C7).
        if (planning.CatchUpOthersLine is { Length: > 0 } others)
        {
            TextFlow.Wrapped(others, 0f, Theme.U32(Theme.Surface.TextSecondary));
        }
    }

    /// <summary>The allied society board: the allowances and the reset, then one row per unlocked society.</summary>
    private void DrawAlliedBoard(PlanningSource planning)
    {
        Gap();
        SectionHeading.Draw(Strings.PlanningBoardHeading);
        var header = planning.BoardHeader;
        if (header.Length == 0)
        {
            return;
        }

        var lines = planning.BoardLines;
        if (lines.Count == 0)
        {
            ImGui.TextDisabled(Strings.PlanningBoardNone);
            return;
        }

        TextFlow.Wrapped(header, 0f, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        using var table = ImRaii.Table("##alliedBoard", 4, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        ImGui.TableSetupColumn(Strings.PlanningBoardColumnSociety, ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn(Strings.PlanningBoardColumnRank, ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn(Strings.PlanningBoardColumnToday, ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn(Strings.PlanningBoardColumnWhere, ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableHeadersRow();
        var sheets = IconSheets;
        var iconSize = MathF.Round(UiMetrics.JobIconSize);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            using var rowId = ImRaii.PushId(i);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            // The society's emblem leads its name, as on the dashboard's allied societies table (UI-5d).
            var emblem = PaneIcons.Tribe(line.Row.Tribe, sheets);
            if (emblem != 0)
            {
                DrawLeadIcon(NodeIcon.Game(emblem), iconSize);
            }

            if (!Chrome.FitText(line.Society, ImGui.GetColorU32(ImGuiCol.Text)) && ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(line.Society);
            }

            ImGui.TableNextColumn();
            var rankColor = line.Row.Maxed ? Theme.U32(Theme.Accent) : ImGui.GetColorU32(ImGuiCol.Text);
            var rankFits = Chrome.FitText(line.Rank, rankColor);
            if (ImGui.IsItemHovered() && (!rankFits || line.RankTooltip.Length > 0))
            {
                UiMetrics.Tooltip(line.Rank, line.RankTooltip.Length > 0 ? line.RankTooltip : null);
            }

            ImGui.TableNextColumn();
            var todayFits = Chrome.FitText(line.Today, ImGui.GetColorU32(line.Row.OfferedToday is null ? ImGuiCol.TextDisabled : ImGuiCol.Text));
            if (ImGui.IsItemHovered() && (!todayFits || line.TodayTooltip.Length > 0))
            {
                UiMetrics.Tooltip(line.Today, line.TodayTooltip.Length > 0 ? line.TodayTooltip : null);
            }

            ImGui.TableNextColumn();
            DrawBoardWhere(line);
        }
    }

    /// <summary>The giver's zone and, when there is a giver, Teleport (through Lifestream; greyed with the reason when it cannot).</summary>
    private void DrawBoardWhere(PlanningSource.BoardLine line)
    {
        if (line.Row.Giver is not { } giver || Links is not { TeleportShown: true } links)
        {
            ImGui.TextDisabled(line.Zone);
            return;
        }

        var teleportWidth = Chrome.ActionPillWidth(ActionIcons.TeleportIcon, Strings.PlanningBoardTeleport, PillLayout.Row);
        if (line.Zone.Length > 0)
        {
            Chrome.FitText(line.Zone, ImGui.GetColorU32(ImGuiCol.Text));
            Chrome.SameLineOrWrap(teleportWidth);
        }

        TravelControls.TeleportButton(links, giver, Strings.PlanningBoardTeleport);
    }
}
