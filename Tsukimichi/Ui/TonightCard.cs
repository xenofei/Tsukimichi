using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Todo;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail column when no quest is selected (game UX panel finding 1): a "Tonight" card that answers "what can I do
/// now" before the catalog does. How many quests are Ready, with a button that opens the Journal under the Available
/// now filter; the next main scenario quest with its blocker; the seasonal events running now on one line; and up to
/// three pinned quests that are Ready. Rows come from the Todo overlay's model (<see cref="TodoList"/>), rebuilt when
/// the session version, the pins or the catalog change, so drawing allocates nothing. Every line is a focusable item
/// that selects its quest.
/// </summary>
public sealed class TonightCard
{
    public const int MaxPinned = 3;

    private static readonly string TonightIcon = FontAwesomeIcon.Moon.ToIconString();

    private readonly UiState ui;
    private readonly QueryRunner runner;
    private readonly Action filtersChanged;

    // Memo keys.
    private int builtVersion = -1;
    private int builtPins = -1;
    private int builtReady = -1;
    private CatalogBundle? builtBundle;

    // Job ladders are only needed because TodoInputs asks for one; built once per catalog.
    private CatalogBundle? ladderBundle;
    private JobLadder? ladder;

    private bool hasSnapshot;
    private string readyText = Strings.TonightReadyNone;
    private int ready;
    private TodoRow? msq;
    private string? events;
    private readonly List<TodoRow> pinned = [];

    /// <param name="filtersChanged">Persists and re-runs the query after the card changed <see cref="UiState.Filters"/>.</param>
    public TonightCard(UiState ui, QueryRunner runner, Action filtersChanged)
    {
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.filtersChanged = filtersChanged ?? throw new ArgumentNullException(nameof(filtersChanged));
    }

    public void Draw(SessionState session, CatalogBundle bundle, Vector2 size)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(bundle);
        using var colors = Theme.PushNightPanel();
        using var child = ImRaii.Child("##detail", size, true);
        if (!child)
        {
            return;
        }

        ui.RecordWindow(UiRects.Detail);
        Refresh(session, bundle);

        Chrome.BeginCard("##tonight", Strings.TonightTitle, TonightIcon);
        if (!hasSnapshot)
        {
            using var mist = Theme.PushText(Theme.Surface.TextSecondary);
            ImGui.TextWrapped(Strings.TonightLogIn);
        }
        else
        {
            DrawReady();
            DrawMsq(bundle);
            if (events is not null)
            {
                Chrome.Hairline();
                using var mist = Theme.PushText(Theme.Surface.TextSecondary);
                ImGui.TextWrapped(events);
            }

            DrawPinned(bundle);
        }

