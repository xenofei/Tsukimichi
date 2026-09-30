using System;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Seasonal;

namespace Tsukimichi.Ui;

/// <summary>
/// The "Seasonal events" section of the Characters dashboard (P11): the events the game reports as running for the
/// viewed character (for a stored one, the ones running at its last capture), each with "announced to end Aug 28
/// (Lodestone)" when curated data has the date and "running now" otherwise, and its quests with their moon, state and
/// giver (click reveals one in the Journal); then "Completed seasonal quests by year", the character's seasonal history
/// grouped by <see cref="SeasonalNow.EditionYears"/>. Built from <see cref="SeasonalNow"/> once per session version and
/// once a minute (an end date can pass), never per frame.
/// </summary>
public sealed partial class CharactersPane
{
    private SeasonalView seasonalView = SeasonalView.Empty;
    private int seasonalVersion = -1;
    private long seasonalMinute = -1;

    private void DrawSeasonal(UiState ui)
    {
        RefreshSeasonal();
        var view = seasonalView;
        using var id = ImRaii.PushId("seasonal");
        if (!ImGui.CollapsingHeader(view.Header, ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        if (view.CaptureNote.Length > 0)
        {
            ImGui.TextDisabled(view.CaptureNote);
        }

        if (view.Running.Length == 0)
        {
            ImGui.TextDisabled(Strings.SeasonalNoneRunning);
        }

        for (var f = 0; f < view.Running.Length; f++)
        {
            using var festivalId = ImRaii.PushId(f);
            DrawRunningFestival(ui, view.Running[f]);
        }

        ImGui.Spacing();
        DrawSeasonalHistory(ui, view);
    }

    private void DrawRunningFestival(UiState ui, SeasonalFestivalView festival)
    {
        using (Theme.PushText(Theme.Silver))
        {
            ImGui.TextUnformatted(festival.Name);
        }

        ImGui.SameLine();
        ImGui.TextDisabled(festival.Status);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(festival.StatusTooltip);
        }

        if (festival.Rows.Length == 0)
        {
            ImGui.TextDisabled(Strings.SeasonalNoQuests);
            return;
        }

        using var table = ImRaii.Table("##seasonalQuests", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        var line = ImGui.GetTextLineHeight();
        ImGui.TableSetupColumn("##state", ImGuiTableColumnFlags.WidthFixed, line * 1.4f);
        ImGui.TableSetupColumn(Strings.CharactersColumnQuest, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(260f));
        ImGui.TableSetupColumn(Strings.CharactersColumnStatus, ImGuiTableColumnFlags.WidthStretch);

        for (var i = 0; i < festival.Rows.Length; i++)
        {
            var row = festival.Rows[i];
            using var rowId = ImRaii.PushId(i);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            MoonGlyph.DrawInline(row.State, UiMetrics.InlineGlyphSize(line));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.StateTooltip(row.State, row.Quest));
            }

            ImGui.TableNextColumn();
            if (ImGui.Selectable(row.Name))
            {
                Reveal(ui, row.Quest);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(row.Tooltip);
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Detail);
        }
    }

