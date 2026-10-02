using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Return;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The "Since you were away" card (feature plan v3 P7) at the top of the detail column, where the What's-new card
/// sits (which goes first when both are due): what the character was doing, where the main scenario stands then and
/// now (route by route, names through the spoiler shield), the quests the game added since, the events running now
/// and the levels that moved, each section a Chrome card with its "Show in Journal" link; or, with no capture to
/// compare with, "When did you last play?" with a patch picker. Close hides it; Don't show again keeps it from opening
/// on its own for that character. <see cref="WelcomeBackSource"/> decides when it opens and computes the summary; the
/// strings are built when the summary, the shield or the catalog change, not per frame.
/// </summary>
public sealed class WelcomeBackCard
{
    /// <summary>The card never takes more than this share of the column, and scrolls past it.</summary>
    private const float MaxHeightFraction = 0.55f;
    private const float MaxHeightPx = 460f;
    private const float Pad = 8f;
    private const int MaxJournalRows = 8;
    private static readonly Localization.LocText MsqShowLabel = new(static () => Strings.WelcomeBackShowInJournal + "##msq");

    private static readonly string JournalIcon = Chrome.Icon(FontAwesomeIcon.Book);
    private static readonly string MsqIcon = Chrome.Icon(FontAwesomeIcon.Moon);
    private static readonly string NewIcon = Chrome.Icon(FontAwesomeIcon.Star);
    private static readonly string EventIcon = Chrome.Icon(FontAwesomeIcon.CalendarAlt);
    private static readonly string LevelIcon = Chrome.Icon(FontAwesomeIcon.ArrowUp);

    private readonly WelcomeBackSource source;
    private readonly SessionState session;
    private readonly UiState ui;
    private readonly Action filtersChanged;

    // Memo keys and the strings built from them.
    private WelcomeBackView? builtView;
    private int builtShield = int.MinValue;
    private CatalogBundle? builtBundle;

    private string title = Strings.WelcomeBackTitle;
    private readonly List<string> header = [];
    private bool showChange;
    private string journalTitle = string.Empty;
    private string journalEmpty = string.Empty;
    private readonly List<(QuestRecord Quest, string Name, string Status)> journalRows = [];
    private string journalMore = string.Empty;
    private string msqThen = string.Empty;
    private readonly List<string> msqNow = [];
    private string msqDoneSince = string.Empty;
    private QuestRecord? msqNext;
    private string newTitle = string.Empty;
    private string newTotal = string.Empty;
    private readonly List<(string Series, string Text, string Tooltip)> newRows = [];
    private bool newUnknown;
    private readonly List<string> events = [];
    private string levels = string.Empty;

    // The question's choice: -1 none yet, 0 … n-1 a series from the picker; its labels, built per series list.
    private int picked = -1;
    private ulong pickedFor;
    private IReadOnlyList<PatchSeries>? labelsFor;
    private string[] seriesLabels = [];

