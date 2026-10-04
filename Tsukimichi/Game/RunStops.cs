using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Tsukimichi.Config;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Travel;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// Runs you can trust (feature plan v7, 1.18.0): the hub of the "Why it stopped" card (A2) and the "Needs you" panel
/// (A5). It holds the one card (<see cref="Dock"/>) and the panel's queue (<see cref="NeedsYou"/>), turns a Questionable
/// run's end (its receipt, A4), a duty guard stop (A3), a knock-out or stall (A5) and a walk travel recovery gave up on
/// (A8, <see cref="TravelService.GaveUp"/>) into a card, runs the cards' safe fixes and builds Copy report.
/// <para>
/// The public raise calls (<see cref="Raise"/>, <see cref="RaiseMissingPlugin"/>, <see cref="RaiseError"/>,
/// <see cref="Alert"/>) are the small API other parts call to show a card or an alert; each composes nothing the card
/// cannot show and never throws into its caller.
/// </para>
/// <para>
/// Every fix is safe by construction (<see cref="StopFix"/>): it starts the same kind of hand-off again or opens a view,
/// never answers the game for the player and never chains. Two run later, when their moment comes, and only once:
/// "Keep going after it" restarts Questionable after the player has cleared the duty themselves (it waits while the
/// card shows, and the card's × or another hand-off cancels it), and "Reload navmesh and retry" starts again once
/// vnavmesh has rebuilt the zone (a minute at most). Runs on the framework thread; the clock is its own steady one
/// (<see cref="Now"/>), which the surfaces draw with too.
/// </para>
/// </summary>
public sealed class RunStops : IDisposable
{
    /// <summary>How long "Reload navmesh and retry" waits for vnavmesh to be ready again before it gives up.</summary>
    public const double RetryWaitSeconds = 60.0;

    /// <summary>How long after a reload is asked before vnavmesh's "ready" is believed (it reads ready until the rebuild starts).</summary>
    public const double RetrySettleSeconds = 1.5;

    private const string QuestionableName = "Questionable";
    private const string AutoDutyName = "AutoDuty";
    private const string ArtisanName = "Artisan";

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IObjectTable objects;
    private readonly IDataManager data;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly Configuration config;
    private readonly Action save;
    private readonly SessionState session;
    private readonly QuestionableIpc questionable;
    private readonly TravelService travel;
    private readonly AutoDutyIpc autoDuty;
    private readonly ArtisanIpc? artisan;
    private readonly HookGate gate;
    private readonly Action<string> print;
    private readonly IPluginLog log;
    private readonly Stopwatch clock = Stopwatch.StartNew();

    private QuestionableStatus lastRun = QuestionableStatus.Idle;
    private bool wasRunning;
    private int seenReceipts = -1;
    private double troubleAt = double.NegativeInfinity;
    private StopReason troubleReason;
    private bool shownThisFrame;
    private bool hoveredThisFrame;
    private TravelGaveUp? lastGaveUp;
    private GoToGiverPlan? lastGaveUpPlan;
    private uint lastGaveUpQuest;

    // "Keep going after it": the duty to wait for and the quest to restart, while the card it came from shows.
    private StopCard? waitingFor;

    // "Reload navmesh and retry": what to start once vnavmesh is ready again.
    private RetryPlan? retry;
    private double copiedAt = double.NegativeInfinity;
    private bool warned;

    public RunStops(
        IFramework framework,
        IClientState clientState,
        ICondition condition,
        IObjectTable objects,
        IDataManager data,
        IDalamudPluginInterface pluginInterface,
        Configuration config,
        Action save,
        SessionState session,
        QuestionableIpc questionable,
        TravelService travel,
        AutoDutyIpc autoDuty,
        ArtisanIpc? artisan,
        HookGate gate,
        Action<string> print,
        IPluginLog log)
    {
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
        this.objects = objects ?? throw new ArgumentNullException(nameof(objects));
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.questionable = questionable ?? throw new ArgumentNullException(nameof(questionable));
        this.travel = travel ?? throw new ArgumentNullException(nameof(travel));
        this.autoDuty = autoDuty ?? throw new ArgumentNullException(nameof(autoDuty));
        this.artisan = artisan;
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        this.print = print ?? throw new ArgumentNullException(nameof(print));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        framework.Update += OnUpdate;
        travel.GaveUp += OnTravelGaveUp;
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        travel.GaveUp -= OnTravelGaveUp;
    }

    /// <summary>The "Why it stopped" card.</summary>
    public StopDock Dock { get; } = new();

    /// <summary>The "Needs you" panel's alerts.</summary>
    public NeedsYouQueue NeedsYou { get; } = new();

    /// <summary>The steady clock the card, the panel and their surfaces share, in seconds.</summary>
    public double Now => clock.Elapsed.TotalSeconds;

    /// <summary>The Questionable run receipts (A4); a run's end becomes a card. Set by the plugin.</summary>
    public QuestionableRunWatch? Runs { get; set; }

    /// <summary>Start Questionable again (the fixes). Set by the plugin.</summary>
    public QuestionableActions? Actions { get; set; }

