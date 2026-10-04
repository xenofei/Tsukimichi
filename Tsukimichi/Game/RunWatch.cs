using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using Tsukimichi.Config;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Runtime;
using Tsukimichi.GameData;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// Runs you can trust (plan v7, 1.18.0): watches the hand-offs while they run.
/// <list type="bullet">
/// <item><b>A3, the duty guard.</b> While Questionable runs, its step is read (<see cref="QuestionableIpc.PollStatus"/>,
/// at most once a second). On a <c>Duty</c> step whose duty has no Duty Support or Trust (<see cref="DutyGuard"/>, the
/// duty read from the quest's script by <see cref="QuestScriptDuties.NamedRuns"/>) it stops Questionable
/// (<c>Questionable.Stop("Tsukimichi")</c>, which also stops the AutoDuty run Questionable started), warns, or does
/// nothing, per <see cref="Configuration.QuestionableDutyGuard"/>, with one chat line and the alert below. A step that
/// may also be a duty with Duty Support or Trust (a quest naming several) is only warned about, and its line names no
/// duty (a later one would be a spoiler). A few seconds after a stop, a character still in the Duty Finder queue is told
/// so: Tsukimichi never withdraws for the player. When Questionable's step data (or its step kind) cannot be read, the
/// guard cannot act: one chat line says so, once, and Settings' guard row shows it.</item>
/// <item><b>A5, "Needs you".</b> While a hand-off runs (Questionable, or a Go to giver, Lifestream task, AutoDuty run or
/// Artisan craft Tsukimichi started), the character dying, standing still while vnavmesh says it moves (a pathfind still
/// pending is progress; a cutscene, talk or loading screen is no stall), a Walk or Go to giver whose recovery gave up
/// (<see cref="TravelService.GaveUp"/>), a duty pop (<see cref="IClientState.CfPop"/>) and an incoming tell each raise
/// one chat line ("Needs you:" in bold), the calm panel over the game (<see cref="RunStops.NeedsYou"/>), the kind's chat
/// sound effect and a taskbar flash while the game's window is not in front, each per Settings › Alerts › While
/// automation runs, rate-limited by <see cref="NeedsYouWatch"/>. On a knock-out or a stall Tsukimichi stops its own
/// hand-offs (and Questionable, when the player opted in), and the "Why it stopped" card (A2) says so; inside a duty it
/// stops neither AutoDuty nor Questionable (the NPC healers raise the character), and only alerts. A tell is only
/// noticed, never answered or hidden, and neither its sender nor its text is repeated. Tsukimichi never commences a
/// duty.</item>
/// </list>
/// Questionable's status is asked at most once a second whenever Questionable is loaded and the guard or any alert is
/// on, run or not (a run is noticed by asking); nothing else is read or raised while no hand-off runs. Only the sound
/// waits for the hook gate (a game call); the chat line, the panel and the flash do not. Runs on the framework thread.
/// </summary>
public sealed class RunWatch : IDisposable
{
    private const string QuestionableName = "Questionable";
    private const string AutoDutyName = "AutoDuty";
    private const string ArtisanName = "Artisan";
    private const string LifestreamName = "Lifestream";

    /// <summary>After a guard stop, how long to wait before checking whether the character is still queued.</summary>
    public const double QueueCheckSeconds = 3.0;

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IObjectTable objects;
    private readonly IChatGui chat;
    private readonly IDataManager data;
    private readonly Configuration config;
    private readonly SessionState session;
    private readonly QuestionableIpc questionable;
    private readonly TravelService travel;
    private readonly LifestreamIpc lifestream;
    private readonly AutoDutyIpc autoDuty;
    private readonly ArtisanIpc? artisan;
    private readonly Func<DutyRunIndex?> dutyRuns;
    private readonly HookGate gate;
    private readonly IPluginLog log;

    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly NeedsYouWatch needsYou = new();
    private readonly DutyGuardWatch guard = new();

    // The guard's lookups, made once so the per-frame look at Questionable's step allocates nothing.
    private readonly Func<uint, IReadOnlyList<DutyRunInfo>> dutiesFor;
    private readonly Func<DutyRunInfo, bool> cleared = Cleared;

