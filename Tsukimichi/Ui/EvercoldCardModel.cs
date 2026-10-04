using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The Before Evercold card (feature plan v7, 1.20.0, N7; spec-1.20 "N7. Before Evercold") for the viewed character,
/// live or stored: one model that Tonight and the Characters dashboard both draw (<see cref="EvercoldCardView"/>).
/// <para>
/// <b>Nothing moves while you look.</b> Which lines the card has and which fold into "Done: …" are decided when the card
/// is built (<see cref="PrepCard.Build"/>): a new character, a new catalog, the flight or duty index arriving, or the card
/// coming back into view after it was not drawn (a quest was selected, another tab was open). While it stays in view, a
/// new snapshot or a tick only changes the checks (<see cref="PrepCard.Update"/>): a tick reads "you said so", a line
/// the game says is done reads checked without it.
/// </para>
/// <para>
/// Ticks and hiding are per character, in <see cref="CharacterSettingsBook"/>. × hides the card with the 8 s Undo toast
/// (<see cref="GuardedAction.HideEvercoldCard"/>, <see cref="SafetyTier.None"/>); the dashboard keeps a quiet "Show again"
/// line. The card retires by itself on Evercold's data or its early access day (<see cref="EvercoldPrep.IsRetired"/>).
/// Names go through the spoiler shield. Strings are rebuilt when the card, the shield, the language or the minute
/// changes; drawing allocates nothing. Framework thread only.
/// </para>
/// </summary>
public sealed class EvercoldCardModel
{
    private readonly SessionState session;
    private readonly CharacterSettingsBook characters;

    // When the card was drawn last: a gap means it left the view, so the next draw builds it again.
    private int drawnFrame = -2;
    private (ulong? Viewed, CatalogBundle? Bundle, FlightIndex? Flight, DutyRunIndex? Duties) buildKey;
    private (int Session, int Book) liveKey = (-1, -1);
    private (PrepCard? Card, int Spoilers, int Language, long Minute, bool Live) viewKey;

    private CatalogBundle? ladderBundle;
    private JobLadder ladder = JobLadder.Empty;
    private PrepCard card = PrepCard.Empty;
    private IReadOnlySet<PrepLineKind> ticked = new HashSet<PrepLineKind>();
    private bool active;
    private bool hidden;

