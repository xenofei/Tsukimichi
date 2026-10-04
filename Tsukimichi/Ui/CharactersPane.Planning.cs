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

        // One row per society (spec-1.19 "C5. Allied societies"): its emblem on a 22 px tile, "Name · Rank", one line
        // under it, and the row's actions at its end (the giver's zone and Teleport; Flag as well for a daily carried
        // over the reset, which the giver takes back).
        using var table = ImRaii.Table("##alliedBoard", 3, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        var tile = MathF.Round(UiMetrics.Px(22f));
        ImGui.TableSetupColumn("##emblem", ImGuiTableColumnFlags.WidthFixed, tile);
        ImGui.TableSetupColumn(Strings.PlanningBoardColumnSociety, ImGuiTableColumnFlags.WidthStretch, 6f);
        ImGui.TableSetupColumn(Strings.PlanningBoardColumnWhere, ImGuiTableColumnFlags.WidthStretch, 3f);
        var sheets = IconSheets;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            using var rowId = ImRaii.PushId(i);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            // The society's emblem leads the row, as on the dashboard's allied societies table (UI-5d).
            var emblem = PaneIcons.Tribe(line.Row.Tribe, sheets);
            if (emblem != 0)
            {
                DrawLeadIcon(NodeIcon.Game(emblem), tile);
            }

            ImGui.TableNextColumn();
            DrawBoardSociety(line);
            ImGui.TableNextColumn();
            DrawBoardWhere(line);
        }
    }

    /// <summary>
    /// "Moogles · Rank 6" (the rank's reputation in its words, gold once full), the alt's name on a stored character's
    /// carried row, and the one line under it: a carried-over daily (a copper dot, Text), the rank-up hint or today.
    /// </summary>
    private static void DrawBoardSociety(PlanningSource.BoardLine line)
    {
        var s = Theme.Surface;
        Chrome.FitText(line.Society, Theme.U32(s.Text));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(line.Society);
        }

        ImGui.SameLine(0f, 0f);
        Chrome.FitText(Strings.StateReasonSeparator, Theme.U32(s.TextTertiary));
        ImGui.SameLine(0f, 0f);
        var rankFits = Chrome.FitText(line.Rank, line.Row.Maxed ? Theme.U32(Theme.Accent) : Theme.U32(s.Text));
        if (ImGui.IsItemHovered() && (!rankFits || line.RankTooltip.Length > 0))
        {
            UiMetrics.Tooltip(line.Rank, line.RankTooltip.Length > 0 ? line.RankTooltip : null);
        }

        if (line.AltNote.Length > 0)
        {
            ImGui.SameLine(0f, 0f);
            Chrome.FitText(line.AltNote, Theme.U32(s.TextTertiary));
        }

        if (line.Line.Length == 0)
        {
            return;
        }

        if (line.NeedsYou)
        {
            // The copper dot (6 px) beside words that say the same thing; never the only carrier.
            var dot = UiMetrics.Px(6f);
            var at = ImGui.GetCursorScreenPos();
            var lineHeight = ImGui.GetTextLineHeight();
            ImGui.Dummy(new System.Numerics.Vector2(dot, lineHeight));
            ImGui.GetWindowDrawList().AddCircleFilled(new System.Numerics.Vector2(at.X + (dot * 0.5f), at.Y + (lineHeight * 0.5f)), dot * 0.5f, Theme.U32(Theme.Copper), 12);
            ImGui.SameLine(0f, UiMetrics.Px(6f));
        }

        TextFlow.Wrapped(line.Line, 0f, Theme.U32(line.NeedsYou ? s.Text : s.TextSecondary));
        if (ImGui.IsItemHovered() && line.LineTooltip.Length > 0)
        {
            UiMetrics.Tooltip(line.Line, line.LineTooltip);
        }
    }

    /// <summary>The giver's zone and, when there is a giver, Teleport (through Lifestream; greyed with the reason when it cannot).</summary>
    private void DrawBoardWhere(PlanningSource.BoardLine line)
    {
        // A daily carried over the reset (1.19.0, C5): Flag its giver, where it is turned in, and Teleport there (Flag
        // at every automation level, Teleport from Travel).
        if (line.Row.Carried is { } carried && Links is { } carriedLinks)
        {
            if (TravelControls.FlagButton(Strings.AlliedFlag, carriedLinks.CanFlagMap(carried), "##carriedFlag"))
            {
                carriedLinks.FlagMap(carried);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(Strings.AlliedFlagTooltip);
            }

            if (carriedLinks.TeleportShown)
            {
                Chrome.SameLineOrWrap(Chrome.ActionPillWidth(ActionIcons.TeleportIcon, Strings.PlanningBoardTeleport, PillLayout.Row));
                TravelControls.TeleportButton(carriedLinks, carried, Strings.PlanningBoardTeleport);
            }

            return;
        }

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