    // The duties each quest's script names, read once per quest and per duty index.
    private readonly Dictionary<uint, IReadOnlyList<DutyRunInfo>> questDuties = [];
    private DutyRunIndex? questDutiesIndex;

    private bool handOff;
    private double? queueCheckAt;
    private ulong? contentId;
    private bool warned;
    private bool blindSaid;
    private bool disposed;

    public RunWatch(
        IFramework framework,
        IClientState clientState,
        ICondition condition,
        IObjectTable objects,
        IChatGui chat,
        IDataManager data,
        Configuration config,
        SessionState session,
        QuestionableIpc questionable,
        TravelService travel,
        LifestreamIpc lifestream,
        AutoDutyIpc autoDuty,
        ArtisanIpc? artisan,
        Func<DutyRunIndex?> dutyRuns,
        HookGate gate,
        IPluginLog log)
    {
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
        this.objects = objects ?? throw new ArgumentNullException(nameof(objects));
        this.chat = chat ?? throw new ArgumentNullException(nameof(chat));
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.questionable = questionable ?? throw new ArgumentNullException(nameof(questionable));
        this.travel = travel ?? throw new ArgumentNullException(nameof(travel));
        this.lifestream = lifestream ?? throw new ArgumentNullException(nameof(lifestream));
        this.autoDuty = autoDuty ?? throw new ArgumentNullException(nameof(autoDuty));
        this.artisan = artisan;
        this.dutyRuns = dutyRuns ?? throw new ArgumentNullException(nameof(dutyRuns));
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        dutiesFor = DutiesFor;
        framework.Update += OnUpdate;
        travel.GaveUp += OnTravelGaveUp;
        clientState.CfPop += OnCfPop;
        chat.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        framework.Update -= OnUpdate;
        travel.GaveUp -= OnTravelGaveUp;
        clientState.CfPop -= OnCfPop;
        chat.ChatMessage -= OnChatMessage;
    }

    /// <summary>The Questionable run receipts (A4): a guard stop is their reason. Set by the plugin; null records nothing.</summary>
    public QuestionableRunWatch? Runs { get; set; }

    /// <summary>The "Why it stopped" card and the "Needs you" panel. Set by the plugin; null shows neither.</summary>
    public RunStops? Stops { get; set; }

    private double Now => clock.Elapsed.TotalSeconds;

    /// <summary>
    /// The game holds the character: a cutscene, a loading screen, or a talk, a quest event or another occupied state
    /// (the same conditions travel's refusal reasons read). Standing still then is no stall.
    /// </summary>
    private bool Held =>
        travel.InCutscene || travel.BetweenAreas
        || condition[ConditionFlag.OccupiedInQuestEvent] || condition[ConditionFlag.OccupiedInEvent] || condition[ConditionFlag.Occupied]
        || condition[ConditionFlag.Occupied30] || condition[ConditionFlag.Occupied33] || condition[ConditionFlag.Occupied38]
        || condition[ConditionFlag.Occupied39] || condition[ConditionFlag.OccupiedSummoningBell];

    /// <summary>The "Needs you" kinds the player left on.</summary>
    private NeedsYouKind Enabled =>
        (config.NeedsYouDeath ? NeedsYouKind.Death : NeedsYouKind.None)
        | (config.NeedsYouStuck ? NeedsYouKind.Stuck : NeedsYouKind.None)
        | (config.NeedsYouDutyPop ? NeedsYouKind.DutyPop : NeedsYouKind.None)
        | (config.NeedsYouTell ? NeedsYouKind.Tell : NeedsYouKind.None);

    private void OnUpdate(IFramework _)
    {
        try
        {
            Tick();
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Run watch failed");
        }
    }