    public EvercoldCardModel(SessionState session, CharacterSettingsBook characters)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
    }

    /// <summary>The flight index (the newest expansion's areas); null leaves the flying line out. Set by the plugin.</summary>
    public Func<FlightIndex?>? Flight { get; set; }

    /// <summary>Whether the live character has attuned an aether current; null when it cannot be read. Set by the plugin.</summary>
    public Func<uint, bool?>? Attuned { get; set; }

    /// <summary>The duty index; null leaves the duties line out. Set by the plugin.</summary>
    public Func<DutyRunIndex?>? Duties { get; set; }

    /// <summary>C9's Make room popover (the journal line's button); set by the main window once it exists.</summary>
    public Func<MakeRoomView?>? MakeRoom { get; set; }

    /// <summary>Persists and re-runs the query after "Show them" changed the Journal's filters. Set by the main window.</summary>
    public Action? FiltersChanged { get; set; }

    /// <summary>Opens the Characters tab at the Duties board (N4). Set by the plugin.</summary>
    public Action? ShowDutyBoard { get; set; }

    /// <summary>What a line's button does.</summary>
    internal enum Act : byte
    {
        ShowNext,
        StoryRoute,
        MakeRoom,
        ShowThem,
        DutyBoard,
        FlyingRoute,
    }

    internal sealed record ButtonView(Act Act, string Label, string Tooltip);

    /// <summary>One open line as drawn: its icon, words, hover (why, then "Tick it yourself if …") and buttons.</summary>
    internal sealed record LineView(
        PrepLineKind Kind,
        PillIcon Icon,
        string Label,
        string Detail,
        string Why,
        string Tick,
        ButtonView[] Buttons,
        QuestRecord? Target,
        RouteTarget? Route);

    /// <summary>The character the card is for, by first name ("Michiru").</summary>
    internal string Name { get; private set; } = string.Empty;

    internal string SubTonight { get; private set; } = string.Empty;

    internal string SubDashboard { get; private set; } = string.Empty;

    internal string SubTooltip { get; private set; } = string.Empty;

    internal string Fold { get; private set; } = string.Empty;

    internal string AllDone { get; private set; } = string.Empty;

    internal string HiddenLine { get; private set; } = string.Empty;

    internal LineView[] Lines { get; private set; } = [];

    /// <summary>The open lines' checks, in the order of <see cref="Lines"/>.</summary>
    internal IReadOnlyList<PrepCardLine> Open => card.Open;

    internal bool AllDoneNow => card.AllDone;

    /// <summary>Whether the player hid the card for the viewed character.</summary>
    internal bool Hidden => hidden;

    /// <summary>When the card was last shown again (Undo, Show again), for its rise; never for a new character.</summary>
    internal double ShownAt { get; private set; } = double.NegativeInfinity;

    /// <summary>
    /// Brings the card up to date for this frame: a new build when the character, the data or the view changed, else
    /// the checks from the session and the ticks. False when there is nothing to draw (no character, retired).
    /// </summary>
    internal bool Refresh()
    {
        var frame = ImGui.GetFrameCount();
        var wasInView = drawnFrame >= frame - 1;
        drawnFrame = frame;
        if (session.Bundle is not { } bundle || session.ViewedSnapshot is not { } snapshot || session.ViewedContentId is not { } contentId
            || session.States.Count == 0)
        {
            active = false;
            return false;
        }

        var now = DateTime.UtcNow;
        var launch = EvercoldPrep.Launch(session.Curated);
        if (EvercoldPrep.IsRetired(bundle.Catalog, launch, now))
        {
            active = false;
            return false;
        }

        var flight = Flight?.Invoke();
        var duties = Duties?.Invoke();
        var key = (session.ViewedContentId, bundle, flight, duties);
        var live = (session.Version, characters.Version);
        var rebuild = !active || !wasInView || key != buildKey;
        if (rebuild || live != liveKey)
        {
            liveKey = live;
            var nowHidden = characters.IsCardDismissed(contentId, EvercoldPrep.CardId);
            if (hidden && !nowHidden && active && key.Item1 == buildKey.Viewed)
            {
                // Back from Undo or Show again on the same character: the card rises into place.
                ShownAt = ImGui.GetTime();
            }

            hidden = nowHidden;
            ticked = EvercoldPrep.Ticked(characters.EvercoldTicks(contentId));
            var lines = EvercoldPrep.Lines(Inputs(bundle, snapshot, flight, duties));
            card = rebuild ? PrepCard.Build(lines, ticked) : card.Update(lines, ticked);
            buildKey = key;
        }

        active = true;
        var view = (card, session.Spoilers.Fingerprint, Localization.Loc.Version, now.Ticks / TimeSpan.TicksPerMinute, session.IsLive);
        if (view != viewKey)
        {
            viewKey = view;
            BuildText(bundle, snapshot, launch, now);
        }

        return true;
    }

    /// <summary>Ticks or unticks a line for the viewed character ("you said so").</summary>
    internal void SetTicked(PrepLineKind kind, bool tick)
    {
        if (session.ViewedContentId is { } contentId)
        {
            characters.Edit(CharacterSettingChange.EvercoldTick(contentId, EvercoldPrep.TickId(kind), tick));
        }
    }

    /// <summary>× : hides the card for the viewed character, with the Undo toast ("Before Evercold hidden for Michiru · Undo").</summary>
    internal void Hide()
    {
        if (session.ViewedContentId is not { } contentId)
        {
            return;
        }

        characters.Edit(CharacterSettingChange.Dismiss(contentId, EvercoldPrep.CardId, true));
        UndoToast.Show(
            string.Format(CultureInfo.CurrentCulture, Strings.PrepHiddenToastFormat, Name),
            () => characters.Edit(CharacterSettingChange.Dismiss(contentId, EvercoldPrep.CardId, false)));
    }

    /// <summary>Show again (the dashboard's way back).</summary>
    internal void ShowAgain()
    {
        if (session.ViewedContentId is { } contentId)
        {
            characters.Edit(CharacterSettingChange.Dismiss(contentId, EvercoldPrep.CardId, false));
        }
    }

    /// <summary>A line's button.</summary>
    internal void Do(Act act, LineView line, UiState ui)
    {
        switch (act)
        {
            case Act.ShowNext when line.Target is { } next:
                ui.Reveal(next);
                break;
            case Act.StoryRoute or Act.FlyingRoute when line.Route is { } route:
                ui.OpenRoute(route);
                break;
            case Act.MakeRoom:
                MakeRoom?.Invoke()?.Open();
                break;
            case Act.ShowThem:
                // The Journal on exactly those quests: the Class & Job section, Ready on any job, every other narrowing
                // filter and the search cleared (as Tonight's "Show them" does for every Ready quest).
                ui.Tab = NavTab.Journal;
                ui.Scope = QuestScope.Section(JobLadder.ClassJobSectionId);
                ui.SelectedRowId = null;
                var includeUnlisted = ui.Filters.IncludeUnlisted;
                ui.Filters.Reset();
                ui.Filters.IncludeUnlisted = includeUnlisted;
                ui.Filters.StateMask = QuestStateMask.Ready | QuestStateMask.ReadyOnOtherJob;
                ui.SearchText = string.Empty;
                FiltersChanged?.Invoke();
                break;
            case Act.DutyBoard:
                ShowDutyBoard?.Invoke();
                break;
        }
    }

    private PrepInputs Inputs(CatalogBundle bundle, CharacterSnapshot snapshot, FlightIndex? flight, DutyRunIndex? duties)
    {
        if (!ReferenceEquals(ladderBundle, bundle))
        {
            ladderBundle = bundle;
            ladder = bundle.BuildJobLadder();
        }

        return new PrepInputs(bundle.Catalog, snapshot, session.States, ladder, duties, flight is null ? null : Zones(flight, EvercoldPrep.LatestExpansion(bundle.Catalog)));
    }

    /// <summary>The newest expansion's flying areas, each with whether flying there is unlocked (every current attuned) when that can be read.</summary>
    private List<PrepZone> Zones(FlightIndex flight, byte expansion)
    {
        var zones = new List<PrepZone>();
        foreach (var zone in flight.Zones)
        {
            if (zone.Expansion != expansion)
            {
                continue;
            }

            bool? flying = Attuned is null ? null : true;
            if (Attuned is { } attuned)
            {
                foreach (var id in zone.QuestCurrents.Select(static c => c.AetherCurrentId).Concat(zone.FieldCurrentIds))
                {
                    var read = attuned(id);
                    if (read is null)
                    {
                        flying = null;
                        break;
                    }

                    flying &= read.Value;
                }
            }

            zones.Add(new PrepZone(zone.TerritoryId, zone.Name, [.. zone.QuestCurrents.Select(static c => c.QuestRowId)], flying));
        }

        return zones;
    }

    private void BuildText(CatalogBundle bundle, CharacterSnapshot snapshot, ExpansionLaunch launch, DateTime now)
    {
        var culture = CultureInfo.CurrentCulture;
        Name = FirstName(snapshot.Name);
        var day = launch.EarlyAccessUtc.ToString(Strings.EventCardDateFormat, culture);
        var access = string.Format(culture, launch.Expected ? Strings.PrepEarlyAccessExpectedFormat : Strings.PrepEarlyAccessFormat, day);
        var forName = string.Format(culture, Strings.PrepForFormat, Name);
        SubTonight = forName + Strings.StateReasonSeparator + access;
        SubDashboard = (session.IsLive ? forName : string.Format(culture, Strings.PrepAsOfFormat, Ago(snapshot.TakenUtc, now))) + Strings.StateReasonSeparator + access;
        SubTooltip = launch.Expected ? Strings.PrepSubTooltipExpected : Strings.PrepSubTooltip;
        AllDone = string.Format(culture, Strings.PrepAllDoneFormat, Name);
        HiddenLine = string.Format(culture, Strings.PrepHiddenLineFormat, Name);
        Fold = card.Done.Count == 0 ? string.Empty
            : string.Format(culture, Strings.PrepFoldFormat, string.Join(Strings.PrepFoldSeparator, card.Done.Select(k => Title(k, bundle))));

        var views = new LineView[card.Open.Count];
        for (var i = 0; i < views.Length; i++)
        {
            views[i] = View(card.Open[i].Line, bundle);
        }

        Lines = views;
    }

    /// <summary>A line's label as the fold line names it: "Job and role quests", "Flying in Dawntrail".</summary>
    private static string Title(PrepLineKind kind, CatalogBundle bundle) => kind switch
    {
        PrepLineKind.Story => Strings.PrepStoryTitle,
        PrepLineKind.Journal => Strings.PrepJournalTitle,
        PrepLineKind.Jobs => Strings.PrepJobsTitle,
        PrepLineKind.Duties => Strings.PrepDutiesTitle,
        _ => string.Format(CultureInfo.CurrentCulture, Strings.PrepFlyingTitleFormat, ExpansionName(bundle, EvercoldPrep.LatestExpansion(bundle.Catalog))),
    };

    private LineView View(PrepLine line, CatalogBundle bundle)
    {
        var culture = CultureInfo.CurrentCulture;
        switch (line.Kind)
        {
            case PrepLineKind.Story:
            {
                var route = line.StoryEnd is { } end ? RouteTarget.ForQuest(end.RowId, session.Spoilers.DisplayName(end)) : null;
                if (line.Earlier)
                {
                    var detail = string.Format(culture, Strings.PrepStoryEarlierFormat, ExpansionName(bundle, line.InExpansion), line.Left);
                    return new LineView(line.Kind, StoryIcon, Strings.PrepStoryEarlierTitle, detail, Strings.PrepStoryWhy, Strings.PrepStoryTick,
                        [new ButtonView(Act.ShowNext, Strings.PrepShowNext, Strings.PrepShowNextTooltip)], line.Next, route);
                }

                var patch = PatchVersion.Series(line.StoryEnd?.AddedIn);
                var left = patch.Length == 0 ? string.Format(culture, Strings.PrepStoryLeftNoPatchFormat, line.Left)
                    : line.Left == 1 ? string.Format(culture, Strings.PrepStoryLeftOneFormat, patch)
                    : string.Format(culture, Strings.PrepStoryLeftFormat, line.Left, patch);
                ButtonView[] buttons = route is null
                    ? [new ButtonView(Act.ShowNext, Strings.PrepShowNext, Strings.PrepShowNextTooltip)]
                    : [new ButtonView(Act.ShowNext, Strings.PrepShowNext, Strings.PrepShowNextTooltip), new ButtonView(Act.StoryRoute, Strings.PrepRoute, Strings.PrepStoryRouteTooltip)];
                return new LineView(line.Kind, StoryIcon, Strings.PrepStoryTitle, left, Strings.PrepStoryWhy, Strings.PrepStoryTick, buttons, line.Next, route);
            }

            case PrepLineKind.Journal:
            {
                var detail = string.Format(culture, Strings.PrepJournalDetailFormat, line.Slots.Used, line.Slots.Cap);
                var why = string.Format(culture, Strings.PrepJournalWhyFormat, EvercoldPrep.JournalFreeSlots, line.Slots.Cap);
                return new LineView(line.Kind, PillIcon.JournalBook, Strings.PrepJournalTitle, detail, why, Strings.PrepJournalTick,
                    [new ButtonView(Act.MakeRoom, Strings.MakeRoom, Strings.MakeRoomTooltip)], null, null);
            }

            case PrepLineKind.Jobs:
            {
                var names = new List<string>(line.JobIds.Count + line.Roles.Count);
                foreach (var job in line.JobIds)
                {
                    var abbreviation = bundle.Names.ClassJobAbbreviation(job);
                    if (abbreviation.Length > 0)
                    {
                        names.Add(abbreviation);
                    }
                }

                foreach (var role in line.Roles)
                {
                    names.Add(string.Format(culture, Strings.PrepRoleFormat, Strings.JobsRoleName(role)));
                }

                var detail = string.Format(culture, names.Count == 1 ? Strings.PrepJobsDetailOneFormat : Strings.PrepJobsDetailFormat, List(names));
                var iconJob = line.JobIds.Count > 0 ? line.JobIds[0] : FirstJobOf(line.Roles);
                PillIcon icon = iconJob == 0 ? PillIcon.JournalBook : GameIconRef.Tile(ActionIcons.JobIconBase + iconJob);
                return new LineView(line.Kind, icon, Strings.PrepJobsTitle, detail, Strings.PrepJobsWhy, Strings.PrepJobsTick,
                    [new ButtonView(Act.ShowThem, Strings.PrepShowThem, Strings.PrepShowThemTooltip)], line.Quests.Count > 0 ? line.Quests[0] : null, null);
            }

            case PrepLineKind.Duties:
            {
                var expansion = ExpansionName(bundle, line.Expansion);
                var detail = line.Duties.Count == 1
                    ? string.Format(culture, Strings.PrepDutiesDetailOneFormat, expansion)
                    : string.Format(culture, Strings.PrepDutiesDetailFormat, line.Duties.Count, expansion);
                return new LineView(line.Kind, GameIconRef.Tile(ActionIcons.DutyFinder), Strings.PrepDutiesTitle, detail,
                    string.Format(culture, Strings.PrepDutiesWhyFormat, expansion), Strings.PrepDutiesTick,
                    [new ButtonView(Act.DutyBoard, Strings.PrepDutyBoard, Strings.PrepDutyBoardTooltip)], null, null);
            }

            default:
            {
                var expansion = ExpansionName(bundle, line.Expansion);
                var title = string.Format(culture, Strings.PrepFlyingTitleFormat, expansion);

                // N6: an area past the story reads as its placeholder, here and in the route's milestones.
                var names = line.Zones.Select(z => session.Spoilers.Name(SpoilerKind.Area, z.Name)).ToArray();
                var detail = names.Length == 1
                    ? string.Format(culture, Strings.PrepFlyingDetailOneFormat, names[0])
                    : string.Format(culture, Strings.PrepFlyingDetailFormat, names.Length, List(names));
                var route = FlyingRoute(line, names, title);
                ButtonView[] buttons = route is null ? [] : [new ButtonView(Act.FlyingRoute, Strings.PrepRoute, Strings.PrepFlyingRouteTooltip)];
                return new LineView(line.Kind, GameIconRef.Tile(ActionIcons.Fly), title, detail,
                    string.Format(culture, Strings.PrepFlyingWhyFormat, expansion), Strings.PrepFlyingTick, buttons, null, route);
            }
        }
    }

    /// <summary>
    /// K3's Route to unlock flying, over every area left: each quest whose current is still needed is a part named
    /// after its area ("Flying in Shaaloani"), so each area is one milestone. With one area left, its field currents
    /// follow the quests. Null when no such quest is left (only field currents).
    /// </summary>
    private RouteTarget? FlyingRoute(PrepLine line, string[] names, string title)
    {
        var parts = new List<RouteTarget>();
        for (var i = 0; i < line.Zones.Count; i++)
        {
            var label = string.Format(CultureInfo.CurrentCulture, Strings.PrepFlyingTitleFormat, names[i]);
            foreach (var rowId in line.Zones[i].QuestRowIds)
            {
                if (!session.States.TryGetValue(rowId, out var state) || state.State != QuestState.Completed)
                {
                    parts.Add(new RouteTarget(RouteTargetKind.Quest, label, [rowId]));
                }
            }
        }

        if (parts.Count == 0)
        {
            return null;
        }

        return RouteTarget.Union(RouteTargetKind.Unlock, title, parts) with
        {
            Icon = ActionIcons.Fly,
            FlyingTerritory = line.Zones.Count == 1 ? line.Zones[0].TerritoryId : 0,
        };
    }

    private uint FirstJobOf(IReadOnlyList<JobRole> roles)
    {
        foreach (var entry in ladder.Jobs)
        {
            if (ladder.RoleOf(entry.Job.RowId) is { } role && roles.Contains(role))
            {
                return entry.Job.RowId;
            }
        }

        return 0;
    }

    private static readonly PillIcon StoryIcon = GameIconRef.Symbol(NodeIcons.MsqMarker);

    private static string ExpansionName(CatalogBundle bundle, byte expansion) =>
        bundle.Names.Expansion(expansion) is { Length: > 0 } named ? named : Core.Evaluation.Expansions.Name(expansion);

    /// <summary>"DRG", "DRG and SGE", "DRG, SGE and CUL".</summary>
    private static string List(IReadOnlyList<string> items) => items.Count switch
    {
        0 => string.Empty,
        1 => items[0],
        _ => string.Join(Strings.PrepListComma, items.Take(items.Count - 1)) + Strings.PrepListAnd + items[^1],
    };

    /// <summary>"2 days ago", "1 day ago", "today": how long ago the stored character's last capture was.</summary>
    private static string Ago(DateTime takenUtc, DateTime nowUtc)
    {
        var days = (int)Math.Floor((nowUtc - DateTime.SpecifyKind(takenUtc, DateTimeKind.Utc)).TotalDays);
        return days switch
        {
            <= 0 => Strings.PrepAgoToday,
            1 => Strings.PrepAgoDayOne,
            _ => string.Format(CultureInfo.CurrentCulture, Strings.PrepAgoDaysFormat, days),
        };
    }

    private static string FirstName(string name)
    {
        var trimmed = name.Trim();
        var space = trimmed.IndexOf(' ', StringComparison.Ordinal);
        return space > 0 ? trimmed[..space] : trimmed;
    }
}
