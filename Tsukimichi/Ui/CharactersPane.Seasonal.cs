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
        DrawSeasonalCalendar(view);
        DrawSeasonalHistory(ui, view);
    }

    private void DrawRunningFestival(UiState ui, SeasonalFestivalView festival)
    {
        Chrome.FitText(festival.Name, Theme.U32(Theme.Surface.Text));
        Chrome.SameLineOrWrap(ImGui.CalcTextSize(festival.Status).X);
        Chrome.FitText(festival.Status, ImGui.GetColorU32(ImGuiCol.TextDisabled));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(festival.StatusTooltip);
        }

        // No end known, or the player's own (1.19.0, C10): "Set end date…" opens a date field.
        if (festival.CanSetEnd)
        {
            DrawSetEndDate(festival);
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
        // The name stretches and ends in an ellipsis; the status keeps its state word (feature plan v4 L6).
        ImGui.TableSetupColumn("##state", ImGuiTableColumnFlags.WidthFixed, GlyphColumn(line));
        ImGui.TableSetupColumn(Strings.CharactersColumnQuest, ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn(Strings.CharactersColumnStatus, ImGuiTableColumnFlags.WidthStretch, 2f);

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
            if (Chrome.EllipsisSelectable(row.Name, false, 0f, out var cut))
            {
                Reveal(ui, row.Quest);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(cut ? row.Name : row.Tooltip, cut ? row.Tooltip : null);
            }

            ImGui.TableNextColumn();
            StatusCell(row.Detail);
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
                    if (Chrome.EllipsisSelectable(name, false, 0f, out var cut))
                    {
                        Reveal(ui, quest);
                    }

                    if (cut && ImGui.IsItemHovered())
                    {
                        UiMetrics.Tooltip(name);
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
        var running = SeasonalNow.Running(catalog, session.ServerFestivals, session.States, curated, now, session.EnteredFestivalEnds);
        var festivals = new SeasonalFestivalView[running.Count];
        for (var i = 0; i < running.Count; i++)
        {
            var festival = running[i];
            var rows = new SeasonalRowView[festival.Quests.Count];
            for (var r = 0; r < rows.Length; r++)
            {
                var (quest, state) = festival.Quests[r];
                var name = session.Spoilers.DisplayName(quest);
                var tooltip = quest.Issuer is { Name.Length: > 0 } ? string.Format(CultureInfo.CurrentCulture, Strings.SeasonalGiverFormat, GiverPortraits.Name(quest, session.Spoilers)) + "\n" + Strings.MsqClickHint : Strings.MsqClickHint;
                rows[r] = new SeasonalRowView(quest, name, state, SeasonalDetail(quest, state), tooltip);
            }

            var status = festival.EndSource == FestivalEndSource.None ? Strings.SeasonalEndNotAnnounced : SeasonalNow.Status(festival, now);
            var statusTooltip = festival.EndSource switch
            {
                FestivalEndSource.Entered => Strings.SeasonalEnteredTooltip,
                _ when festival.EndEvidence is { } evidence => string.Format(CultureInfo.CurrentCulture, Strings.SeasonalEvidenceTooltipFormat, evidence),
                _ => Strings.SeasonalRunningNowTooltip,
            };
            festivals[i] = new SeasonalFestivalView(festival.Name, status, statusTooltip, rows)
            {
                Id = festival.FestivalId,
                CanSetEnd = festival.EndSource is FestivalEndSource.None or FestivalEndSource.Entered,
                EnteredUtc = festival.EndSource == FestivalEndSource.Entered ? festival.AnnouncedEndUtc : null,
            };
        }

        var runningNames = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var festival in running)
        {
            runningNames.Add(festival.Name);
        }

        var calendar = new System.Collections.Generic.List<SeasonalCalendarView>();
        foreach (var line in SeasonalCalendar.Events(curated, now))
        {
            if (!runningNames.Contains(line.Name))
            {
                calendar.Add(CalendarView(line, now));
            }
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
        seasonalView = new SeasonalView(header, captureNote, festivals, historyLabel, years) { Calendar = [.. calendar] };
    }

    /// <summary>"ended · usually August · last ran 2026", or "usually March · last ran 2026"; the dated windows on hover.</summary>
    private static SeasonalCalendarView CalendarView(SeasonalEventLine line, DateTime now)
    {
        var month = line.UsualMonth is { } m ? CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m) : string.Empty;
        var year = line.LastStart is { } last ? last.Start.Year.ToString(CultureInfo.CurrentCulture) : string.Empty;
        var text = (line.EndedRecently, year.Length > 0) switch
        {
            (true, true) => string.Format(CultureInfo.CurrentCulture, Strings.SeasonalEndedFormat, month, year),
            (false, true) => string.Format(CultureInfo.CurrentCulture, Strings.SeasonalUsuallyFormat, month, year),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.SeasonalUsuallyOnlyFormat, month),
        };

        var tooltip = new System.Text.StringBuilder(Strings.SeasonalRunsTooltipHeading);
        foreach (var window in line.Windows)
        {
            tooltip.Append('\n').Append(string.Format(
                CultureInfo.CurrentCulture,
                Strings.SeasonalRunFormat,
                window.Start.ToString(Strings.SeasonalRunDateFormat, CultureInfo.CurrentCulture),
                window.End.ToString(Strings.SeasonalRunDateFormat, CultureInfo.CurrentCulture)));
        }

        return new SeasonalCalendarView(line.Name, text, tooltip.ToString());
    }

    /// <summary>
    /// "Set end date…" (1.19.0, C10): a quiet chip that opens a date and time field in local time. Save keeps the date
    /// per event in the configuration and labels it "you entered this" everywhere; Remove takes it out. Curated data
    /// replaces it when an update ships the Lodestone's.
    /// </summary>
    private void DrawSetEndDate(SeasonalFestivalView festival)
    {
        var label = festival.EnteredUtc is null ? Strings.SeasonalSetEndDate : Strings.SeasonalChangeEndDate;
        Chrome.SameLineOrWrap(Chrome.ActionChipWidth(label));
        if (Chrome.ActionChip("##setEnd", label))
        {
            var start = (festival.EnteredUtc ?? DateTime.UtcNow.Date.AddDays(7).AddHours(Core.Runtime.GameResets.DailyHourUtc).AddMinutes(-1)).ToLocalTime();
            endDateText = start.ToString(EndDateFormat, CultureInfo.InvariantCulture);
            endTimeText = start.ToString(Strings.TimeFormat, CultureInfo.InvariantCulture);
            endDateError = false;
            ImGui.OpenPopup("##setEndPopup");
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.SeasonalSetEndDateTooltip);
        }

        using var popup = ImRaii.Popup("##setEndPopup");
        if (!popup)
        {
            return;
        }

        Chrome.SemiboldTextWrapped(festival.Name, Theme.Surface.Text);
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextUnformatted(Strings.SeasonalEndDateLabel);
        }

        ImGui.SetNextItemWidth(UiMetrics.Px(110f));
        ImGui.InputText("##endDate", ref endDateText, 10);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(UiMetrics.Px(60f));
        ImGui.InputText("##endTime", ref endTimeText, 5);
        using (Typography.Caption())
        using (Theme.PushText(endDateError ? Theme.DangerText : Theme.Surface.TextTertiary))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + UiMetrics.Px(300f));
            ImGui.TextUnformatted(endDateError ? Strings.SeasonalEndDateInvalid : Strings.SeasonalEndDateHint);
            ImGui.PopTextWrapPos();
        }

        if (ImGui.Button(Strings.SeasonalEndDateSave))
        {
            if (DateTime.TryParseExact(endDateText.Trim() + " " + endTimeText.Trim(), EndDateFormat + " " + Strings.TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var local)
                && local.ToUniversalTime() > DateTime.UtcNow)
            {
                SetEnteredEnd(festival.Id, local.ToUniversalTime());
                ImGui.CloseCurrentPopup();
            }
            else
            {
                endDateError = true;
            }
        }

        ImGui.SameLine();
        if (festival.EnteredUtc is not null && ImGui.Button(Strings.SeasonalEndDateRemove))
        {
            SetEnteredEnd(festival.Id, null);
            ImGui.CloseCurrentPopup();
        }

        if (festival.EnteredUtc is not null)
        {
            ImGui.SameLine();
        }

        if (ImGui.Button(Strings.SeasonalEndDateCancel))
        {
            ImGui.CloseCurrentPopup();
        }
    }

    /// <summary>Saves (or with null removes) the player's end date for a Festival id and hands the set to the session.</summary>
    private void SetEnteredEnd(ushort festival, DateTime? endUtc)
    {
        if (endUtc is { } end)
        {
            settings.SeasonalEndDates[festival] = end;
        }
        else
        {
            settings.SeasonalEndDates.Remove(festival);
        }

        saveSettings();
        session.SetEnteredFestivalEnds(new System.Collections.Generic.Dictionary<ushort, DateTime>(settings.SeasonalEndDates));
    }

    private const string EndDateFormat = "yyyy-MM-dd";
    private string endDateText = string.Empty;
    private string endTimeText = string.Empty;
    private bool endDateError;

    private void DrawSeasonalCalendar(SeasonalView view)
    {
        if (view.Calendar.Length == 0)
        {
            return;
        }

        using var node = ImRaii.TreeNode(Strings.SeasonalCalendarLabel + "###seasonalCalendar");
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.SeasonalCalendarTooltip);
        }

        if (!node)
        {
            return;
        }

        for (var i = 0; i < view.Calendar.Length; i++)
        {
            var line = view.Calendar[i];
            using var id = ImRaii.PushId(i);
            Chrome.FitText(line.Name, Theme.U32(Theme.Surface.Text));
            Chrome.SameLineOrWrap(ImGui.CalcTextSize(line.Text).X);
            Chrome.FitText(line.Text, Theme.U32(Theme.Surface.TextSecondary));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(line.Name, line.Tooltip);
            }
        }
    }

    private sealed record SeasonalCalendarView(string Name, string Text, string Tooltip);

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

    private sealed record SeasonalFestivalView(string Name, string Status, string StatusTooltip, SeasonalRowView[] Rows)
    {
        /// <summary>The Festival id.</summary>
        public ushort Id { get; init; }

        /// <summary>No end is known, or the player entered it: "Set end date…" shows.</summary>
        public bool CanSetEnd { get; init; }

        /// <summary>The end the player entered, UTC; null when the end is not theirs.</summary>
        public DateTime? EnteredUtc { get; init; }
    }

    private sealed record SeasonalHistoryFestivalView(string Label, (QuestRecord Quest, string Name)[] Quests);

    private sealed record SeasonalYearView(string Label, SeasonalHistoryFestivalView[] Festivals);

    private sealed record SeasonalView(string Header, string CaptureNote, SeasonalFestivalView[] Running, string HistoryLabel, SeasonalYearView[] History)
    {
        /// <summary>The events not running now, with when they usually come and their dated runs (1.19.0, C10).</summary>
        public SeasonalCalendarView[] Calendar { get; init; } = [];

        private static readonly Localization.LocCache<SeasonalView> EmptyView = new(static () => new(
            Strings.SeasonalHeader + "###seasonalHeader",
            string.Empty,
            [],
            string.Format(CultureInfo.CurrentCulture, Strings.SeasonalHistoryFormat, 0) + "###seasonalHistory",
            []));

        /// <summary>No character: the headers alone, in the current language.</summary>
        public static SeasonalView Empty => EmptyView.Value;
    }
}