    /// <summary>The game links: the Duty Finder, the map, Teleport, the journal. Set by the plugin.</summary>
    public GameLinks? Links { get; set; }

    /// <summary>Opens Settings › Companions › Setup. Set by the plugin.</summary>
    public Action? OpenSetup { get; set; }

    /// <summary>The panel's Stop all: <c>/tsuki stop</c>. Set by the plugin.</summary>
    public Action? StopAll { get; set; }

    /// <summary>The travel preflight, for the report's movement line. Set by the plugin.</summary>
    public TravelPreflightService? Preflight { get; set; }

    /// <summary>The game's version, for the report. Set by the plugin.</summary>
    public string GameVersion { get; set; } = string.Empty;

    /// <summary>Whether "Keep going after it" waits for the card on screen.</summary>
    public bool WaitingForDuty => waitingFor is not null;

    /// <summary>Whether "Reload navmesh and retry" waits for vnavmesh.</summary>
    public bool Retrying => retry is not null;

    /// <summary>Whether Copy report was pressed in the last two seconds (the action then says "Report copied").</summary>
    public bool JustCopied => Now - copiedAt < 2.0;

    // ------------------------------------------------------------------ the API

    /// <summary>Shows <paramref name="card"/> (counting a stop at its step); a calm card never replaces one that needs the player.</summary>
    public void Raise(StopCard card)
    {
        try
        {
            if (card.NeedsYou && card.QuestRowId != 0)
            {
                RunStopCounts.Note(config.RunStopCounts, card.QuestRowId, card.Sequence);
                save();
            }

            if (Dock.Raise(card, Now))
            {
                log.Information("Why it stopped: {Reason} ({HandOff}) at quest {RowId}", card.Reason, card.HandOff, card.QuestRowId);
            }
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "A stop card could not be raised");
        }
    }

    /// <summary>
    /// A hand-off could not run because its plugin is not loaded. <paramref name="why"/> says whether it is installed but
    /// off or not installed (<see cref="CompanionPlugins.DisabledReason"/>); null says to turn it on or install it.
    /// </summary>
    public void RaiseMissingPlugin(StopHandOff handOff, string? why, uint questRowId = 0) =>
        Raise(new StopCard(StopReason.MissingPlugin, handOff)
        {
            Title = Format(Strings.StopCardTitle(StopReason.MissingPlugin), HandOffName(handOff)),
            Why = why ?? Strings.StopWhyMissing,
            QuestRowId = questRowId,
            Context = questRowId != 0 ? QuestName(questRowId) : string.Empty,
            ReportReason = "missing plugin",
        });

    /// <summary>A hand-off stopped with an error <paramref name="why"/> said in plain words.</summary>
    public void RaiseError(StopHandOff handOff, string why, uint questRowId = 0) =>
        Raise(new StopCard(StopReason.Error, handOff)
        {
            Title = Format(Strings.StopCardTitle(StopReason.Error), HandOffName(handOff)),
            Why = why,
            QuestRowId = questRowId,
            Context = questRowId != 0 ? QuestName(questRowId) : string.Empty,
            TerritoryId = clientState.TerritoryType,
            Position = objects.LocalPlayer?.Position,
            ReportReason = "error",
        });

    /// <summary>Puts <paramref name="alert"/> on the "Needs you" panel.</summary>
    public void Alert(NeedsYouAlert alert)
    {
        try
        {
            NeedsYou.Raise(alert, Now);
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "A Needs you alert could not be shown");
        }
    }

    /// <summary>
    /// The duty guard (A3) stopped Questionable before <paramref name="duty"/> (null when unsure which): the guard's own
    /// card, which the run receipt that follows refreshes rather than replaces.
    /// </summary>
    public void RaiseDutyGuard(QuestionableStatus status, DutyRunInfo? duty, string dutyName)
    {
        var rowId = status.RowId ?? 0;
        var why = duty is not null && dutyName.Length > 0 ? Format(Strings.StopWhyDutyGuardFormat, dutyName) : Strings.StopWhyDutyGuard;
        Raise(new StopCard(StopReason.DutyGuard, StopHandOff.Questionable)
        {
            Title = Strings.StopCardTitle(StopReason.DutyGuard),
            Why = why,
            Context = Join(StepContext(rowId, status.Step), Strings.StopContextGuardSetting),
            QuestRowId = rowId,
            DutyId = duty?.ContentFinderConditionId ?? 0,
            InstanceContentId = duty?.InstanceContentId ?? 0,
            DutyName = dutyName,
            TerritoryId = clientState.TerritoryType,
            Position = objects.LocalPlayer?.Position,
            Sequence = status.Sequence,
            Step = status.Step,
            SingleQuest = Runs?.Origin != QuestionableRunOrigin.KeepGoing,
            ReportReason = "duty guard · no Duty Support or Trust",
        });
    }

    /// <summary>
    /// The character was knocked out (or stalled, <paramref name="reason"/> <see cref="StopReason.Stuck"/>) during a
    /// run, and <paramref name="did"/> says what Tsukimichi stopped: the card, and the reason a Questionable run that
    /// ends in the next <see cref="RunStopClassifier.TroubleExplainsSeconds"/> gets.
    /// </summary>
    public void RaiseTrouble(StopReason reason, string did)
    {
        troubleAt = Now;
        troubleReason = reason;
        var status = questionable.LastStatus;
        var rowId = status.Running ? status.RowId ?? 0 : 0;
        var handOff = status.Running ? StopHandOff.Questionable
            : travel.JourneyActive ? StopHandOff.Travel
            : autoDuty.HandOffClaimed ? StopHandOff.AutoDuty
            : artisan is { HandOffClaimed: true } ? StopHandOff.Artisan
            : StopHandOff.Travel;
        var why = reason == StopReason.KnockedOut
            ? rowId != 0 ? Format(Strings.StopWhyKnockedOutFormat, QuestName(rowId), did) : Format(Strings.StopWhyKnockedOut, did)
            : Strings.StopWhyStuck;
        Raise(new StopCard(reason, handOff)
        {
            Title = Format(Strings.StopCardTitle(reason), HandOffName(handOff)),
            Why = why,
            Context = Join(StepContext(rowId, status.Step), ZoneName(clientState.TerritoryType)),
            QuestRowId = rowId,
            TerritoryId = clientState.TerritoryType,
            Position = objects.LocalPlayer?.Position,
            Sequence = status.Sequence,
            Step = status.Step,
            SingleQuest = Runs?.Origin != QuestionableRunOrigin.KeepGoing,
            ReportReason = reason == StopReason.KnockedOut ? "knocked out" : "stuck · no progress",
        });
    }

    /// <summary>What a knock-out or a stall stopped, in one sentence, for the chat line, the panel and the card.</summary>
    public static string DidSentence(IReadOnlyList<string> stopped, bool questionableLeftRunning)
    {
        var did = stopped.Count > 0 ? Format(Strings.NeedsYouDidStoppedFormat, string.Join(Strings.CommandListSeparator, stopped)) : Strings.NeedsYouDidNothing;
        return questionableLeftRunning ? did + " " + Strings.NeedsYouDidQuestionableRuns : did;
    }

    // ------------------------------------------------------------------ the surfaces

    /// <summary>A surface drew the card this frame (<paramref name="hovered"/>: under the pointer, which stops its clock).</summary>
    public void NoteShown(bool hovered)
    {
        shownThisFrame = true;
        hoveredThisFrame |= hovered;
    }

    /// <summary>"2 min ago" for the card on screen.</summary>
    public string AgoText()
    {
        var minutes = (int)((Now - Dock.RaisedAt) / 60.0);
        return minutes < 1 ? Strings.StopAgoNow
            : minutes < 60 ? Format(Strings.StopAgoMinutesFormat, minutes)
            : Format(Strings.StopAgoHoursFormat, minutes / 60);
    }

    /// <summary>The game icon of a card's hand-off (spec-1.18: the quest's marker, Duty Finder, Sprint, Crafting Log).</summary>
    public GameIconRef Icon(StopCard card) => card.HandOff switch
    {
        StopHandOff.AutoDuty => ActionIcons.DutyFinderIcon,
        StopHandOff.Travel => ActionIcons.WalkIcon,
        StopHandOff.Artisan => GameIconRef.Tile(ActionIcons.CraftingLog),
        _ => ActionIcons.GoTo(card.QuestRowId != 0 ? session.Bundle?.Catalog.GetByRowId(card.QuestRowId) : null),
    };

    /// <summary>Whether <paramref name="fix"/> shows on <paramref name="card"/> (a fix with nothing to act on is left out).</summary>
    public bool Shows(StopCard card, StopFix fix) => fix switch
    {
        StopFix.None => false,
        StopFix.StartNext => card.NextRowId != 0 && Actions is not null,
        StopFix.StartAgain => card.QuestRowId != 0 && Actions is not null,
        StopFix.ShowDuty => card.DutyId != 0 && Links is not null,
        StopFix.KeepGoingAfterDuty => card.InstanceContentId != 0 && card.QuestRowId != 0 && Actions is not null,
        StopFix.TryAgain => card.HandOff == StopHandOff.Questionable ? card.QuestRowId != 0 && Actions is not null : RetryPlanFor(card) is not null,
        StopFix.ReloadAndRetry => RetryPlanFor(card) is not null || (card.HandOff == StopHandOff.Questionable && card.QuestRowId != 0 && Actions is not null),
        StopFix.FlagSpot => card.Position is not null && card.TerritoryId != 0 && Links is not null,
        StopFix.TeleportCloser => TravelQuest(card) is not null && Links is not null,
        StopFix.OpenSetup => OpenSetup is not null,
        StopFix.OpenJournal => card.QuestRowId != 0 && Links is not null && Quest(card.QuestRowId) is not null,
        _ => false,
    };

    /// <summary>Why <paramref name="fix"/> cannot act now, shown under or on it; null when it can.</summary>
    public string? Blocker(StopCard card, StopFix fix)
    {
        switch (fix)
        {
            case StopFix.StartNext:
                return Actions?.StartQuestBlocker(card.NextRowId);
            case StopFix.StartAgain:
                return Actions?.StartQuestBlocker(card.QuestRowId);
            case StopFix.ShowDuty:
                return Links?.CanOpenDutyFinder(card.DutyId) == true ? null : Strings.StopFixShowDutyTooltip;
            case StopFix.TryAgain:
                if (objects.LocalPlayer is not { } player || player.IsDead)
                {
                    return Strings.StopTryAgainWaitUp;
                }

                if (condition[ConditionFlag.InCombat])
                {
                    return Strings.StopTryAgainWaitCombat;
                }

                return card.HandOff == StopHandOff.Questionable ? Actions?.StartQuestBlocker(card.QuestRowId) : null;
            case StopFix.ReloadAndRetry:
                return travel.Vnavmesh.Available ? null : Strings.StopReloadNoVnav;
            case StopFix.FlagSpot:
                return Links?.CanFlagSpot(card.TerritoryId) == true ? null : Strings.StopFixFlagSpotTooltip;
            case StopFix.TeleportCloser:
                return TravelQuest(card) is { } quest && Links is { } links && !links.CanTeleport(quest) ? links.TeleportTooltip(quest) : null;
            default:
                return null;
        }
    }

    /// <summary>The fix's label (Keep going after it reads "Stop waiting" while it waits; the reload pill says it reloads).</summary>
    public string Label(StopFix fix) => fix switch
    {
        StopFix.StartNext => Strings.StopFixStartNext,
        StopFix.StartAgain => Strings.StopFixStartAgain,
        StopFix.ShowDuty => Strings.StopFixShowDuty,
        StopFix.KeepGoingAfterDuty => WaitingForDuty ? Strings.StopFixKeepGoingCancel : Strings.StopFixKeepGoing,
        StopFix.TryAgain => Strings.StopFixTryAgain,
        StopFix.ReloadAndRetry => Retrying ? Strings.StopFixReloading : Strings.StopFixReloadRetry,
        StopFix.FlagSpot => Strings.StopFixFlagSpot,
        StopFix.TeleportCloser => Strings.StopFixTeleport,
        StopFix.OpenSetup => Strings.StopFixOpenSetup,
        StopFix.OpenJournal => Strings.StopFixOpenJournal,
        _ => string.Empty,
    };

    /// <summary>The fix's tooltip: what it does.</summary>
    public string Tooltip(StopCard card, StopFix fix) => fix switch
    {
        StopFix.StartNext => Format(Strings.StopFixStartNextTooltipFormat, QuestName(card.NextRowId)),
        StopFix.StartAgain => Format(Strings.StopFixStartAgainTooltipFormat, QuestName(card.QuestRowId)),
        StopFix.ShowDuty => Strings.StopFixShowDutyTooltip,
        StopFix.KeepGoingAfterDuty => Strings.StopFixKeepGoingTooltip,
        StopFix.TryAgain => Strings.StopFixTryAgainTooltip,
        StopFix.ReloadAndRetry => Strings.StopFixReloadRetryTooltip,
        StopFix.FlagSpot => Strings.StopFixFlagSpotTooltip,
        StopFix.TeleportCloser => TravelQuest(card) is { } quest && Links is { } links ? links.TeleportTooltip(quest) : string.Empty,
        StopFix.OpenSetup => Strings.StopFixOpenSetupTooltip,
        _ => string.Empty,
    };

    /// <summary>The fix's icon.</summary>
    public PillIcon FixIcon(StopCard card, StopFix fix) => fix switch
    {
        StopFix.StartNext => ActionIcons.GoTo(Quest(card.NextRowId)),
        StopFix.StartAgain or StopFix.KeepGoingAfterDuty => ActionIcons.GoTo(Quest(card.QuestRowId)),
        StopFix.TryAgain => card.HandOff == StopHandOff.Questionable ? ActionIcons.GoTo(Quest(card.QuestRowId)) : ActionIcons.WalkIcon,
        StopFix.ShowDuty => ActionIcons.DutyFinderIcon,
        StopFix.ReloadAndRetry => ActionIcons.WalkIcon,
        StopFix.FlagSpot => ActionIcons.FlagIcon,
        StopFix.TeleportCloser => ActionIcons.TeleportIcon,
        StopFix.OpenSetup => Chrome.Icon(Dalamud.Interface.FontAwesomeIcon.Cog),
        _ => PillIcon.JournalBook,
    };

    /// <summary>Runs <paramref name="fix"/> for <paramref name="card"/>; Questionable's confirmation (if any) opens in <paramref name="host"/>.</summary>
    public void Fix(StopCard card, StopFix fix, string host)
    {
        try
        {
            switch (fix)
            {
                case StopFix.StartNext:
                    Actions?.StartQuest(host, card.NextRowId);
                    break;
                case StopFix.StartAgain:
                    StartQuestionable(card, host);
                    break;
                case StopFix.ShowDuty:
                    Links?.OpenDutyFinder(card.DutyId);
                    break;
                case StopFix.KeepGoingAfterDuty:
                    waitingFor = WaitingForDuty ? null : card;
                    break;
                case StopFix.TryAgain:
                    if (card.HandOff == StopHandOff.Questionable)
                    {
                        StartQuestionable(card, host);
                    }
                    else if (RetryPlanFor(card) is { } again)
                    {
                        travel.Start(again.Plan, again.Target);
                    }

                    break;
                case StopFix.ReloadAndRetry:
                    StartRetry(card);
                    break;
                case StopFix.FlagSpot when card.Position is { } at:
                    Links?.FlagSpot(card.TerritoryId, at);
                    break;
                case StopFix.TeleportCloser when TravelQuest(card) is { } quest:
                    Links?.TeleportToGiver(quest);
                    break;
                case StopFix.OpenSetup:
                    OpenSetup?.Invoke();
                    break;
                case StopFix.OpenJournal when Quest(card.QuestRowId) is { } quest:
                    Links?.OpenJournal(quest);
                    break;
            }
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "A stop card fix failed");
        }
    }

    /// <summary>The panel's fix for its alert (Reload navmesh and retry after a walk gave up).</summary>
    public bool PanelShows(NeedsYouAlert alert) => alert.Fix == StopFix.ReloadAndRetry && lastGaveUpPlan is not null;

    /// <summary>Runs the panel's fix.</summary>
    public void PanelFix(NeedsYouAlert alert)
    {
        if (alert.Fix == StopFix.ReloadAndRetry && lastGaveUpPlan is { } plan && lastGaveUp is { } gaveUp)
        {
            BeginRetry(new RetryPlan(plan, gaveUp.Target, 0, false));
        }
    }

    /// <summary>The card's × (or the overlay's): the card fades out, and "Keep going after it" stops waiting.</summary>
    public void Dismiss()
    {
        waitingFor = null;
        Dock.Dismiss(Now);
    }

    /// <summary>
    /// The report for the card on screen (spec-1.18 A2, "Copy report"), for the clipboard: never the character's name,
    /// world, Free Company or chat. Notes the copy for "Report copied".
    /// </summary>
    public string Report(StopCard card)
    {
        copiedAt = Now;
        var player = objects.LocalPlayer;
        var privateWords = new List<string>(4);
        if (player is not null)
        {
            privateWords.Add(player.Name.TextValue);
            privateWords.Add(player.CompanyTag.TextValue);
            privateWords.Add(player.HomeWorld.ValueNullable?.Name.ExtractText() ?? string.Empty);
            privateWords.Add(player.CurrentWorld.ValueNullable?.Name.ExtractText() ?? string.Empty);
        }

        var territory = card.TerritoryId != 0 ? card.TerritoryId : clientState.TerritoryType;
        var single = card.SingleQuest ? "\"this quest only\"" : "\"keep going\"";
        var facts = new RunReportFacts
        {
            PluginVersion = DiagnosticBuilder.PluginVersionText(),
            ApiLevel = typeof(IDalamudPluginInterface).Assembly.GetName().Version?.Major.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            GameVersion = GameVersion,
            HandOff = card.HandOff.ToString(),
            HandOffVersion = card.HandOff switch
            {
                StopHandOff.Questionable => PluginVersion(QuestionableName),
                StopHandOff.AutoDuty => PluginVersion(AutoDutyName),
                StopHandOff.Artisan => PluginVersion(ArtisanName),
                _ => string.Empty,
            },
            StartedBy = card.HandOff == StopHandOff.Questionable ? "started by Tsukimichi, " + single : "started by Tsukimichi",
            Stopped = card.ReportReason.Length > 0 ? card.ReportReason : card.Reason.ToString().ToLowerInvariant(),
            QuestName = card.QuestRowId != 0 ? QuestName(card.QuestRowId) : string.Empty,
            QuestId = card.QuestRowId & 0xFFFF,
            Sequence = card.Sequence,
            Step = card.Step,
            Job = player?.ClassJob.ValueNullable?.Abbreviation.ExtractText() ?? string.Empty,
            Level = player?.Level ?? 0,
            Zone = ZoneName(territory),
            TerritoryId = territory,
            MapCoordinates = card.Position is { } at && Links is { } links ? links.MapCoordinateText(territory, at) : string.Empty,
            Travel = TravelLine(),
            StopsHere = card.QuestRowId != 0 && config.RunStopCounts.TryGetValue(RunStopCounts.Key(card.QuestRowId, card.Sequence), out var count) ? count : 0,
        };
        return RunReport.Build(facts, privateWords);
    }

    // ------------------------------------------------------------------ the frame

    private void OnUpdate(IFramework _)
    {
        try
        {
            Tick();
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Run stops failed");
        }
    }

    private void Tick()
    {
        var now = Now;
        var reduce = UiMetrics.ReduceMotion;
        var status = questionable.LastStatus;
        if (status.Running)
        {
            lastRun = status;
        }

        // Another hand-off starting means the card is about an older run: it goes, and nothing waits on its behalf.
        var running = status.Running || travel.JourneyActive || autoDuty.HandOffClaimed || artisan is { HandOffClaimed: true };
        if (running && !wasRunning && Dock.Current is not null && !Dock.Leaving)
        {
            waitingFor = null;
            Dock.HandOffStarted(now);
        }

        wasRunning = running;
        TakeReceipt();
        TickWaiting();
        TickRetry(now);

        // A cleared cause takes its alert away: the character is up, the duty pop closed.
        if (NeedsYou.Has(NeedsYouKind.Death) && objects.LocalPlayer is { IsDead: false })
        {
            NeedsYou.Clear(NeedsYouKind.Death, now);
        }

        if (NeedsYou.Has(NeedsYouKind.DutyPop) && !condition[ConditionFlag.InDutyQueue])
        {
            NeedsYou.Clear(NeedsYouKind.DutyPop, now);
        }

        if (!clientState.IsLoggedIn)
        {
            waitingFor = null;
            retry = null;
        }

        Dock.Tick(now, shownThisFrame, hoveredThisFrame, reduce);
        NeedsYou.Tick(now, reduce);
        shownThisFrame = false;
        hoveredThisFrame = false;
    }

    /// <summary>A new run receipt becomes a card: the run Tsukimichi started, or one a stop condition ended.</summary>
    private void TakeReceipt()
    {
        if (Runs is not { } runs)
        {
            return;
        }

        if (seenReceipts < 0)
        {
            // The receipt kept from an earlier session is no news.
            seenReceipts = runs.Version;
            return;
        }

        if (runs.Version == seenReceipts)
        {
            return;
        }

        seenReceipts = runs.Version;
        if (runs.Receipts.Count == 0 || runs.Receipts[0] is not { } receipt || !RunStopClassifier.CardFor(receipt.End, receipt.Origin))
        {
            return;
        }

        var trouble = Now - troubleAt <= RunStopClassifier.TroubleExplainsSeconds ? troubleReason : (StopReason?)null;
        var reason = RunStopClassifier.FromReceipt(receipt.End, trouble);
        var rowId = receipt.QuestRowId != 0 ? receipt.QuestRowId : lastRun.RowId ?? 0;
        var handOff = StopHandOff.Questionable;
        string why;
        if (reason == StopReason.Error && lastRun.Interaction is { } kind && kind.Contains("Duty", StringComparison.OrdinalIgnoreCase)
            && !kind.Contains("SinglePlayer", StringComparison.OrdinalIgnoreCase) && !autoDuty.Available)
        {
            // The step was a duty run and AutoDuty is not there to run it: say that rather than "an error".
            reason = StopReason.MissingPlugin;
            handOff = StopHandOff.AutoDuty;
            why = Format(Strings.StopWhyMissingDutyFormat, CompanionPlugins.DisabledReason(CompanionPlugin.AutoDuty) ?? Strings.StopWhyMissing);
        }
        else
        {
            why = reason switch
            {
                StopReason.Finished => runs.Describe(receipt),
                StopReason.Player => rowId != 0 ? Format(Strings.StopWhyPlayerFormat, QuestName(rowId)) : Strings.StopWhyPlayer,
                StopReason.DutyGuard => Strings.StopWhyDutyGuard,
                StopReason.KnockedOut => rowId != 0 ? Format(Strings.StopWhyKnockedOutFormat, QuestName(rowId), string.Empty).TrimEnd() : Format(Strings.StopWhyKnockedOut, string.Empty).TrimEnd(),
                StopReason.Stuck => Strings.StopWhyStuck,
                _ => rowId != 0 ? Format(Strings.StopWhyErrorFormat, QuestName(rowId)) : Strings.StopWhyError,
            };
        }

        var single = receipt.Origin != QuestionableRunOrigin.KeepGoing;
        Raise(new StopCard(reason, handOff)
        {
            Title = Format(Strings.StopCardTitle(reason), HandOffName(handOff)),
            Why = why,
            Context = reason == StopReason.Finished ? string.Empty : Join(StepContext(rowId, lastRun.RowId == rowId ? lastRun.Step : null), ZoneName(clientState.TerritoryType)),
            Receipt = reason == StopReason.Finished ? ReceiptLine(receipt) : string.Empty,
            QuestRowId = rowId,
            NextRowId = reason == StopReason.Finished ? NextQuest(receipt) : 0,
            TerritoryId = clientState.TerritoryType,
            Position = objects.LocalPlayer?.Position,
            Sequence = lastRun.RowId == rowId ? lastRun.Sequence : null,
            Step = lastRun.RowId == rowId ? lastRun.Step : null,
            SingleQuest = single,
            ReportReason = ReportReason(reason, receipt.End),
        });
    }

    /// <summary>"Keep going after it": once the player has cleared the duty themselves and stands outside it, Questionable starts again, once.</summary>
    private void TickWaiting()
    {
        if (waitingFor is not { } card)
        {
            return;
        }

        if (!ReferenceEquals(Dock.Current, card) && Dock.Current?.Key != card.Key)
        {
            // The card went (dismissed, replaced): nothing waits on its behalf.
            waitingFor = null;
            return;
        }

        if (!gate.HooksAllowed || objects.LocalPlayer is not { IsDead: false } || travel.InDuty || condition[ConditionFlag.InCombat] || travel.BetweenAreas)
        {
            return;
        }

        if (!Cleared(card.InstanceContentId))
        {
            return;
        }

        waitingFor = null;
        log.Information("Keep going after it: duty {Duty} cleared; starting Questionable on {RowId}", card.InstanceContentId, card.QuestRowId);
        if (Actions?.StartChosen(card.QuestRowId, card.SingleQuest) != true)
        {
            print(Strings.QuestionableStartFailed);
        }
    }

    private static bool Cleared(uint instanceContentId) => instanceContentId != 0 && UIState.IsInstanceContentCompleted(instanceContentId);

    /// <summary>"Reload navmesh and retry": once vnavmesh is ready again, the same walk (or Questionable) starts once.</summary>
    private void TickRetry(double now)
    {
        if (retry is not { } pending)
        {
            return;
        }

        if (now - pending.AskedAt < RetrySettleSeconds)
        {
            return;
        }

        if (now - pending.AskedAt > RetryWaitSeconds)
        {
            retry = null;
            print(Strings.StopReloadFailed);
            return;
        }

        if (!travel.Vnavmesh.IsReady || travel.BetweenAreas)
        {
            return;
        }

        retry = null;
        if (pending.Questionable)
        {
            if (Actions?.StartChosen(pending.QuestRowId, pending.Single) != true)
            {
                print(Strings.QuestionableStartFailed);
            }
        }
        else if (pending.Plan is { } plan)
        {
            travel.Start(plan with { Options = travel.CurrentOptions }, pending.Target);
        }
    }

    private void StartRetry(StopCard card)
    {
        if (Retrying)
        {
            return;
        }

        if (RetryPlanFor(card) is { } plan)
        {
            BeginRetry(plan);
        }
        else if (card.HandOff == StopHandOff.Questionable && card.QuestRowId != 0)
        {
            // Questionable was walking: reload, then start it again on the same quest, unless it still runs (it then
            // finds its own new path on the fresh navmesh).
            BeginRetry(new RetryPlan(null, string.Empty, card.QuestRowId, card.SingleQuest) { Questionable = !questionable.LastStatus.Running });
        }
    }

    private void BeginRetry(RetryPlan plan)
    {
        if (!travel.StartNavReload())
        {
            // No reload to wait for (the gate is missing): start again now.
            retry = plan with { AskedAt = Now - RetrySettleSeconds };
            return;
        }

        retry = plan with { AskedAt = Now };
    }

    private RetryPlan? RetryPlanFor(StopCard card) =>
        card.HandOff == StopHandOff.Travel && lastGaveUpPlan is { } plan && lastGaveUp is { } gaveUp && ReferenceEquals(Dock.Current, card)
            ? new RetryPlan(plan, gaveUp.Target, 0, false)
            : null;

    private void StartQuestionable(StopCard card, string host)
    {
        if (Actions is not { } actions || card.QuestRowId == 0)
        {
            return;
        }

        if (card.SingleQuest)
        {
            actions.StartQuest(host, card.QuestRowId);
        }
        else
        {
            actions.StartKeepGoing(host, card.QuestRowId);
        }
    }

    // ------------------------------------------------------------------ travel

    /// <summary>A walk travel recovery (A8) gave up on: a Stuck or No path card, with Reload navmesh and retry.</summary>
    private void OnTravelGaveUp(TravelGaveUp gaveUp)
    {
        try
        {
            lastGaveUp = gaveUp;
            lastGaveUpPlan = travel.JourneyPlan;
            lastGaveUpQuest = Links?.JourneyQuestRowId ?? 0;
            var reason = RunStopClassifier.FromTravel(gaveUp.Failure);
            var zone = ZoneName(clientState.TerritoryType);
            var why = reason switch
            {
                StopReason.Stuck => zone.Length > 0 ? Format(Strings.StopWhyStuckFormat, zone) : Strings.StopWhyStuck,
                StopReason.NoPath => gaveUp.Target.Length > 0 ? Format(Strings.StopWhyNoPathFormat, gaveUp.Target) : Strings.StopWhyNoPath,
                _ => Format(Strings.StopWhyTravelErrorFormat, gaveUp.Failure.ToString()),
            };
            Raise(new StopCard(reason, StopHandOff.Travel)
            {
                Title = Format(Strings.StopCardTitle(reason), gaveUp.WalkOnly ? Strings.TravelWalk : Strings.TravelGoTo),
                Why = why,
                Context = Join(gaveUp.Target.Length > 0 ? Format(Strings.StopContextTravelFormat, gaveUp.Target) : string.Empty, zone),
                TerritoryId = clientState.TerritoryType,
                Position = objects.LocalPlayer?.Position,
                ReportReason = "travel · " + gaveUp.Failure,
            });
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "The travel stop card failed");
        }
    }

    /// <summary>The quest a travel card's walk headed for, for Teleport closer; null for another card.</summary>
    private QuestRecord? TravelQuest(StopCard card) =>
        card.HandOff == StopHandOff.Travel && card.Reason == StopReason.NoPath && lastGaveUpQuest != 0 ? Quest(lastGaveUpQuest) : null;

    // ------------------------------------------------------------------ words

    private string ReceiptLine(QuestionableRunReceipt receipt)
    {
        var count = receipt.Completed.Count;
        var quests = count switch
        {
            0 => Strings.QuestionableRunNoQuests,
            1 => Strings.StopReceiptOneQuest,
            _ => Format(Strings.QuestionableRunQuestsFormat, count),
        };
        return Format(Strings.StopReceiptFormat, QuestionableRunWatch.DurationText(receipt.Duration), quests);
    }

    /// <summary>The quest the finished quest leads to that the character can take now and Questionable has a path for; 0 for none.</summary>
    private uint NextQuest(QuestionableRunReceipt receipt)
    {
        var finished = receipt.QuestRowId != 0 ? receipt.QuestRowId : receipt.Completed.Count > 0 ? receipt.Completed[^1] : 0;
        if (session.Bundle is not { } bundle || finished == 0)
        {
            return 0;
        }

        var states = session.LiveStates;
        return RunStopClassifier.NextQuest(bundle.Catalog, finished, rowId =>
            states.TryGetValue(rowId, out var evaluation) && evaluation.State == QuestState.Ready && QuestionableCrossCheck.QuestionableId(rowId) is not null);
    }

    private static string ReportReason(StopReason reason, QuestionableRunEnd end) => reason switch
    {
        StopReason.Finished => "finished · " + end,
        StopReason.Player => "stopped from Tsukimichi",
        StopReason.DutyGuard => "duty guard · no Duty Support or Trust",
        StopReason.KnockedOut => "knocked out",
        StopReason.Stuck => "stuck · no progress",
        StopReason.MissingPlugin => "missing plugin · AutoDuty for a duty step",
        _ => "error · stopped on its own or from its own window",
    };

    private string StepContext(uint rowId, int? step)
    {
        if (rowId == 0)
        {
            return string.Empty;
        }

        var name = QuestName(rowId);
        return step is { } s ? Format(Strings.StopContextStepFormat, name, s + 1) : name;
    }

    private static string Join(string first, string second) =>
        first.Length == 0 ? second : second.Length == 0 ? first : first + Strings.StopCardSeparator + second;

    private string QuestName(uint rowId) =>
        rowId == 0 ? string.Empty : Actions?.QuestName(rowId) ?? Quest(rowId)?.Name ?? rowId.ToString(CultureInfo.InvariantCulture);

    private QuestRecord? Quest(uint rowId) => rowId == 0 ? null : session.Bundle?.Catalog.GetByRowId(rowId);

    private static string HandOffName(StopHandOff handOff) => handOff switch
    {
        StopHandOff.AutoDuty => AutoDutyName,
        StopHandOff.Artisan => ArtisanName,
        StopHandOff.Travel => Strings.StopHandOffTravel,
        _ => QuestionableName,
    };

    private string ZoneName(uint territoryId)
    {
        if (territoryId == 0)
        {
            return string.Empty;
        }

        try
        {
            return data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId)?.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "A zone name could not be read");
            return string.Empty;
        }
    }

    private string PluginVersion(string internalName)
    {
        foreach (var plugin in pluginInterface.InstalledPlugins)
        {
            if (string.Equals(plugin.InternalName, internalName, StringComparison.OrdinalIgnoreCase))
            {
                return plugin.Version.ToString();
            }
        }

        return string.Empty;
    }

    /// <summary>"vnavmesh 0.4.3 · Lifestream 2.5.1 · movement Standard": the travel plugins loaded and the movement type.</summary>
    private string TravelLine()
    {
        var parts = new List<string>(3);
        foreach (var name in (ReadOnlySpan<string>)[VnavmeshIpc.PluginInternalName, "Lifestream"])
        {
            var version = PluginVersion(name);
            if (version.Length > 0)
            {
                parts.Add(name + " " + version);
            }
        }

        if (Preflight is { } preflight)
        {
            foreach (var result in preflight.Results)
            {
                if (result.Item == PreflightItem.MovementType)
                {
                    parts.Add(result.State == PreflightState.Warn ? "movement Legacy" : "movement Standard");
                }
            }
        }

        return string.Join(" · ", parts);
    }

    private static string Format(string format, params object[] args) => string.Format(CultureInfo.CurrentCulture, format, args);

    private void WarnOnce(Exception ex, string message)
    {
        if (warned)
        {
            log.Debug(ex, message);
            return;
        }

        warned = true;
        log.Warning(ex, message);
    }

    /// <summary>What "Reload navmesh and retry" starts once vnavmesh is ready: a travel plan, or Questionable on a quest.</summary>
    private sealed record RetryPlan(GoToGiverPlan? Plan, string Target, uint QuestRowId, bool Single)
    {
        public bool Questionable { get; init; }

        public double AskedAt { get; init; }
    }
}
