using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The achievements that need several quests, under Story chains on the Characters dashboard (1.9.0 collector extras,
/// feature plan v5; R5 F6, C5 #7): each one a chain-like row with a filling moon, "3/5" and the next quest to do,
/// "Earned" once the game (or every quest) says so, and the quests still to do in the name's tooltip. The progress
/// reads the viewed character's completion bits, so a stored character's rows are as exact as the live one's; the
/// earned flag is the game's own when its achievement list was loaded. Rows are built when the session, the viewed
/// character or the game's flag change, not per frame. Kept in its own file with one call line in
/// <see cref="DrawMain"/>.
/// </summary>
public sealed partial class CharactersPane
{
    private const uint LadderGauges = 0x8_0000;

    /// <summary>How often the rows re-read the game's achievement flag (it changes when the Achievements window loads).</summary>
    private const double LadderRefreshSeconds = 5.0;

    private static readonly FixedWidth LaddersDoneWidth = new();

    /// <summary>The game's achievement flag for the viewed character (<c>RewardUnlockReader.AchievementEarned</c>); null lets the quests decide.</summary>
    public Func<uint, bool?>? AchievementEarned { get; set; }

    private sealed record AchievementRow(string Name, float Fraction, string Count, QuestRecord? Next, string NextText, bool Ready, string Tooltip, IReadOnlyList<uint> RowIds);

    private (int Version, ulong? Viewed, CatalogBundle? Bundle, long Tick, int Language) laddersKey = (-1, null, null, -1, -1);
    private AchievementRow[] ladderRows = [];

    private void DrawAchievementLadders(UiState ui)
    {
        RefreshLadders();
        if (ladderRows.Length == 0)
        {
            return;
        }

        ImGui.Spacing();
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextUnformatted(Strings.LaddersHeading);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.LaddersHeadingTooltip);
        }

        using var table = ImRaii.Table("##achievementLadders", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        if (LaddersDoneWidth.Stale(ladderRows))
        {
            var done = FixedWidth.Fit(0f, Strings.JobsColumnDone);
            foreach (var row in ladderRows)
            {
                done = FixedWidth.Fit(done, row.Count);
            }

            LaddersDoneWidth.Store(ladderRows, done);
        }

        var line = ImGui.GetTextLineHeight();
        ImGui.TableSetupColumn("##moon", ImGuiTableColumnFlags.WidthFixed, GlyphColumn(line));
        ImGui.TableSetupColumn(Strings.LaddersColumnAchievement, ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn(Strings.JobsColumnDone, ImGuiTableColumnFlags.WidthFixed, LaddersDoneWidth.Value);
        ImGui.TableSetupColumn(Strings.JobsColumnNext, ImGuiTableColumnFlags.WidthStretch, 3f);

        for (var i = 0; i < ladderRows.Length; i++)
        {
            var row = ladderRows[i];
            using var rowId = ImRaii.PushId(i);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            MoonGlyph.DrawHaloInline(Motion.Key(DashboardGaugeTag, LadderGauges | (uint)i), row.Fraction, UiMetrics.InlineGlyphSize(line));
            if (ImGui.IsItemHovered())
            {
                FillingMoonTooltip(row.Count);
            }

            ImGui.TableNextColumn();
            Chrome.FitText(row.Name, ImGui.GetColorU32(ImGuiCol.Text));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(row.Name, row.Tooltip);
            }

            DrawRowMenu(ui, row.RowIds);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Count);
            ImGui.TableNextColumn();
            DrawNextQuest(ui, row.Next, row.NextText, row.Ready);
        }
    }

    private void RefreshLadders()
    {
        var bundle = session.Bundle;
        var snapshot = session.ViewedSnapshot;
        var key = (session.Version, snapshot?.ContentId, bundle, (long)(ImGui.GetTime() / LadderRefreshSeconds), Localization.Loc.Version);
        if (key == laddersKey)
        {
            return;
        }

        laddersKey = key;
        if (bundle is null || snapshot is null || bundle.AchievementLadders.Count == 0)
        {
            ladderRows = [];
            return;
        }

        bool Done(uint rowId) => snapshot.IsCompleted(QuestRecord.ToQuestId(rowId));
        var rows = new List<AchievementRow>(bundle.AchievementLadders.Count);
        var tooltip = new StringBuilder();
        foreach (var ladder in bundle.AchievementLadders.All)
        {
            var progress = AchievementLadders.Progress(ladder, Done, AchievementEarned?.Invoke(ladder.AchievementId));
            var next = progress.Remaining.Count > 0 ? bundle.Catalog.GetByRowId(progress.Remaining[0]) : null;
            tooltip.Clear();
            tooltip.Append(progress.Earned ? Strings.LadderEarned : Strings.LadderNotEarned);
            if (progress.Remaining.Count > 0)
            {
                tooltip.Append('\n').Append(Strings.LadderStillToDo);
                foreach (var rowId in progress.Remaining)
                {
                    tooltip.Append("\n· ").Append(session.Spoilers.DisplayName(bundle.Catalog, rowId, rowId.ToString(CultureInfo.InvariantCulture)));
                }
            }

            rows.Add(new AchievementRow(
                ladder.Name,
                progress.Fraction,
                string.Format(CultureInfo.CurrentCulture, Strings.JobsChainCountFormat, progress.Done, progress.Total),
                next,
                next is null
                    ? progress.Earned ? Strings.LadderEarned : Strings.JobsChainComplete
                    : string.Format(CultureInfo.CurrentCulture, Strings.JobsChainNextFormat, session.Spoilers.DisplayName(next)),
                next is not null && session.States.TryGetValue(next.RowId, out var evaluation)
                    && evaluation.State is QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Accepted,
                tooltip.ToString(),
                ladder.RowIds));
        }

        ladderRows = rows.ToArray();
    }
}