    private void DrawSeasonalHistory(UiState ui, SeasonalView view)
    {
        using var node = ImRaii.TreeNode(view.HistoryLabel);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.SeasonalHistoryTooltip);
        }

        if (!node)
        {
            return;
        }

        if (view.History.Length == 0)
        {
            ImGui.TextDisabled(Strings.SeasonalHistoryNone);
            return;
        }

        for (var y = 0; y < view.History.Length; y++)
        {
            var year = view.History[y];
            using var yearNode = ImRaii.TreeNode(year.Label);
            if (!yearNode)
            {
                continue;
            }

            for (var f = 0; f < year.Festivals.Length; f++)
            {
                var festival = year.Festivals[f];
                using var festivalId = ImRaii.PushId(f);
                ImGui.TextDisabled(festival.Label);
                using var indent = ImRaii.PushIndent();
                for (var q = 0; q < festival.Quests.Length; q++)
                {
                    var (quest, name) = festival.Quests[q];
                    using var questId = ImRaii.PushId(q);
                    if (ImGui.Selectable(name))
                    {
                        Reveal(ui, quest);
                    }
                }
            }
        }
    }

    /// <summary>Rebuilds the section's rows when the session changed or a minute passed.</summary>
    private void RefreshSeasonal()
    {
        var now = DateTime.UtcNow;
        var minute = now.Ticks / TimeSpan.TicksPerMinute;
        if (seasonalVersion == session.Version && seasonalMinute == minute)
        {
            return;
        }

        seasonalVersion = session.Version;
        seasonalMinute = minute;
        if (session.Bundle is not { } bundle || session.ViewedSnapshot is not { } snapshot)
        {
            seasonalView = SeasonalView.Empty;
            return;
        }

        var catalog = bundle.Catalog;
        var curated = session.Curated.Festivals;
        var running = SeasonalNow.Running(catalog, session.ServerFestivals, session.States, curated, now);
        var festivals = new SeasonalFestivalView[running.Count];
        for (var i = 0; i < running.Count; i++)
        {
            var festival = running[i];
            var rows = new SeasonalRowView[festival.Quests.Count];
            for (var r = 0; r < rows.Length; r++)
            {
                var (quest, state) = festival.Quests[r];
                var name = session.Spoilers.DisplayName(quest);
                var tooltip = quest.Issuer is { Name.Length: > 0 } issuer ? Strings.SeasonalGiverPrefix + issuer.Name + "\n" + Strings.MsqClickHint : Strings.MsqClickHint;
                rows[r] = new SeasonalRowView(quest, name, state, SeasonalDetail(quest, state), tooltip);
            }

            var status = SeasonalNow.Status(festival, now);
            var statusTooltip = festival.EndEvidence is { } evidence ? Strings.SeasonalEvidenceTooltipPrefix + evidence : Strings.SeasonalRunningNowTooltip;
            festivals[i] = new SeasonalFestivalView(festival.Name, status, statusTooltip, rows);
        }

        var history = SeasonalNow.History(catalog, snapshot, curated);
        var years = new SeasonalYearView[history.Count];
        var completed = 0;
        for (var y = 0; y < history.Count; y++)
        {
            var year = history[y];
            completed += year.QuestCount;
            var entries = new SeasonalHistoryFestivalView[year.Festivals.Count];
            for (var f = 0; f < entries.Length; f++)
            {
                var festival = year.Festivals[f];
                var quests = new (QuestRecord, string)[festival.Quests.Count];
                for (var q = 0; q < quests.Length; q++)
                {
                    quests[q] = (festival.Quests[q], session.Spoilers.DisplayName(festival.Quests[q]));
                }

                entries[f] = new SeasonalHistoryFestivalView(
                    string.Format(CultureInfo.CurrentCulture, Strings.SeasonalHistoryFestivalFormat, festival.Name, quests.Length),
                    quests);
            }

            var yearName = year.Year is { } known ? known.ToString(CultureInfo.InvariantCulture) : Strings.SeasonalYearUnknown;
            var label = string.Format(CultureInfo.CurrentCulture, Strings.SeasonalYearFormat, yearName, year.QuestCount)
                        + "###seasonalYear" + (year.Year ?? 0).ToString(CultureInfo.InvariantCulture);
            years[y] = new SeasonalYearView(label, entries);
        }

        var header = (festivals.Length == 0
            ? Strings.SeasonalHeader
            : string.Format(CultureInfo.CurrentCulture, Strings.SeasonalHeaderFormat, festivals.Length)) + "###seasonalHeader";
        var captureNote = session.IsLive
            ? string.Empty
            : string.Format(CultureInfo.CurrentCulture, Strings.SeasonalAtCaptureFormat, snapshot.TakenUtc.ToLocalTime().ToString(Strings.DateTimeFormat, CultureInfo.CurrentCulture));
        var historyLabel = string.Format(CultureInfo.CurrentCulture, Strings.SeasonalHistoryFormat, completed) + "###seasonalHistory";
        seasonalView = new SeasonalView(header, captureNote, festivals, historyLabel, years);
    }

    /// <summary>
    /// The Status column: the state name, then what follows it in the Journal (the decisive blocker, the step in the
    /// journal); the giver is in the name's tooltip.
    /// </summary>
    private string SeasonalDetail(QuestRecord quest, QuestState state)
    {
        var name = Strings.StateName(state, quest);
        if (!session.States.TryGetValue(quest.RowId, out var evaluation))
        {
            return name;
        }

        var reason = state switch
        {
            QuestState.Accepted => BlockerText.StepText(evaluation.Sequence, quest.StepCount),
            QuestState.Blocked or QuestState.Unknown => BlockerText.For(evaluation, quest, session.Names, session.States),
            _ => string.Empty,
        };

        return reason.Length > 0 ? name + Strings.StateReasonSeparator + reason : name;
    }

    private sealed record SeasonalRowView(QuestRecord Quest, string Name, QuestState State, string Detail, string Tooltip);

    private sealed record SeasonalFestivalView(string Name, string Status, string StatusTooltip, SeasonalRowView[] Rows);

    private sealed record SeasonalHistoryFestivalView(string Label, (QuestRecord Quest, string Name)[] Quests);

    private sealed record SeasonalYearView(string Label, SeasonalHistoryFestivalView[] Festivals);

    private sealed record SeasonalView(string Header, string CaptureNote, SeasonalFestivalView[] Running, string HistoryLabel, SeasonalYearView[] History)
    {
        public static readonly SeasonalView Empty = new(
            Strings.SeasonalHeader + "###seasonalHeader",
            string.Empty,
            [],
            string.Format(CultureInfo.CurrentCulture, Strings.SeasonalHistoryFormat, 0) + "###seasonalHistory",
            []);
    }
}