    private void Tick()
    {
        if (!clientState.IsLoggedIn)
        {
            handOff = false;
            return;
        }

        if (session.LiveContentId != contentId)
        {
            // Another character: the step acted on was the last one's.
            contentId = session.LiveContentId;
            guard.Reset();
        }

        var now = Now;
        var enabled = Enabled;
        var mode = config.QuestionableDutyGuard;

        // Questionable is asked only while something here would use the answer.
        var status = (mode != DutyGuardMode.Nothing || enabled != NeedsYouKind.None) && questionable.Available
            ? questionable.PollStatus()
            : QuestionableStatus.Idle;
        if (status.Running && mode != DutyGuardMode.Nothing)
        {
            Guard(status, mode, now);
            if (!blindSaid && questionable.StepKindUnreadable)
            {
                // The guard cannot see the step's kind: said once in chat (and in Settings' guard row), not only logged.
                blindSaid = true;
                log.Warning("Duty guard: Questionable's step kind cannot be read (step data unreadable {Unreadable}); the guard cannot act before a duty", questionable.StepDataUnreadable);
                chat.Print(Strings.DutyGuardBlindChat, Strings.ChatTag);
            }
        }

        handOff = status.Running
            || travel.JourneyActive
            || lifestream.HandOffClaimed
            || autoDuty.HandOffClaimed
            || artisan is { HandOffClaimed: true };

        if (queueCheckAt is { } at && now >= at)
        {
            queueCheckAt = null;
            if (condition[ConditionFlag.InDutyQueue])
            {
                chat.Print(Strings.DutyGuardStillQueued, Strings.ChatTag);
            }
        }

        if (enabled == NeedsYouKind.None)
        {
            return;
        }

        var player = objects.LocalPlayer;
        // A Walk or Go to giver of Tsukimichi's watches its own walk and recovers first (a new path, then a navmesh
        // reload, feature plan v7 A8); its stall raises the alert only when that recovery gives up (OnTravelGaveUp).
        // A pathfind still pending is progress, and a cutscene, talk or loading screen holds the character: no stall.
        var moving = handOff && (enabled & NeedsYouKind.Stuck) != 0 && !travel.JourneyActive && travel.Vnavmesh.Available && travel.Vnavmesh.IsWalking;
        var frame = new NeedsYouFrame(handOff, player?.IsDead == true, moving, player?.Position, now, moving && travel.Vnavmesh.IsPathfinding, moving && Held);
        var raised = needsYou.Tick(frame, enabled);
        if ((raised & NeedsYouKind.Death) != 0)
        {
            Trouble(StopReason.KnockedOut, status, now);
        }

        if ((raised & NeedsYouKind.Stuck) != 0)
        {
            Trouble(StopReason.Stuck, status, now);
        }
    }

    /// <summary>
    /// A knock-out or a stall during a run: Tsukimichi stops its own hand-offs (Settings), then one chat line, the panel,
    /// the sound and the flash, and the "Why it stopped" card.
    /// </summary>
    private void Trouble(StopReason reason, QuestionableStatus status, double now)
    {
        var handOff = status.Running ? StopHandOff.Questionable
            : travel.JourneyActive ? StopHandOff.Travel
            : autoDuty.HandOffClaimed ? StopHandOff.AutoDuty
            : artisan is { HandOffClaimed: true } ? StopHandOff.Artisan
            : StopHandOff.Travel;
        var (stopped, questionableLeft) = StopForTrouble(status);
        var did = RunStops.DidSentence(stopped, questionableLeft);
        var seconds = (int)NeedsYouWatch.StuckSeconds;
        if (reason == StopReason.KnockedOut)
        {
            var quest = status.RowId is { } rowId ? QuestDisplayName(rowId) : null;
            var chatLine = quest is not null
                ? string.Format(CultureInfo.CurrentCulture, Strings.NeedsYouDeathDuringChatFormat, quest, did)
                : string.Format(CultureInfo.CurrentCulture, Strings.NeedsYouDeathChatFormat, did);
            Alert(NeedsYouKind.Death, chatLine, new NeedsYouAlert(NeedsYouKind.Death, Strings.NeedsYouTitle(NeedsYouKind.Death), did), now);
        }
        else
        {
            Alert(
                NeedsYouKind.Stuck,
                string.Format(CultureInfo.CurrentCulture, Strings.NeedsYouStuckChatFormat, seconds, did),
                new NeedsYouAlert(NeedsYouKind.Stuck, Strings.NeedsYouTitle(NeedsYouKind.Stuck), string.Format(CultureInfo.CurrentCulture, Strings.NeedsYouStuckLineFormat, seconds, did)),
                now);
        }

        Stops?.RaiseTrouble(reason, handOff, did);
    }