        Chrome.EndCard();
        ImGui.Spacing();
        using (Theme.PushText(Theme.Surface.TextTertiary))
        {
            ImGui.TextWrapped(Strings.TonightPickHint);
        }
    }

    private void DrawReady()
    {
        using (Theme.PushText(ready > 0 ? Theme.Surface.Text : Theme.Surface.TextSecondary))
        {
            ImGui.TextWrapped(readyText);
        }

        if (ready == 0)
        {
            return;
        }

        if (ImGui.Button(Strings.TonightShowReady))
        {
            // The whole journal, narrowed to what can be picked up now; its chip clears it.
            ui.Tab = NavTab.Journal;
            ui.Scope = QuestScope.None;
            ui.Filters.AvailableOnly = true;
            filtersChanged();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.TonightShowReadyTooltip);
        }
    }

    private void DrawMsq(CatalogBundle bundle)
    {
        Chrome.Hairline();
        using (Theme.PushText(Theme.Surface.TextTertiary))
        {
            ImGui.TextUnformatted(Strings.TonightMsqLabel);
        }

        if (msq is null)
        {
            using var dim = Theme.PushText(Theme.MoonDim);
            ImGui.TextUnformatted(Strings.TonightMsqDone);
            return;
        }

        Row(bundle, msq, 0);
    }

    private void DrawPinned(CatalogBundle bundle)
    {
        if (pinned.Count == 0)
        {
            return;
        }

        Chrome.Hairline();
        using (Theme.PushText(Theme.Surface.TextTertiary))
        {
            ImGui.TextUnformatted(Strings.TonightPinnedTitle);
        }

        for (var i = 0; i < pinned.Count; i++)
        {
            Row(bundle, pinned[i], i + 1);
        }
    }

    /// <summary>A quest line: its moon, its name as a selectable (click or Enter selects it), and its hint under it.</summary>
    private void Row(CatalogBundle bundle, TodoRow row, int id)
    {
        var lineHeight = ImGui.GetTextLineHeight();
        var glyph = UiMetrics.InlineGlyphSize(lineHeight);
        ImGui.PushID(id);
        MoonGlyph.DrawInline(row.State, glyph);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.StateTooltip(row.State));
        }

        ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((glyph - lineHeight) * 0.5f));
        if (ImGui.Selectable(row.Name))
        {
            if (bundle.Catalog.GetByRowId(row.RowId) is { } quest)
            {
                ui.Reveal(quest);
            }
            else
            {
                ui.SelectedRowId = row.RowId;
            }
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.TonightRowTooltip);
        }

        ImGui.PopID();
        if (row.Hint.Length > 0)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + glyph + ImGui.GetStyle().ItemSpacing.X);
            using var mist = Theme.PushText(Theme.Surface.TextSecondary);
            ImGui.TextWrapped(row.Hint);
        }
    }

    private void Refresh(SessionState session, CatalogBundle bundle)
    {
        var readyNow = runner.Counts?.OverallReady ?? 0;
        if (builtVersion == session.Version && builtPins == runner.PinsVersion && ReferenceEquals(builtBundle, bundle) && builtReady == readyNow)
        {
            return;
        }

        builtVersion = session.Version;
        builtPins = runner.PinsVersion;
        builtBundle = bundle;
        builtReady = readyNow;
        ready = readyNow;
        readyText = readyNow switch
        {
            0 => Strings.TonightReadyNone,
            1 => Strings.TonightReadyOne,
            _ => string.Format(CultureInfo.CurrentCulture, Strings.TonightReadyFormat, readyNow),
        };

        msq = null;
        events = null;
        pinned.Clear();
        hasSnapshot = session.ViewedSnapshot is not null;
        if (session.ViewedSnapshot is not { } snapshot)
        {
            return;
        }

        if (!ReferenceEquals(ladderBundle, bundle))
        {
            ladderBundle = bundle;
            ladder = bundle.BuildJobLadder();
        }

        var model = TodoList.Build(new TodoInputs(
            bundle.Catalog,
            session.States,
            runner.Pinned,
            session.FeatureQuestIds,
            0,
            snapshot.CurrentJob,
            snapshot.JobLevels,
            ladder!,
            bundle.Names.ClassJobAbbreviations,
            ShowPins: true,
            ShowNearbyFeature: false,
            ShowMsq: true,
            ShowJobQuests: false,
            Names: session.Names));
        foreach (var section in model.Sections)
        {
            switch (section.Section)
            {
                case TodoSection.Msq when section.Rows.Count > 0:
                    msq = section.Rows[0];
                    break;
                case TodoSection.Pinned:
                    foreach (var row in section.Rows)
                    {
                        if (row.State == QuestState.Ready && pinned.Count < MaxPinned)
                        {
                            pinned.Add(row);
                        }
                    }

                    break;
            }
        }

        events = EventsLine(session, bundle, snapshot);
    }

    /// <summary>"Events now: Starlight Celebration (3 ready)" from the festivals the client reports running; null when none is.</summary>
    private static string? EventsLine(SessionState session, CatalogBundle bundle, CharacterSnapshot snapshot)
    {
        if (snapshot.ActiveFestivals.Count == 0)
        {
            return null;
        }

        var readyByFestival = new Dictionary<ushort, int>();
        foreach (var quest in bundle.Catalog.All)
        {
            if (quest.Festival != 0 && session.States.TryGetValue(quest.RowId, out var evaluation) && evaluation.State == QuestState.Ready)
            {
                readyByFestival[quest.Festival] = readyByFestival.GetValueOrDefault(quest.Festival) + 1;
            }
        }

        var text = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in snapshot.ActiveFestivals)
        {
            var name = session.Curated.Festivals.TryGetValue(id, out var info) && info.Name.Length > 0 ? info.Name : Strings.TonightEventFallback;
            var count = readyByFestival.GetValueOrDefault(id);

            // Festivals the client runs without any quest of ours (and unnamed ones) stay out of the line.
            if ((count == 0 && !session.Curated.Festivals.ContainsKey(id)) || !seen.Add(name))
            {
                continue;
            }

            if (text.Length > 0)
            {
                text.Append(Strings.StateReasonSeparator);
            }

            text.Append(count > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.TonightEventReadyFormat, name, count) : name);
        }

        return text.Length == 0 ? null : string.Format(CultureInfo.CurrentCulture, Strings.TonightEventsFormat, text);
    }
}