    /// <param name="filtersChanged">Persists and re-runs the query after a "Show in Journal" changed <see cref="UiState.Filters"/>.</param>
    public WelcomeBackCard(WelcomeBackSource source, SessionState session, UiState ui, Action filtersChanged)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
        this.filtersChanged = filtersChanged ?? throw new ArgumentNullException(nameof(filtersChanged));
    }

    /// <summary>True while the card has something to show. Takes a finished summary first.</summary>
    public bool Visible
    {
        get
        {
            source.Poll();
            return source.View is not null;
        }
    }

    /// <summary>Draws the card when visible and returns the height it used (0 when hidden), so the caller can shrink the pane below it.</summary>
    public float Draw(float availableHeight, CatalogBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (source.View is not { } view)
        {
            return 0f;
        }

        var pad = UiMetrics.Px(Pad);
        var line = ImGui.GetTextLineHeightWithSpacing();
        var height = view.Asking ? (line * 9f) + (2f * pad)
            : view.Summary is null ? (line * 3f) + (2f * pad)
            : MathF.Min(availableHeight * MaxHeightFraction, UiMetrics.Px(MaxHeightPx));
        height = MathF.Min(height, availableHeight * MaxHeightFraction);
        var start = ImGui.GetCursorPosY();

        using (Theme.PushNightPanel())
        using (ImRaii.PushColor(ImGuiCol.Border, Chrome.CardChildBorder))
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(pad, pad)))
        using (var child = ImRaii.Child("##welcomeBack", new Vector2(0f, height), true))
        {
            if (child)
            {
                // Brass at Full and Quiet, the corner marks at Full (R3 #8).
                using var frame = Chrome.CardFrameInWindow();
                Refresh(view, bundle);
                DrawBody(view);
            }
        }

        ImGui.Spacing();
        return ImGui.GetCursorPosY() - start;
    }

    private void DrawBody(WelcomeBackView view)
    {
        // The title, then its two buttons at the right end of the line, or right-aligned on the next line when they
        // would run into it (feature plan v4 L6).
        Chrome.FitText(title, Theme.U32(Theme.Moon));
        var closeWidth = ImGuiHelpers.GetButtonSize(Strings.WelcomeBackClose).X;
        var quietWidth = ImGuiHelpers.GetButtonSize(Strings.WelcomeBackDontShow).X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        Chrome.SameLineRightOrWrap(closeWidth + quietWidth + spacing);
        if (ImGui.SmallButton(Strings.WelcomeBackDontShow))
        {
            source.DontShowAgain();
            return;
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.WelcomeBackDontShowTooltip);
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(Strings.WelcomeBackClose))
        {
            source.Close();
            return;
        }

        ImGui.Spacing();
        using var wrap = ImRaii.TextWrapPos(ImGui.GetWindowContentRegionMax().X);
        if (view.Asking)
        {
            DrawQuestion(view);
            return;
        }

        if (view.Summary is null)
        {
            using var mist = Theme.PushText(Theme.Surface.TextSecondary);
            ImGui.TextUnformatted(Strings.WelcomeBackReading);
            return;
        }

        DrawHeader();
        DrawJournal();
        DrawMsq();
        DrawNew();
        DrawEvents();
        DrawLevels();
    }

    private void DrawHeader()
    {
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            foreach (var text in header)
            {
                ImGui.TextWrapped(text);
            }
        }

        if (showChange)
        {
            if (ImGui.SmallButton(Strings.WelcomeBackChange))
            {
                source.Ask();
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.WelcomeBackChangeTooltip);
            }
        }

        ImGui.Spacing();
    }

    /// <summary>"When did you last play?": a combo of the patch series the data knows, newest first, then Show or I'm new.</summary>
    private void DrawQuestion(WelcomeBackView view)
    {
        if (pickedFor != view.ContentId)
        {
            pickedFor = view.ContentId;
            picked = -1;
        }

        Chrome.BeginCard("##ask", Strings.WelcomeBackAskTitle, JournalIcon);
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextWrapped(Strings.WelcomeBackAskBody);
        }

        var series = source.PickerSeries;
        if (!ReferenceEquals(labelsFor, series) || labelsLanguage != Localization.Loc.Version)
        {
            labelsLanguage = Localization.Loc.Version;
            labelsFor = series;
            seriesLabels = new string[series.Count];
            for (var i = 0; i < seriesLabels.Length; i++)
            {
                seriesLabels[i] = string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackAskSeriesFormat, series[i].Series, series[i].Quests);
            }
        }

        if (picked >= series.Count)
        {
            picked = -1;
        }

        // The combo shrinks to the card, and each button moves to the next line when it would run past the edge.
        var preview = picked < 0 ? Strings.WelcomeBackAskPick : seriesLabels[picked];
        ImGui.SetNextItemWidth(Chrome.FitWidth(UiMetrics.Px(200f)));
        using (var combo = ImRaii.Combo("##lastPlayed", preview))
        {
            if (combo)
            {
                for (var i = 0; i < series.Count; i++)
                {
                    if (ImGui.Selectable(seriesLabels[i], i == picked))
                    {
                        picked = i;
                    }
                }
            }
        }

        Chrome.SameLineOrWrap(ImGuiHelpers.GetButtonSize(Strings.WelcomeBackAskShow).X);
        using (ImRaii.Disabled(picked < 0))
        {
            if (ImGui.Button(Strings.WelcomeBackAskShow) && picked >= 0)
            {
                source.Answer(series[picked].Series);
            }
        }

        Chrome.SameLineOrWrap(ImGuiHelpers.GetButtonSize(Strings.WelcomeBackAskNew).X);
        if (ImGui.Button(Strings.WelcomeBackAskNew))
        {
            source.Answer(WelcomeBackState.NewPlayer);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.WelcomeBackAskNewTooltip);
        }

        Chrome.EndCard();
    }

    private void DrawJournal()
    {
        Chrome.BeginCard("##journal", journalTitle, JournalIcon);
        if (journalRows.Count == 0)
        {
            ImGui.TextDisabled(journalEmpty);
        }

        for (var i = 0; i < journalRows.Count; i++)
        {
            var (quest, name, status) = journalRows[i];
            using var id = ImRaii.PushId(i);
            if (Chrome.EllipsisSelectable(name, false, 0f, out var cut))
            {
                ui.Reveal(quest);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(cut ? name : Strings.WelcomeBackRowTooltip, cut ? Strings.WelcomeBackRowTooltip : null);
            }

            ImGui.Indent();
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                TextFlow.Wrapped(status, Chrome.RoomX());
            }

            ImGui.Unindent();
        }

        if (journalMore.Length > 0)
        {
            ImGui.TextDisabled(journalMore);
        }

        Chrome.EndCard();
        ImGui.Spacing();
    }

    private void DrawMsq()
    {
        Chrome.BeginCard("##msq", Strings.WelcomeBackMsqTitle, MsqIcon);
        if (msqThen.Length > 0)
        {
            using var mist = Theme.PushText(Theme.Surface.TextSecondary);
            ImGui.TextWrapped(msqThen);
        }

        foreach (var text in msqNow)
        {
            ImGui.TextWrapped(text);
        }

        if (msqDoneSince.Length > 0)
        {
            using var mist = Theme.PushText(Theme.Surface.TextSecondary);
            ImGui.TextWrapped(msqDoneSince);
        }

        if (msqNext is { } next)
        {
            if (ImGui.SmallButton(MsqShowLabel.Value))
            {
                ui.Reveal(next);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.WelcomeBackMsqShowTooltip);
            }
        }

        Chrome.EndCard();
        ImGui.Spacing();
    }

    private void DrawNew()
    {
        Chrome.BeginCard("##new", newTitle, NewIcon);
        if (newUnknown)
        {
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextWrapped(Strings.WelcomeBackNewUnknown);
            }

            if (ImGui.SmallButton(Strings.WelcomeBackNewUnknownPick))
            {
                source.Ask();
            }

            Chrome.EndCard();
            ImGui.Spacing();
            return;
        }

        ImGui.TextWrapped(newTotal);
        for (var i = 0; i < newRows.Count; i++)
        {
            // The line wraps between words and its button follows it, or starts the next line when it would not fit.
            var (series, text, tooltip) = newRows[i];
            using var id = ImRaii.PushId(i);
            TextFlow.Wrapped(text, Chrome.RoomX());
            Chrome.SameLineOrWrap(ImGui.CalcTextSize(Strings.WelcomeBackShowInJournal).X + (ImGui.GetStyle().FramePadding.X * 2f));
            if (ImGui.SmallButton(Strings.WelcomeBackShowInJournal))
            {
                ShowAddedIn(series);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(tooltip);
            }
        }

        if (newRows.Count > 0)
        {
            using var dim = Theme.PushText(Theme.Surface.TextTertiary);
            ImGui.TextWrapped(Strings.WelcomeBackNewNote);
        }

        Chrome.EndCard();
        ImGui.Spacing();
    }

    private void DrawEvents()
    {
        if (events.Count == 0)
        {
            return;
        }

        Chrome.BeginCard("##events", Strings.WelcomeBackEventsTitle, EventIcon);
        foreach (var text in events)
        {
            ImGui.TextWrapped(text);
        }

        Chrome.EndCard();
        ImGui.Spacing();
    }

    private void DrawLevels()
    {
        if (levels.Length == 0)
        {
            return;
        }

        Chrome.BeginCard("##levels", Strings.WelcomeBackLevelsTitle, LevelIcon);
        ImGui.TextWrapped(levels);
        Chrome.EndCard();
    }

    /// <summary>
    /// "Show in Journal" on a series: the whole journal filtered to the quests that series added, with the quick view,
    /// the search and every other narrowing filter cleared (Include removed only widens, so it stays), as the Tonight
    /// card's "Show them" does. The Added in chip clears it.
    /// </summary>
    private void ShowAddedIn(string series)
    {
        ui.Tab = NavTab.Journal;
        ui.Scope = QuestScope.None;
        var includeUnlisted = ui.Filters.IncludeUnlisted;
        ui.Filters.Reset();
        ui.Filters.IncludeUnlisted = includeUnlisted;
        ui.Filters.AddedIn = series;
        ui.SearchText = string.Empty;
        filtersChanged();
    }

    // ------------------------------------------------------------------ model

    private int builtLanguage = -1;
    private int labelsLanguage = -1;

    private void Refresh(WelcomeBackView view, CatalogBundle bundle)
    {
        var shield = Shield(view);
        if (ReferenceEquals(builtView, view) && builtShield == shield.Fingerprint && ReferenceEquals(builtBundle, bundle) && builtLanguage == Localization.Loc.Version)
        {
            return;
        }

        builtLanguage = Localization.Loc.Version;
        builtView = view;
        builtShield = shield.Fingerprint;
        builtBundle = bundle;
        title = view.Name.Length > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackTitleFormat, view.Name) : Strings.WelcomeBackTitle;
        header.Clear();
        journalRows.Clear();
        msqNow.Clear();
        newRows.Clear();
        events.Clear();
        journalMore = msqThen = msqDoneSince = newTotal = levels = string.Empty;
        msqNext = null;
        showChange = false;
        if (view.Summary is not { } summary)
        {
            return;
        }

        Func<QuestRecord, string> name = shield.DisplayName;
        BuildHeader(summary);
        BuildJournal(summary, name);
        BuildMsq(summary, bundle, name);
        BuildNew(summary);
        foreach (var festival in summary.Events)
        {
            events.Add(festival.ReadyCount > 0
                ? string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackEventReadyFormat, festival.Name, festival.ReadyCount)
                : festival.Name);
        }

        var text = new StringBuilder();
        foreach (var change in summary.JobChanges)
        {
            var abbreviation = bundle.Names.ClassJobAbbreviation(change.Job);
            if (abbreviation.Length == 0)
            {
                continue;
            }

            if (text.Length > 0)
            {
                text.Append(MsqText.Separator);
            }

            text.Append(string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackLevelFormat, abbreviation, change.Then, change.Now));
        }

        levels = text.ToString();
    }

    // The shield of the card's character, kept from when it was the viewed one: a stored character's card stays open
    // while another character is viewed, and must not fall back to someone else's shield.
    private ulong keptShieldFor;
    private SpoilerMask? keptShield;

    /// <summary>
    /// The shield of the character the card speaks for: the viewed one's while it is viewed, the logged-in one's for
    /// the logged-in character, else the shield it had when it was last viewed (the card opens only for the viewed or
    /// the logged-in character).
    /// </summary>
    private SpoilerMask Shield(WelcomeBackView view)
    {
        if (view.ContentId == session.ViewedContentId)
        {
            keptShieldFor = view.ContentId;
            keptShield = session.Spoilers;
            return keptShield;
        }

        if (view.ContentId == session.LiveContentId)
        {
            return session.LiveSpoilers;
        }

        return keptShieldFor == view.ContentId && keptShield is { } kept ? kept : session.LiveSpoilers;
    }

    private void BuildHeader(WelcomeBackSummary summary)
    {
        if (summary.PreviousTakenUtc is { } taken && summary.DaysAway is { } days)
        {
            var local = taken.ToLocalTime();
            header.Add(days switch
            {
                0 => Strings.WelcomeBackAwayToday,
                1 => string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackAwayOneDayFormat, local),
                _ => string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackAwayFormat, days, local),
            });
        }

        switch (summary.SinceSource)
        {
            case SincePatchSource.Recorded:
                header.Add(string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackSinceRecordedFormat, summary.SincePatch));
                break;
            case SincePatchSource.Inferred:
                header.Add(string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackSinceInferredFormat, summary.SincePatch));
                break;
            case SincePatchSource.Answer:
                header.Add(string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackSinceAnswerFormat, summary.SincePatch));
                showChange = !summary.HasPrevious;
                break;
        }

        if (summary.NewestPatch.Length > 0)
        {
            header.Add(string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackNewestFormat, summary.NewestPatch));
        }
    }

    private void BuildJournal(WelcomeBackSummary summary, Func<QuestRecord, string> name)
    {
        journalTitle = summary.HasPrevious ? Strings.WelcomeBackJournalTitle : Strings.WelcomeBackJournalNowTitle;
        journalEmpty = summary.HasPrevious ? Strings.WelcomeBackJournalNone : Strings.WelcomeBackJournalNowNone;

        // What moved first (completed, moved on, dropped), then what is still where it was; journal order within.
        var ordered = new List<MidwayQuest>(summary.Midway);
        ordered.Sort(static (a, b) =>
        {
            var byRank = Rank(a.Outcome).CompareTo(Rank(b.Outcome));
            if (byRank != 0)
            {
                return byRank;
            }

            var bySort = a.Quest.Journal.SortKey.CompareTo(b.Quest.Journal.SortKey);
            return bySort != 0 ? bySort : a.Quest.RowId.CompareTo(b.Quest.RowId);
        });
        for (var i = 0; i < ordered.Count && i < MaxJournalRows; i++)
        {
            var row = ordered[i];
            journalRows.Add((row.Quest, name(row.Quest), Status(row)));
        }

        if (ordered.Count > MaxJournalRows)
        {
            journalMore = string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackMoreFormat, ordered.Count - MaxJournalRows);
        }
    }

    /// <summary>What moved sorts first; the journal order breaks ties inside each rank.</summary>
    private static int Rank(MidwayOutcome outcome) => outcome switch
    {
        MidwayOutcome.Completed => 0,
        MidwayOutcome.Moved => 1,
        MidwayOutcome.Dropped => 2,
        _ => 3,
    };

    private static string Status(MidwayQuest row) => row.Outcome switch
    {
        MidwayOutcome.Completed => Strings.WelcomeBackCompleted,
        MidwayOutcome.Dropped => row.ThenStep.Length > 0 ? row.ThenStep + MsqText.Separator + Strings.WelcomeBackDropped : Strings.WelcomeBackDropped,
        MidwayOutcome.Moved => row.ThenStep.Length > 0 && row.NowStep.Length > 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackMovedFormat, row.ThenStep, row.NowStep)
            : Strings.WelcomeBackMoved,
        MidwayOutcome.SameStep => row.ThenStep.Length > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackStillAtFormat, row.ThenStep) : Strings.WelcomeBackStill,
        _ => row.NowStep.Length > 0 ? row.NowStep : Strings.WelcomeBackInJournal,
    };

    private void BuildMsq(WelcomeBackSummary summary, CatalogBundle bundle, Func<QuestRecord, string> name)
    {
        if (summary.MsqThen is { } then)
        {
            msqThen = string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackMsqThenFormat, Position(then, bundle, name));
        }

        if (summary.MsqNow is { } now)
        {
            msqNow.Add(string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackMsqNowFormat, Position(now, bundle, name)));

            // Inside a branch region, one line per route with its next quest.
            if (now.IsBranched)
            {
                msqNow.AddRange(MsqText.Lines(now, name));
            }

            msqNext = now.Next;
        }

        // What is left to reach the latest story (1.9.0, R6 F): counts only, no names.
        if (summary.CatchUp is { IsComplete: false } catchUp)
        {
            msqNow.Add(PlanningSource.CatchUpText(catchUp));
        }

        var done = summary.MsqDoneSince;
        if (done > 0)
        {
            msqDoneSince = done == 1 ? Strings.WelcomeBackMsqDoneSinceOne : string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackMsqDoneSinceFormat, done);
        }
    }

    private static string Position(MsqPosition position, CatalogBundle bundle, Func<QuestRecord, string> name)
    {
        if (position.Next is not { } next)
        {
            return Strings.WelcomeBackMsqComplete;
        }

        var expansion = bundle.Names.Expansion(next.Expansion) is { Length: > 0 } named ? named : Expansions.Name(next.Expansion);
        return position.IsBranched
            ? string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackMsqRoutesFormat, expansion, MsqText.Spelled(position, name))
            : string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackMsqAtFormat, expansion, name(next), position.Done, position.Total);
    }

    private void BuildNew(WelcomeBackSummary summary)
    {
        newUnknown = summary.SinceSource == SincePatchSource.None;
        newTitle = summary.SinceSource switch
        {
            SincePatchSource.None => Strings.WelcomeBackNewUnknownPick,
            SincePatchSource.Answer => string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackNewTitleSeriesFormat, summary.SincePatch),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackNewTitleFormat, summary.SincePatch),
        };

        if (newUnknown)
        {
            return;
        }

        newTotal = summary.NewQuestTotal == 0
            ? Strings.WelcomeBackNewNone
            : string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackNewTotalFormat, summary.NewQuestTotal, summary.NewMainScenario, summary.NewUnlocks, summary.NewSide);
        foreach (var series in summary.NewQuests)
        {
            newRows.Add((
                series.Series,
                string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackNewSeriesFormat, series.Series, series.Total, series.MainScenario, series.Unlocks, series.Side),
                string.Format(CultureInfo.CurrentCulture, Strings.WelcomeBackNewShowTooltipFormat, series.Series)));
        }
    }
}
