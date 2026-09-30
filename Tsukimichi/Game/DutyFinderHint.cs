using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unique;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// The Duty Finder unlock hint (P13), game side: while the game's Duty Finder (<c>ContentsFinder</c> addon, the Raid
/// Finder is the same addon on another tab) is open, follows the duty the player selected
/// (<c>AgentContentsFinder.SelectedDuty</c>, a <c>ContentsId</c> whose <c>Id</c> is a ContentFinderCondition row
/// for <c>ContentsType.Regular</c>) and whether the logged-in character has it unlocked
/// (<c>UIState.IsInstanceContentUnlocked</c> of the InstanceContent row the condition links). When the duty is locked and
/// <see cref="DutyUnlockIndex"/> knows the quests that unlock it, <see cref="Current"/> hands
/// <see cref="DutyFinderPanel"/> a model to draw: the duty name and, per quest, its state and
/// <see cref="BlockerText.StatusText"/> for the logged-in character (spoiler-masked like every in-world surface).
/// <para>
/// Selection, not hover: tree-list rows are recycled nodes, so a hovered row cannot be mapped back to a duty reliably
/// (Dalamud developer review §1 P13). The listeners are registered on <see cref="IAddonLifecycle"/> for
/// <c>ContentsFinder</c> only: PostSetup and PreFinalize track the window, PostRefresh and PostReceiveEvent re-read the
/// selection at once (a click, a tab change), and PostUpdate re-reads it at most every <see cref="PollInterval"/> for the
/// changes no event announces (keyboard or pad selection, an unlock arriving). Every read is on the framework thread
/// (the lifecycle calls there) and is a struct field and one static call, so the per-frame cost is two comparisons.
/// The model is rebuilt only when the selected duty, its lock, the session version or the index changes.
/// </para>
/// <para>
/// Behind the addon kill switch (T20): the listeners are registered only while <see cref="Enabled"/> (Settings ›
/// Integrations › "Duty Finder unlock hint") is on and the shared <see cref="HookGate"/> allows game hooks, and follow
/// its <see cref="HookGate.Changed"/>; while paused nothing here touches game memory. Informational only: it never
/// opens or queues a duty (<c>AgentContentsFinder.OpenRegularDuty</c> is deliberately not called).
/// </para>
/// </summary>
public sealed unsafe class DutyFinderHint : IDisposable
{
    /// <summary>Name of the game's Duty Finder addon.</summary>
    public const string AddonName = "ContentsFinder";

    /// <summary>Quests listed before the panel folds the rest into "and N more".</summary>
    public const int MaxQuests = 3;

    /// <summary>How often PostUpdate re-reads the selection, in milliseconds.</summary>
    public const long PollInterval = 200;

    /// <summary><c>ContentFinderCondition.ContentLinkType</c> value whose <c>Content</c> is an InstanceContent row.</summary>
    private const byte InstanceContentLink = 1;

    private static readonly DutyHintQuest[] NoQuests = [];

    private readonly IAddonLifecycle lifecycle;
    private readonly IGameGui gameGui;
    private readonly IDataManager data;
    private readonly SessionState session;
    private readonly DutyUnlockIndexSource index;
    private readonly HookGate gate;
    private readonly IPluginLog log;
    private readonly IAddonLifecycle.AddonEventDelegate onSetup;
    private readonly IAddonLifecycle.AddonEventDelegate onChange;
    private readonly IAddonLifecycle.AddonEventDelegate onUpdate;
    private readonly IAddonLifecycle.AddonEventDelegate onFinalize;
    private readonly List<DutyHintQuest> quests = [];

    private Dictionary<uint, (uint Instance, string Name)>? duties;
    private bool enabled = true;
    private bool registered;
    private bool disposed;
    private bool warned;

    // What the game shows now (framework thread).
    private bool open;
    private uint selected;
    private bool locked;
    private long lastPoll;

    // What the model was built from.
    private uint modelCondition;
    private bool modelLocked;
    private int modelVersion = -1;
    private DutyUnlockIndex? modelIndex;
    private DutyHintModel? model;

    /// <param name="gate">The shared addon kill switch (T20): while it pauses game hooks the listeners are not registered.</param>
    public DutyFinderHint(IAddonLifecycle lifecycle, IGameGui gameGui, IDataManager data, SessionState session, DutyUnlockIndexSource index, HookGate gate, IPluginLog log)
    {
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.gameGui = gameGui ?? throw new ArgumentNullException(nameof(gameGui));
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.index = index ?? throw new ArgumentNullException(nameof(index));
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        onSetup = OnSetup;
        onChange = OnChange;
        onUpdate = OnUpdate;
        onFinalize = OnFinalize;
        gate.Changed += Apply;
        Apply();
    }