    /// <summary>
    /// Stops Tsukimichi's own hand-offs (a walk, a Lifestream task, an AutoDuty run, an Artisan craft), when Settings
    /// says to, and Questionable only when the player opted in (<see cref="StopAll.OnTrouble"/>). Inside a duty neither
    /// AutoDuty nor Questionable is stopped: the NPC healers raise the character and the run goes on, so the alert is
    /// all it gets. Returns the names stopped and whether Questionable runs on.
    /// </summary>
    private (List<string> Stopped, bool QuestionableLeft) StopForTrouble(QuestionableStatus status)
    {
        var stopped = new List<string>(4);
        var running = (travel.JourneyActive ? StopTarget.Travel : StopTarget.None)
            | (lifestream.HandOffClaimed ? StopTarget.Lifestream : StopTarget.None)
            | (status.Running ? StopTarget.Questionable : StopTarget.None)
            | (autoDuty.HandOffClaimed ? StopTarget.AutoDuty : StopTarget.None)
            | (artisan is { HandOffClaimed: true } ? StopTarget.Artisan : StopTarget.None);
        var inDuty = travel.InDuty;
        var stop = StopAll.OnTrouble(running, config.NeedsYouStopHandOffs, config.NeedsYouStopQuestionable, inDuty);
        if (stop == StopTarget.None)
        {
            log.Information("Needs you: stopped nothing (in a duty {InDuty}); Questionable left running {Left}", inDuty, status.Running);
            return (stopped, status.Running);
        }

        if ((stop & StopTarget.Travel) != 0)
        {
            var name = travel.JourneyIsWalkOnly ? Strings.TravelWalk : Strings.TravelGoTo;
            if (travel.Stop())
            {
                stopped.Add(name);
            }
        }

        if ((stop & StopTarget.Lifestream) != 0 && lifestream.Abort())
        {
            stopped.Add(LifestreamName);
        }

        if ((stop & StopTarget.AutoDuty) != 0 && autoDuty.Stop())
        {
            stopped.Add(AutoDutyName);
        }

        if ((stop & StopTarget.Artisan) != 0 && artisan is { } crafting && crafting.Stop())
        {
            stopped.Add(ArtisanName);
        }

        var left = status.Running;
        if ((stop & StopTarget.Questionable) != 0 && questionable.CanStop && questionable.Stop())
        {
            stopped.Add(QuestionableName);
            left = false;
        }

        log.Information("Needs you: stopped {Stopped} (in a duty {InDuty}); Questionable left running {Left}", string.Join(", ", stopped), inDuty, left);
        return (stopped, left);
    }

    /// <summary>The quest's name as the live character's spoiler shield shows it.</summary>
    private string QuestDisplayName(uint rowId)
    {
        var quest = session.Bundle?.Catalog.GetByRowId(rowId);
        return quest is null ? QuestionableCrossCheck.QuestionableId(rowId) ?? rowId.ToString(CultureInfo.InvariantCulture) : session.LiveSpoilers.DisplayName(quest);
    }

