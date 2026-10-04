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
/// nothing, per <see cref="Configuration.QuestionableDutyGuard"/>, with one chat line and the alert below. A few seconds
/// after a stop, a character still in the Duty Finder queue is told so: Tsukimichi never withdraws for the player.</item>
/// <item><b>A5, "Needs you".</b> While a hand-off runs (Questionable, or a Go to giver, Lifestream task, AutoDuty run or
/// Artisan craft Tsukimichi started), the character dying, standing still while vnavmesh says it moves, a duty pop
/// (<see cref="IClientState.CfPop"/>) and an incoming tell each raise one chat line, a chat sound effect and a toast,
/// each kind and channel per Settings › Alerts › While automation runs, rate-limited by <see cref="NeedsYouWatch"/>.
/// Nothing is read or raised while no hand-off runs; a tell is only noticed, never answered or hidden.</item>
/// </list>
/// The sound and the toast are game calls, so they wait for the hook gate. Runs on the framework thread.
/// </summary>
public sealed class RunWatch : IDisposable
{
    /// <summary>The chat sound effect a "Needs you" alert plays (<c>&lt;se.6&gt;</c>).</summary>
    public const uint AlertSound = 6;

    /// <summary>After a guard stop, how long to wait before checking whether the character is still queued.</summary>
    public const double QueueCheckSeconds = 3.0;

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IObjectTable objects;
    private readonly IChatGui chat;
    private readonly IToastGui toasts;
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
    private bool disposed;

    public RunWatch(
        IFramework framework,
        IClientState clientState,
        ICondition condition,
        IObjectTable objects,
        IChatGui chat,
        IToastGui toasts,
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
        this.toasts = toasts ?? throw new ArgumentNullException(nameof(toasts));
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
        clientState.CfPop -= OnCfPop;
        chat.ChatMessage -= OnChatMessage;
    }

    /// <summary>The Questionable run receipts (A4): a guard stop is their reason. Set by the plugin; null records nothing.</summary>
    public QuestionableRunWatch? Runs { get; set; }

    private double Now => clock.Elapsed.TotalSeconds;

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
        var moving = handOff && (enabled & NeedsYouKind.Stuck) != 0 && travel.Vnavmesh.Available && travel.Vnavmesh.IsWalking;
        var frame = new NeedsYouFrame(handOff, player?.IsDead == true, moving, player?.Position, now);
        var raised = needsYou.Tick(frame, enabled);
        if ((raised & NeedsYouKind.Death) != 0)
        {
            Alert(Strings.NeedsYouDeathLine, Strings.NeedsYouToastDeath, now);
        }

        if ((raised & NeedsYouKind.Stuck) != 0)
        {
            Alert(string.Format(CultureInfo.CurrentCulture, Strings.NeedsYouStuckFormat, (int)NeedsYouWatch.StuckSeconds), Strings.NeedsYouToastStuck, now);
        }
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
        var dutyName = hidden || verdict.Duty is not { } duty ? Strings.DutyGuardHiddenDuty : duty.Name;
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
                Runs?.NoteDutyGuardStop();
                queueCheckAt = now + QueueCheckSeconds;
                Alert(
                    verdict.Certain
                        ? string.Format(CultureInfo.CurrentCulture, Strings.DutyGuardStopFormat, dutyName)
                        : string.Format(CultureInfo.CurrentCulture, Strings.DutyGuardStopMaybeFormat, dutyName, questName),
                    Strings.DutyGuardToastStop,
                    now);
                break;
            case DutyGuardAction.Stop:
                Alert(string.Format(CultureInfo.CurrentCulture, Strings.DutyGuardNoStopFormat, dutyName), Strings.DutyGuardToastWarn, now);
                break;
            case DutyGuardAction.Warn:
                Alert(
                    verdict.Certain
                        ? string.Format(CultureInfo.CurrentCulture, Strings.DutyGuardWarnFormat, dutyName)
                        : string.Format(CultureInfo.CurrentCulture, Strings.DutyGuardWarnMaybeFormat, dutyName, questName),
                    Strings.DutyGuardToastWarn,
                    now);
                break;
            default:
                Alert(string.Format(CultureInfo.CurrentCulture, Strings.DutyGuardUnsureFormat, questName), Strings.DutyGuardToastWarn, now);
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

    private void OnCfPop(ContentFinderCondition duty)
    {
        try
        {
            if (!needsYou.Event(NeedsYouKind.DutyPop, handOff, Enabled, Now))
            {
                return;
            }

            var name = duty.Name.ExtractText().Trim();
            Alert(name.Length > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.NeedsYouDutyPopFormat, name) : Strings.NeedsYouDutyPopUnnamed, Strings.NeedsYouToastDutyPop, Now);
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

            var sender = message.Sender.TextValue.Trim();
            Alert(sender.Length > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.NeedsYouTellFormat, sender) : Strings.NeedsYouTellUnnamed, Strings.NeedsYouToastTell, Now);
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Tell alert failed");
        }
    }

    /// <summary>One chat line, then the toast and the sound when Settings allows them and the hook gate is open.</summary>
    private void Alert(string line, string toast, double now)
    {
        chat.Print(line, Strings.ChatTag);
        if (!gate.HooksAllowed)
        {
            return;
        }

        if (config.NeedsYouToast)
        {
            toasts.ShowError(toast);
        }

        if (config.NeedsYouSound && needsYou.TakeSound(now))
        {
            UIGlobals.PlayChatSoundEffect(AlertSound);
        }
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