    /// <summary>Follows <c>Configuration.DutyFinderHintEnabled</c>; off unregisters the listeners and forgets the selection.</summary>
    public bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled == value || disposed)
            {
                return;
            }

            enabled = value;
            Apply();
        }
    }

    /// <summary>Whether the listeners are registered now: the setting is on and the <see cref="HookGate"/> allows game hooks.</summary>
    public bool IsActive => registered;

    /// <summary>
    /// The panel's model for this frame, or null when there is nothing to say: the hint is off or paused, the Duty
    /// Finder is closed, no duty (or a roulette) is selected, the duty is unlocked or its lock cannot be read, or no
    /// quest is known to unlock it. Framework thread; allocation-free unless an input changed.
    /// </summary>
    public DutyHintModel? Current()
    {
        if (!registered || !open || selected == 0 || !locked)
        {
            return null;
        }

        var currentIndex = index.Current;
        if (selected != modelCondition || locked != modelLocked || session.Version != modelVersion || !ReferenceEquals(currentIndex, modelIndex))
        {
            Rebuild(currentIndex);
        }

        return model;
    }

    /// <summary>
    /// Screen rectangle of the Duty Finder window, or false when it is not visible (hidden with the UI, closing, or
    /// unreadable; logged once). Framework thread; allocation-free.
    /// </summary>
    public bool TryGetWindowRect(out ScreenRect rect)
    {
        rect = default;
        if (!registered || !open)
        {
            return false;
        }

        try
        {
            var addon = gameGui.GetAddonByName(AddonName);
            if (addon.IsNull || !addon.IsVisible)
            {
                return false;
            }

            var size = addon.ScaledSize;
            if (size.X <= 0f || size.Y <= 0f)
            {
                return false;
            }

            rect = ScreenRect.FromSize(addon.Position, size);
            return true;
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Duty Finder position unavailable; the unlock hint is not shown");
            return false;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        gate.Changed -= Apply;
        Apply();
    }

    /// <summary>Registers or unregisters the listeners to match the setting and the gate. Framework thread.</summary>
    private void Apply()
    {
        var want = enabled && !disposed && gate.HooksAllowed;
        if (want == registered)
        {
            return;
        }

        try
        {
            if (want)
            {
                lifecycle.RegisterListener(AddonEvent.PostSetup, AddonName, onSetup);
                lifecycle.RegisterListener(AddonEvent.PostRefresh, AddonName, onChange);
                lifecycle.RegisterListener(AddonEvent.PostReceiveEvent, AddonName, onChange);
                lifecycle.RegisterListener(AddonEvent.PostUpdate, AddonName, onUpdate);
                lifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, onFinalize);
            }
            else
            {
                lifecycle.UnregisterListener(AddonEvent.PostSetup, AddonName, onSetup);
                lifecycle.UnregisterListener(AddonEvent.PostRefresh, AddonName, onChange);
                lifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, AddonName, onChange);
                lifecycle.UnregisterListener(AddonEvent.PostUpdate, AddonName, onUpdate);
                lifecycle.UnregisterListener(AddonEvent.PreFinalize, AddonName, onFinalize);
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Duty Finder listeners could not be {Action}", want ? "registered" : "unregistered");
        }

        registered = want;
        if (!want)
        {
            Forget();
        }
    }

    private void OnSetup(AddonEvent type, AddonArgs args)
    {
        open = true;
        ReadSelection();
    }

    private void OnChange(AddonEvent type, AddonArgs args)
    {
        open = true;
        ReadSelection();
    }

    private void OnUpdate(AddonEvent type, AddonArgs args)
    {
        // Registered while the window was already up: PostUpdate is the first event this side sees.
        open = true;
        var now = Environment.TickCount64;
        if (now - lastPoll >= PollInterval)
        {
            ReadSelection();
        }
    }

    private void OnFinalize(AddonEvent type, AddonArgs args) => Forget();

    private void Forget()
    {
        open = false;
        selected = 0;
        locked = false;
    }

    /// <summary>
    /// Reads the selected duty and its lock. A roulette or no selection reads as none; a condition that links no
    /// InstanceContent row, or a read failure (logged once), reads as not locked, so the panel says nothing.
    /// </summary>
    private void ReadSelection()
    {
        lastPoll = Environment.TickCount64;
        try
        {
            var agent = AgentContentsFinder.Instance();
            if (agent == null)
            {
                selected = 0;
                locked = false;
                return;
            }

            var duty = agent->SelectedDuty;
            selected = duty.ContentType == ContentsType.Regular ? duty.Id : 0;
            locked = selected != 0 && Duty(selected) is { } info && !UIState.IsInstanceContentUnlocked(info.Instance);
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Duty Finder selection could not be read; the unlock hint is not shown");
            selected = 0;
            locked = false;
        }
    }

    private void Rebuild(DutyUnlockIndex currentIndex)
    {
        modelCondition = selected;
        modelLocked = locked;
        modelVersion = session.Version;
        modelIndex = currentIndex;
        var previous = model;
        model = null;

        var resolved = currentIndex.Resolve(selected, session.Bundle?.Catalog);
        if (resolved.Count == 0 || Duty(selected) is not { } duty)
        {
            return;
        }

        // The Duty Finder belongs to the logged-in character: its states, its blocker names and its spoiler shield.
        var states = session.LiveStates;
        var names = session.LiveNames;
        var spoilers = session.LiveSpoilers;
        quests.Clear();
        for (var i = 0; i < resolved.Count && i < MaxQuests; i++)
        {
            var quest = resolved[i];
            var evaluation = states.GetValueOrDefault(quest.RowId);
            var state = evaluation?.State ?? QuestState.Unknown;
            quests.Add(new DutyHintQuest(
                quest,
                state,
                spoilers.DisplayName(quest),
                BlockerText.StatusText(evaluation, quest, names, states),
                state == QuestState.Completed));
        }

        var more = resolved.Count > MaxQuests
            ? string.Format(CultureInfo.CurrentCulture, Strings.DutyHintMoreFormat, resolved.Count - MaxQuests)
            : string.Empty;
        // Every poller apply bumps the session version; when the panel's content came out the same, keep the model the
        // panel already has rather than hand it an equal new one.
        model = previous is not null && SameContent(previous, selected, duty.Name, quests, more)
            ? previous
            : new DutyHintModel(selected, duty.Name, quests.Count == 0 ? NoQuests : quests.ToArray(), more);
    }

    private static bool SameContent(DutyHintModel previous, uint condition, string dutyName, List<DutyHintQuest> lines, string more)
    {
        if (previous.ContentFinderConditionId != condition || previous.Quests.Count != lines.Count
            || !string.Equals(previous.DutyName, dutyName, StringComparison.Ordinal) || !string.Equals(previous.MoreText, more, StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var a = previous.Quests[i];
            var b = lines[i];
            if (!ReferenceEquals(a.Quest, b.Quest) || a.State != b.State || a.Done != b.Done
                || !string.Equals(a.Name, b.Name, StringComparison.Ordinal) || !string.Equals(a.StatusText, b.StatusText, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>ContentFinderCondition row id to its InstanceContent row and display name, read once from the sheet.</summary>
    private (uint Instance, string Name)? Duty(uint conditionId)
    {
        if (duties is null)
        {
            var map = new Dictionary<uint, (uint, string)>();
            try
            {
                foreach (var row in data.GetExcelSheet<ContentFinderCondition>())
                {
                    if (row.ContentLinkType != InstanceContentLink || row.Content.RowId == 0)
                    {
                        continue;
                    }

                    var text = row.Name.ExtractText();
                    // The sheet writes "the Vault"; the panel opens with the name, so its first letter is raised.
                    var name = text.Length > 0 && char.IsLower(text[0]) ? char.ToUpperInvariant(text[0]) + text[1..] : text;
                    map[row.RowId] = (row.Content.RowId, name);
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "ContentFinderCondition sheet could not be read; the Duty Finder unlock hint is off");
            }

            duties = map;
        }

        return duties.TryGetValue(conditionId, out var info) ? info : null;
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

/// <summary>What the Duty Finder unlock hint shows for one locked duty; built once per change, drawn every frame.</summary>
/// <param name="ContentFinderConditionId">The selected duty.</param>
/// <param name="DutyName">Its Duty Finder name, first letter raised.</param>
/// <param name="Quests">The quests that unlock it, at most <see cref="DutyFinderHint.MaxQuests"/>.</param>
/// <param name="MoreText">"and N more" when more quests unlock it; empty otherwise.</param>
public sealed record DutyHintModel(uint ContentFinderConditionId, string DutyName, IReadOnlyList<DutyHintQuest> Quests, string MoreText);

/// <summary>One unlocking quest of <see cref="DutyHintModel"/>, its strings built once.</summary>
/// <param name="Quest">The quest record (for Reveal and Flag giver).</param>
/// <param name="State">Its state for the logged-in character.</param>
/// <param name="Name">Its name through the logged-in character's spoiler shield.</param>
/// <param name="StatusText"><see cref="BlockerText.StatusText"/>: the state word and the decisive blocker.</param>
/// <param name="Done">The quest is completed (the duty is locked for another reason).</param>
public sealed record DutyHintQuest(QuestRecord Quest, QuestState State, string Name, string StatusText, bool Done);