    /// <summary>A3: one look at Questionable's step; acts at most once per step (<see cref="DutyGuardWatch"/>).</summary>
    private void Guard(QuestionableStatus status, DutyGuardMode mode, double now)
    {
        var verdict = guard.Observe(mode, status.RowId, status.Sequence, status.Step, status.Interaction, dutiesFor, gate.HooksAllowed ? cleared : null);
        if (verdict.Action == DutyGuardAction.None || status.RowId is not { } rowId)
        {
            return;
        }

        var quest = session.Bundle?.Catalog.GetByRowId(rowId);
        var hidden = quest is not null && session.LiveSpoilers.IsMasked(quest);
        var questName = quest is null ? QuestionableCrossCheck.QuestionableId(rowId) ?? rowId.ToString(CultureInfo.InvariantCulture) : session.LiveSpoilers.DisplayName(quest);

        // An uncertain step may be a later duty of the quest: it is never named (a spoiler), only "the next duty".
        var dutyName = hidden || !verdict.Certain || verdict.Duty is not { } duty ? Strings.DutyGuardHiddenDuty : duty.Name;
        log.Information(
            "Duty guard: {Action} at quest {RowId} sequence {Sequence} step {Step}, duty {Duty} (certain {Certain})",
            verdict.Action,
            rowId,
            status.Sequence ?? 0,
            status.Step ?? 0,
            verdict.Duty?.ContentFinderConditionId ?? 0,
            verdict.Certain);

        switch (verdict.Action)
        {
            case DutyGuardAction.Stop when questionable.CanStop && questionable.Stop():
                // Only a certain step is stopped (DutyGuard.Decide): the line names its duty unless the shield hides it.
                Runs?.NoteDutyGuardStop();
                queueCheckAt = now + QueueCheckSeconds;
                GuardLine(string.Format(CultureInfo.CurrentCulture, Strings.DutyGuardStopFormat, dutyName), now);
                Stops?.RaiseDutyGuard(status, verdict.Duty, hidden ? string.Empty : dutyName);
                break;
            case DutyGuardAction.Stop:
                GuardLine(string.Format(CultureInfo.CurrentCulture, Strings.DutyGuardNoStopFormat, dutyName), now);
                break;
            case DutyGuardAction.Warn:
                GuardLine(
                    verdict.Certain
                        ? string.Format(CultureInfo.CurrentCulture, Strings.DutyGuardWarnFormat, dutyName)
                        : string.Format(CultureInfo.CurrentCulture, Strings.DutyGuardWarnNextFormat, questName),
                    now);
                break;
            default:
                GuardLine(string.Format(CultureInfo.CurrentCulture, Strings.DutyGuardUnsureFormat, questName), now);
                break;
        }
    }

    /// <summary>The duties a quest's script names, read once per quest; empty until the duty index has been built.</summary>
    private IReadOnlyList<DutyRunInfo> DutiesFor(uint rowId)
    {
        if (dutyRuns() is not { } index)
        {
            return [];
        }

        if (!ReferenceEquals(index, questDutiesIndex))
        {
            questDutiesIndex = index;
            questDuties.Clear();
        }

        if (!questDuties.TryGetValue(rowId, out var duties))
        {
            duties = QuestScriptDuties.NamedRuns(data.Excel, data.Language.ToLumina(), rowId, index);
            questDuties[rowId] = duties;
        }

        return duties;
    }

    /// <summary>Whether the character has cleared the duty (the game's own record); asked only while the hook gate allows.</summary>
    private static bool Cleared(DutyRunInfo duty) => duty.InstanceContentId != 0 && UIState.IsInstanceContentCompleted(duty.InstanceContentId);

    /// <summary>
    /// A Walk or Go to giver whose recovery gave up: the stuck alert (chat, panel with "Reload navmesh and retry",
    /// sound, flash), rate-limited like the others. Raised through <see cref="NeedsYouWatch.Raise"/>: the frame's
    /// <see cref="NeedsYouWatch.Event"/> takes only pops and tells.
    /// </summary>
    private void OnTravelGaveUp(TravelGaveUp gaveUp)
    {
        try
        {
            if (!needsYou.Raise(NeedsYouKind.Stuck, Enabled, Now))
            {
                return;
            }

            Alert(
                NeedsYouKind.Stuck,
                Strings.NeedsYouTravelGaveUp,
                new NeedsYouAlert(NeedsYouKind.Stuck, Strings.NeedsYouTitle(NeedsYouKind.Stuck), Strings.NeedsYouTravelGaveUpLine) { Fix = StopFix.ReloadAndRetry },
                Now);
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Travel stuck alert failed");
        }
    }

