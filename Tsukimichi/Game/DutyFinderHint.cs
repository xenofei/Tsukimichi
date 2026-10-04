using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unique;
using Tsukimichi.Core.Unlocks;
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
/// <see cref="BlockerText.StatusText"/> for the logged-in character (spoiler-masked like every in-world surface), with
/// the duty's clear badges (1.19.0, C7: Solo with NPCs, Group of N, High-end, Story-required or Optional).
/// <para>
/// A selected roulette (<c>ContentsType.Roulette</c>, a <c>ContentRoulette</c> row) answers too (1.19.0, feature plan v7
/// N4): when it is closed, or open with duties in it not unlocked, <see cref="CurrentRoulette"/> hands the panel the
/// Duties card's block for it, built from the logged-in character's duty records (<see cref="DutyBoard.HintFor"/>): its
/// state ("locked · needs a Lv 100 job · best is BLM 98") and its first duties not unlocked, each with its size badge and
/// the quest that unlocks it.
/// </para>
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

    private static readonly DutyBadges.Look[] NoBadges = [];

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

    private Dictionary<uint, (uint Instance, string Name, uint Icon)>? duties;
    private bool enabled = true;
    private bool registered;
    private bool disposed;
    private bool warned;

    // What the game shows now (framework thread).
    private bool open;
    private uint selected;
    private uint selectedRoulette;
    private bool locked;
    private long lastPoll;

    // What the model was built from.
    private uint modelCondition;
    private bool modelLocked;
    private int modelVersion = -1;
    private DutyUnlockIndex? modelIndex;
    private int modelBadges = -1;
    private DutyHintModel? model;

    // What the roulette's model was built from.
    private uint rouletteModelId;
    private int rouletteModelVersion = -1;
    private int rouletteModelBadges = -1;
    private DutyUnlockIndex? rouletteModelIndex;
    private RouletteHintModel? rouletteModel;

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
    /// The clear badges and the duty index (1.19.0, C7 and N4); null leaves the badges and the roulette answer out. Set by
    /// the plugin.
    /// </summary>
    public ClearBadgeSource? Badges { get; set; }

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
        var badges = Badges?.Revision ?? 0;
        if (selected != modelCondition || locked != modelLocked || session.Version != modelVersion || !ReferenceEquals(currentIndex, modelIndex) || badges != modelBadges)
        {
            Rebuild(currentIndex, badges);
        }

        return model;
    }

    /// <summary>
    /// The panel's model for the selected roulette, or null when there is nothing to say: the hint is off or paused, the
    /// Duty Finder is closed, no roulette is selected, the duty index or the character's duty records are not read yet,
    /// or the roulette is open with every duty in it unlocked (Mentor, which mentor status opens, is never answered).
    /// Framework thread; allocation-free unless an input changed.
    /// </summary>
    public RouletteHintModel? CurrentRoulette()
    {
        if (!registered || !open || selectedRoulette == 0 || Badges is not { } badges)
        {
            return null;
        }

        var currentIndex = index.Current;
        var revision = badges.Revision;
        if (selectedRoulette != rouletteModelId || session.Version != rouletteModelVersion || !ReferenceEquals(currentIndex, rouletteModelIndex) || revision != rouletteModelBadges)
        {
            RebuildRoulette(currentIndex, badges, revision);
        }

        return rouletteModel;
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
        // A listener whose unregister threw keeps firing after Apply() recorded the hint as off: read nothing then.
        if (!registered)
        {
            return;
        }

        open = true;
        ReadSelection();
    }

    private void OnChange(AddonEvent type, AddonArgs args)
    {
        if (!registered)
        {
            return;
        }

        open = true;
        ReadSelection();
    }

    private void OnUpdate(AddonEvent type, AddonArgs args)
    {
        if (!registered)
        {
            return;
        }

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
        selectedRoulette = 0;
        locked = false;
    }

    /// <summary>
    /// Reads the selected duty and its lock, or the selected roulette. No selection reads as none; a condition that links
    /// no InstanceContent row, or a read failure (logged once), reads as not locked, so the panel says nothing.
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
                selectedRoulette = 0;
                locked = false;
                return;
            }

            var duty = agent->SelectedDuty;
            selected = duty.ContentType == ContentsType.Regular ? duty.Id : 0;
            selectedRoulette = duty.ContentType == ContentsType.Roulette ? duty.Id : 0;
            locked = selected != 0 && Duty(selected) is { } info && !UIState.IsInstanceContentUnlocked(info.Instance);
        }
        catch (Exception ex)
        {
            WarnOnce(ex, "Duty Finder selection could not be read; the unlock hint is not shown");
            selected = 0;
            selectedRoulette = 0;
            locked = false;
        }
    }

    private void Rebuild(DutyUnlockIndex currentIndex, int badgesRevision)
    {
        modelCondition = selected;
        modelLocked = locked;
        modelVersion = session.Version;
        modelIndex = currentIndex;
        modelBadges = badgesRevision;
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
        // The duty's clear badges (C7), as "How you'll clear it" shows them; none while the duty index is read.
        var badges = Badges is { } clear && clear.Index?.ByCondition(selected) is { } info ? clear.For(info, DutyBadgeSurface.DutyFinder) : NoBadges;

        // Every poller apply bumps the session version; when the panel's content came out the same, keep the model the
        // panel already has rather than hand it an equal new one.
        model = previous is not null && SameContent(previous, selected, duty.Name, quests, more) && ReferenceEquals(previous.Badges, badges)
            ? previous
            : new DutyHintModel(selected, duty.Name, quests.Count == 0 ? NoQuests : quests.ToArray(), more) { AllQuestRowIds = RowIds(resolved), Icon = duty.Icon, Badges = badges };
    }

    /// <summary>
    /// The selected roulette's model: the Duties card's block for it (<see cref="DutyBoard.HintFor"/>) for the logged-in
    /// character, its first <see cref="MaxQuests"/> duties not unlocked with their size badge and unlock quest (names
    /// through the live spoiler shield; a duty whose every unlock quest the shield hides is "A duty further along the
    /// story"), and the route over those quests.
    /// </summary>
    private void RebuildRoulette(DutyUnlockIndex currentIndex, ClearBadgeSource badges, int revision)
    {
        rouletteModelId = selectedRoulette;
        rouletteModelVersion = session.Version;
        rouletteModelIndex = currentIndex;
        rouletteModelBadges = revision;
        rouletteModel = null;
        if (badges.Index is not { } runs || session.Bundle is not { } bundle || session.LiveSnapshot is not { } snapshot)
        {
            return;
        }

        var catalog = bundle.Catalog;
        var states = session.LiveStates;
        var board = DutyBoard.Build(runs, snapshot, condition => DutyBoardSource.UnlockQuests(currentIndex, condition, catalog, states), bundle.DutyJobs());
        if (DutyBoard.HintFor(board, selectedRoulette) is not { } line)
        {
            return;
        }

        var spoilers = session.LiveSpoilers;
        var rows = new List<RouletteHintRow>(Math.Min(MaxQuests, line.Missing.Count));
        var parts = new List<RouteTarget>();
        foreach (var missing in line.Missing)
        {
            var quest = missing.UnlockQuests.Count > 0 ? missing.UnlockQuests[0] : null;
            // Hidden as the Duties board hides it: every unlock quest masked, or a duty the story has not introduced (1.20.0 N6).
            var name = spoilers.IsNameMasked(Core.Query.SpoilerKind.Duty, missing.Duty.Name) ? spoilers.Name(Core.Query.SpoilerKind.Duty, missing.Duty.Name)
                : quest is not null && missing.UnlockQuests.All(spoilers.IsMasked)
                    ? string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardHiddenDutyFormat, missing.Duty.LevelRequired)
                    : missing.Duty.Name;
            if (quest is not null)
            {
                parts.Add(new RouteTarget(RouteTargetKind.Duty, name, [quest.RowId]));
            }

            if (rows.Count < MaxQuests)
            {
                var size = badges.For(missing.Duty, DutyBadgeSurface.Board);
                rows.Add(quest is null
                    ? new RouletteHintRow(name, size, Strings.DutyBoardNoQuest, string.Empty, null)
                    : new RouletteHintRow(name, size, Strings.DutyBoardWith, spoilers.DisplayName(quest), quest));
            }
        }

        var more = line.Missing.Count > MaxQuests
            ? string.Format(CultureInfo.CurrentCulture, Strings.DutyHintMoreFormat, line.Missing.Count - MaxQuests)
            : string.Empty;
        rouletteModel = new RouletteHintModel(
            line.Roulette.Id,
            string.Format(CultureInfo.CurrentCulture, Strings.DutyBoardRouletteFormat, line.Roulette.ShortName),
            DutyBoardSource.State(line, snapshot, bundle),
            rows,
            more)
        {
            Route = parts.Count == 0 ? null : RouteTarget.Union(RouteTargetKind.Duty, line.Roulette.ShortName, parts),
        };
    }

    private static uint[] RowIds(IReadOnlyList<QuestRecord> quests)
    {
        var ids = new uint[quests.Count];
        for (var i = 0; i < ids.Length; i++)
        {
            ids[i] = quests[i].RowId;
        }

        return ids;
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

    /// <summary>
    /// ContentFinderCondition row id to its InstanceContent row, display name and icon (<see cref="DutyArt"/>'s chain:
    /// its emblem, its category's tile, and on to the Duty Finder menu icon), read once from the sheet.
    /// </summary>
    private (uint Instance, string Name, uint Icon)? Duty(uint conditionId)
    {
        if (duties is null)
        {
            var map = new Dictionary<uint, (uint, string, uint)>();
            try
            {
                var pvpIcon = data.GetExcelSheet<ContentType>().GetRowOrDefault(DutyArt.PvpContentType)?.Icon ?? 0u;
                var menuIcon = data.GetExcelSheet<MainCommand>().GetRowOrDefault(DutyArt.DutyFinderMenu) is { Icon: > 0 } menu ? (uint)menu.Icon : 0u;
                foreach (var row in data.GetExcelSheet<ContentFinderCondition>())
                {
                    if (row.ContentLinkType != InstanceContentLink || row.Content.RowId == 0)
                    {
                        continue;
                    }

                    var text = row.Name.ExtractText();
                    // The sheet writes "the Vault"; the panel opens with the name, so its first letter is raised.
                    var name = text.Length > 0 && char.IsLower(text[0]) ? char.ToUpperInvariant(text[0]) + text[1..] : text;
                    var type = row.ContentType.RowId != 0 ? row.ContentType.ValueNullable : null;
                    var genre = row.JournalGenre.RowId != 0 ? row.JournalGenre.ValueNullable : null;
                    var pvp = row.Content.GetValueOrDefault<Lumina.Excel.Sheets.InstanceContent>() is { } instance && instance.InstanceContentType.RowId == DutyArt.PvpInstanceContentType;
                    var sources = new DutyArtSources(row.Icon, type?.Icon ?? 0u, type?.IconDutyFinder ?? 0u, genre is { Icon: > 0 } g ? (uint)g.Icon : 0u, pvp);
                    map[row.RowId] = (row.Content.RowId, name, DutyArt.Icon(sources, pvpIcon, menuIcon));
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
public sealed record DutyHintModel(uint ContentFinderConditionId, string DutyName, IReadOnlyList<DutyHintQuest> Quests, string MoreText)
{
    /// <summary>Every quest that unlocks the duty (not only the first <see cref="DutyFinderHint.MaxQuests"/>), for "Route to unlock".</summary>
    public IReadOnlyList<uint> AllQuestRowIds { get; init; } = [];

    /// <summary>The duty's icon (<see cref="DutyArt"/>'s chain), drawn before its name; 0 draws none.</summary>
    public uint Icon { get; init; }

    /// <summary>The duty's clear badges (1.19.0, C7), on the line under its name; empty for none.</summary>
    public IReadOnlyList<DutyBadges.Look> Badges { get; init; } = [];
}

/// <summary>What the Duty Finder hint shows for a selected roulette (1.19.0, N4); built once per change, drawn every frame.</summary>
/// <param name="RouletteId">The <c>ContentRoulette</c> row.</param>
/// <param name="Header">"Level Cap Dungeons roulette".</param>
/// <param name="State">"locked · needs a Lv 100 job · best is BLM 98", "open · 1 raid not unlocked".</param>
/// <param name="Rows">Its first duties not unlocked, at most <see cref="DutyFinderHint.MaxQuests"/>.</param>
/// <param name="MoreText">"and N more" when more duties are left; empty otherwise.</param>
public sealed record RouletteHintModel(uint RouletteId, string Header, string State, IReadOnlyList<RouletteHintRow> Rows, string MoreText)
{
    /// <summary>The route over every quest that unlocks a duty left in it; null when no quest is known.</summary>
    public RouteTarget? Route { get; init; }
}

/// <summary>One duty of <see cref="RouletteHintModel"/>, its strings built once.</summary>
/// <param name="Duty">Its name, or the spoiler shield's stand-in.</param>
/// <param name="Badges">Its size badge (C7), as the Duties card's rows wear it.</param>
/// <param name="Trailing">"not unlocked · with ", or "not unlocked · no unlock quest known".</param>
/// <param name="QuestName">The quest that unlocks it, through the live spoiler shield; empty for none.</param>
/// <param name="Quest">That quest (for Reveal); null for none.</param>
public sealed record RouletteHintRow(string Duty, IReadOnlyList<DutyBadges.Look> Badges, string Trailing, string QuestName, QuestRecord? Quest);

/// <summary>One unlocking quest of <see cref="DutyHintModel"/>, its strings built once.</summary>
/// <param name="Quest">The quest record (for Reveal and Flag giver).</param>
/// <param name="State">Its state for the logged-in character.</param>
/// <param name="Name">Its name through the logged-in character's spoiler shield.</param>
/// <param name="StatusText"><see cref="BlockerText.StatusText"/>: the state word and the decisive blocker.</param>
/// <param name="Done">The quest is completed (the duty is locked for another reason).</param>
public sealed record DutyHintQuest(QuestRecord Quest, QuestState State, string Name, string StatusText, bool Done);
