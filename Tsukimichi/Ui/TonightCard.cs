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
/// now" before the catalog does. How many quests are Ready, with a button that opens the Journal showing exactly those
/// (All quests, Ready only, no search, no other filter); the next main scenario quest with its blocker (one per route inside a branch region); the seasonal events running now on one line; and up to
/// three pinned quests that are Ready (the first ones in the order they were pinned). Rows come from the Todo overlay's model (<see cref="TodoList"/>), rebuilt when
/// the session version, the pins or the catalog change, so drawing allocates nothing. Every line is a focusable item
/// that selects its quest.
/// </summary>
public sealed partial class TonightCard
{
    public const int MaxPinned = 3;

    /// <summary>ImGui ids of the second and later main scenario rows (one per route), above the pinned rows' 1–3.</summary>
    private const int MsqRowIdBase = 100;

    private static readonly string TonightIcon = FontAwesomeIcon.Moon.ToIconString();

    private readonly UiState ui;
    private readonly QueryRunner runner;
    private readonly Action filtersChanged;

    // Memo keys.
    private int builtVersion = -1;
    private int builtPins = -1;
    private CatalogBundle? builtBundle;

    // Job ladders are only needed because TodoInputs asks for one; built once per catalog.
    private CatalogBundle? ladderBundle;
    private JobLadder? ladder;

    private bool hasSnapshot;
    private string readyText = Strings.TonightReadyNone;
    private int ready;
    // The next main scenario quest; inside a branch region one row per open route, in route order. Empty once complete.
    private readonly List<TodoRow> msq = [];
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
        var appearFrom = BeginAppear();
        Refresh(session, bundle);

        Chrome.BeginCard("##tonight", Strings.TonightTitle, TonightIcon);
        if (!hasSnapshot)
        {
            using var mist = Theme.PushText(Theme.Surface.TextSecondary);
            TextFlow.Wrapped(Strings.TonightLogIn, Chrome.RoomX());
        }
        else
        {
            // Up next (1.21.0 P1) leads the card; the lines under it keep their places.
            DrawUpNext(session, bundle);
            DrawReplayAndEvents(session, bundle);
            DrawReady();
            DrawMsq(bundle);
            DrawPlanning();
            DrawPayoffGates();
            DrawFinale(bundle);
            DrawNewChapters();
            DrawStops();
            if (events is not null)
            {
                Chrome.Hairline();
                using var mist = Theme.PushText(Theme.Surface.TextSecondary);
                TextFlow.Wrapped(events, Chrome.RoomX());
            }

            DrawBeforeEvercold();
            DrawPinned(bundle);
        }

        Chrome.EndCard();
        ImGui.Spacing();
        using (Theme.PushText(Theme.Surface.TextTertiary))
        {
            TextFlow.Wrapped(Strings.TonightPickHint);
        }

        EndAppear(appearFrom);
    }

    private void DrawReady()
    {
        using (Theme.PushText(ready > 0 ? Theme.Surface.Text : Theme.Surface.TextSecondary))
        {
            TextFlow.Wrapped(readyText, Chrome.RoomX());
        }

        if (ready == 0)
        {
            return;
        }

        if (ImGui.Button(Strings.TonightShowReady))
        {
            // Exactly the quests counted above: the whole journal, Ready only, with the quick view, the search and
            // every other narrowing filter cleared (Include removed only widens, so it stays). The state chip clears it.
            ui.Tab = NavTab.Journal;
            ui.Scope = QuestScope.None;
            var includeUnlisted = ui.Filters.IncludeUnlisted;
            ui.Filters.Reset();
            ui.Filters.IncludeUnlisted = includeUnlisted;
            ui.Filters.StateMask = QuestStateMask.Ready;
            ui.SearchText = string.Empty;
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

        if (msq.Count == 0)
        {
            using var dim = Theme.PushText(Theme.AccentDim);
            ImGui.TextUnformatted(Strings.TonightMsqDone);
            return;
        }

        // One row on a linear stretch; one per open route inside a branch region (ids clear of the pinned rows'). The row
        // whose quest Up next already names, on a branch too, says the catch-up instead (1.21.0 P1, spec decision 2).
        for (var i = 0; i < msq.Count; i++)
        {
            Row(bundle, MsqRowShown(msq[i]), i == 0 ? 0 : MsqRowIdBase + i);
        }
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

        // The name stays inside the card, ending in an ellipsis (the tooltip then carries it whole), and the hint
        // under it wraps between words (feature plan v4 L6).
        ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((glyph - lineHeight) * 0.5f));
        if (Chrome.EllipsisSelectable(row.Name, false, 0f, out var cut))
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
            if (cut)
            {
                UiMetrics.Tooltip(row.Name, Strings.TonightRowTooltip);
            }
            else
            {
                UiMetrics.Tooltip(Strings.TonightRowTooltip);
            }
        }

        ImGui.PopID();
        if (row.Hint.Length > 0)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + glyph + ImGui.GetStyle().ItemSpacing.X);
            using var mist = Theme.PushText(Theme.Surface.TextSecondary);
            TextFlow.Wrapped(row.Hint, Chrome.RoomX());
        }
    }

    private void Refresh(SessionState session, CatalogBundle bundle)
    {
        if (builtVersion == session.Version && builtPins == runner.PinsVersion && ReferenceEquals(builtBundle, bundle))
        {
            return;
        }

        builtVersion = session.Version;
        builtPins = runner.PinsVersion;
        builtBundle = bundle;
        var readyNow = CountReady(session, bundle);
        ready = readyNow;
        readyText = readyNow switch
        {
            0 => Strings.TonightReadyNone,
            1 => Strings.TonightReadyOne,
            _ => string.Format(CultureInfo.CurrentCulture, Strings.TonightReadyFormat, readyNow),
        };

        msq.Clear();
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
            runner.PinnedInOrder,
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
            Names: session.Names,
            // Every pin still to do, not the overlay's first few: the first Ready ones in pin order may sit past its cap.
            PinLimit: int.MaxValue));
        foreach (var section in model.Sections)
        {
            switch (section.Section)
            {
                case TodoSection.Msq:
                    msq.AddRange(section.Rows);
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

        events = EventsLine(session, bundle);
    }

    /// <summary>
    /// The Ready quests "Show them" lists: every Ready quest of the catalog, the class intros included (the tree's
    /// Ready badges leave those out, but the Ready-only table shows them). Removed quests are never Ready.
    /// </summary>
    private static int CountReady(SessionState session, CatalogBundle bundle)
    {
        var count = 0;
        foreach (var quest in bundle.Catalog.All)
        {
            if (!quest.IsRemoved && session.States.TryGetValue(quest.RowId, out var evaluation) && evaluation.State == QuestState.Ready)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// "Events now: Starlight Celebration (3 ready)" from the festivals running on the server
    /// (<see cref="SessionState.ServerFestivals"/>: the live character's flags, or a stored one's less the stale ones);
    /// null when none is.
    /// </summary>
    private static string? EventsLine(SessionState session, CatalogBundle bundle)
    {
        var running = session.ServerFestivals.Ids;
        if (running.Count == 0)
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
        foreach (var id in running)
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