    private void OnCfPop(ContentFinderCondition duty)
    {
        try
        {
            if (!needsYou.Event(NeedsYouKind.DutyPop, handOff, Enabled, Now))
            {
                return;
            }

            var name = duty.Name.ExtractText().Trim();
            Alert(
                NeedsYouKind.DutyPop,
                name.Length > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.NeedsYouDutyPopChatFormat, name) : Strings.NeedsYouDutyPopChat,
                new NeedsYouAlert(
                    NeedsYouKind.DutyPop,
                    Strings.NeedsYouTitle(NeedsYouKind.DutyPop),
                    name.Length > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.NeedsYouDutyPopLineFormat, name) : Strings.NeedsYouDutyPopLine),
                Now);
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Duty pop alert failed");
        }
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        try
        {
            if (message.LogKind != XivChatType.TellIncoming || !needsYou.Event(NeedsYouKind.Tell, handOff, Enabled, Now))
            {
                return;
            }

            // Neither the sender nor the text is repeated anywhere (spec-1.18 A5).
            Alert(NeedsYouKind.Tell, Strings.NeedsYouTellChat, new NeedsYouAlert(NeedsYouKind.Tell, Strings.NeedsYouTitle(NeedsYouKind.Tell), Strings.NeedsYouTellLine), Now);
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Tell alert failed");
        }
    }

    /// <summary>
    /// One "Needs you" alert: the chat line ("Needs you:" in bold, then <paramref name="chatLine"/>, in the plugin's own
    /// echo channel), the panel over the game, the kind's sound and the taskbar flash.
    /// </summary>
    private void Alert(NeedsYouKind kind, string chatLine, NeedsYouAlert panel, double now)
    {
        PrintNeedsYou(chatLine);
        Stops?.Alert(panel);
        Signal(kind, now);
    }

    /// <summary>A duty guard line (A3): the chat line, Duty ready's sound and the flash; the card says the rest.</summary>
    private void GuardLine(string line, double now)
    {
        chat.Print(line, Strings.ChatTag);
        Signal(NeedsYouKind.DutyPop, now);
    }

    /// <summary>The kind's chat sound effect (none for 0; at most once every 10 s; while the hook gate allows) and the taskbar flash.</summary>
    private void Signal(NeedsYouKind kind, double now)
    {
        var sound = SoundFor(config, kind);
        if (sound > 0 && gate.HooksAllowed && needsYou.TakeSound(now))
        {
            UIGlobals.PlayChatSoundEffect((uint)sound);
        }

        if (config.NeedsYouFlash)
        {
            TaskbarFlash.FlashIfBackground();
        }
    }

    /// <summary>The chat sound effect Settings picked for <paramref name="kind"/> (1–16), 0 for none.</summary>
    public static int SoundFor(Configuration config, NeedsYouKind kind)
    {
        ArgumentNullException.ThrowIfNull(config);
        return kind switch
        {
            NeedsYouKind.Death => config.NeedsYouSoundDeath,
            NeedsYouKind.Stuck => config.NeedsYouSoundStuck,
            NeedsYouKind.DutyPop => config.NeedsYouSoundDutyPop,
            NeedsYouKind.Tell => config.NeedsYouSoundTell,
            _ => 0,
        };
    }

    /// <summary>Plays chat sound effect <paramref name="sound"/> now (Settings' Test); false while the hook gate pauses game calls.</summary>
    public bool TestSound(int sound)
    {
        if (sound is < 1 or > Configuration.MaxNeedsYouSound || !gate.HooksAllowed)
        {
            return false;
        }

        UIGlobals.PlayChatSoundEffect((uint)sound);
        return true;
    }

    /// <summary>"Needs you:" in the chat channel's bold, then the rest in its normal colour, after the plugin's gold tag.</summary>
    private void PrintNeedsYou(string rest)
    {
        var builder = new Lumina.Text.SeStringBuilder();
        builder.AppendBold(Strings.NeedsYouLead);
        builder.Append(" ");
        builder.Append(rest);
        chat.Print(builder.ToArray(), Strings.ChatTag);
    }

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
}
