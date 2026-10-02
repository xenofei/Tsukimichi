using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Unique;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// Optional nameplate marks on quest givers (feature plan v5, 1.7.0; R8 D): an event NPC that hands out a quest the
/// logged-in character can take and cares about gets a short title line under its name, "☾ Pinned", "☾ Moonlit
/// reward" or "☾ Ready on WHM" (<see cref="NamePlateMarks"/> picks which). Off by default
/// (<see cref="Config.Configuration.NamePlateMarks"/>).
/// <para>
/// Cost: the NPC-to-mark table is rebuilt only when its inputs change (the live states, the pins, the Moonlit catalog,
/// the language), on <see cref="SessionState.Changed"/>; Dalamud's <see cref="INamePlateGui.OnNamePlateUpdate"/> hands
/// over only the plates with important updates, and each costs a kind check and, for an event NPC, one dictionary
/// lookup. When the table changes the plates are redrawn once (<see cref="INamePlateGui.RequestRedraw"/>), so marks
/// appear and clear without waiting for the game.
/// </para>
/// <para>
/// Only <see cref="NamePlateKind.EventNpcCompanion"/> plates of <see cref="ObjectKind.EventNpc"/> objects are touched,
/// by base id (the ENpcResident row a quest's giver names); player plates never are. The title is set through
/// <see cref="INamePlateUpdateHandler.TitleParts"/>, so the game's « » quotes and other plugins' wraps stay. Subscribed
/// while the setting is on and the shared <see cref="HookGate"/> allows game hooks; <see cref="Dispose"/> unsubscribes
/// and redraws the plates without the marks.
/// </para>
/// </summary>
public sealed class NamePlateHooks : IDisposable
{
    private readonly INamePlateGui namePlates;
    private readonly SessionState session;
    private readonly HookGate gate;
    private readonly IPluginLog log;

    private Dictionary<uint, NamePlateMark> marks = [];
    private Dictionary<uint, SeString> titles = [];
    private bool enabled;
    private bool subscribed;
    private bool disposed;
    private bool warned;

    // The inputs the table was built from; a rebuild is skipped while all are the same.
    private object? builtStates;
    private object? builtMoonlit;
    private object? builtBundle;
    private int builtPins = -1;
    private int builtLanguage = -1;
    private UniqueRewardCatalog? moonlitFor;
    private HashSet<uint> moonlitQuests = [];

    public NamePlateHooks(INamePlateGui namePlates, SessionState session, HookGate gate, IPluginLog log)
    {
        this.namePlates = namePlates ?? throw new ArgumentNullException(nameof(namePlates));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        gate.Changed += Apply;
    }

    /// <summary>The logged-in character's pinned row ids; set by the plugin. Null marks no pins.</summary>
    public Func<IReadOnlySet<uint>>? LivePins { get; set; }

    /// <summary>Moves when the pins change (the query runner's pins version); a rebuild follows. Null reads as unchanged.</summary>
    public Func<int>? PinsVersion { get; set; }

    /// <summary>The Moonlit catalog; set by the plugin. Null marks no Moonlit rewards.</summary>
    public Func<UniqueRewardCatalog>? Moonlit { get; set; }

    /// <summary>The player's setting (off by default). Subscribed only while this is on and the gate allows game hooks.</summary>
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

    /// <summary>Whether the handler is subscribed now.</summary>
    public bool IsActive => subscribed;

    /// <summary>How many givers carry a mark now.</summary>
    public int MarkCount => marks.Count;

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

    private void Apply()
    {
        var want = enabled && !disposed && gate.HooksAllowed;
        if (want == subscribed)
        {
            return;
        }

        subscribed = want;
        try
        {
            if (want)
            {
                session.Changed += Rebuild;
                namePlates.OnNamePlateUpdate += OnNamePlateUpdate;
                Rebuild();
                namePlates.RequestRedraw();
            }
            else
            {
                session.Changed -= Rebuild;
                namePlates.OnNamePlateUpdate -= OnNamePlateUpdate;
                marks = [];
                titles = [];
                builtStates = null;
                // The game restores its own titles on the next full update.
                namePlates.RequestRedraw();
            }
        }
        catch (Exception ex)
        {
            Warn(ex, "Nameplate marks could not be switched");
        }
    }

    /// <summary>Rebuilds the NPC-to-mark table when its inputs changed; redraws the plates when the table did. Framework thread.</summary>
    private void Rebuild()
    {
        if (!subscribed)
        {
            return;
        }

        try
        {
            var states = session.LiveContentId is null ? null : session.LiveStates;
            var moonlit = Moonlit?.Invoke();
            var bundle = session.Bundle;
            var pinsVersion = PinsVersion?.Invoke() ?? 0;
            var language = Localization.Loc.Version;
            if (ReferenceEquals(states, builtStates)
                && ReferenceEquals(moonlit, builtMoonlit)
                && ReferenceEquals(bundle, builtBundle)
                && pinsVersion == builtPins
                && language == builtLanguage)
            {
                return;
            }

            builtStates = states;
            builtMoonlit = moonlit;
            builtBundle = bundle;
            builtPins = pinsVersion;
            var languageChanged = language != builtLanguage;
            builtLanguage = language;

            var next = states is null || bundle is null
                ? new Dictionary<uint, NamePlateMark>()
                : NamePlateMarks.Build(bundle.Catalog, states, LivePins?.Invoke() ?? new HashSet<uint>(), MoonlitQuests(moonlit));
            if (!languageChanged && NamePlateMarks.Same(marks, next))
            {
                return;
            }

            var names = session.LiveNames;
            var nextTitles = new Dictionary<uint, SeString>(next.Count);
            foreach (var (npcId, mark) in next)
            {
                nextTitles[npcId] = new SeStringBuilder().AddText(Title(mark, names)).Build();
            }

            marks = next;
            titles = nextTitles;
            namePlates.RequestRedraw();
        }
        catch (Exception ex)
        {
            Warn(ex, "Nameplate marks could not be worked out");
        }
    }

    /// <summary>The quests with a Moonlit reward, read once per catalog.</summary>
    private HashSet<uint> MoonlitQuests(UniqueRewardCatalog? catalog)
    {
        if (catalog is null)
        {
            return [];
        }

        if (!ReferenceEquals(catalog, moonlitFor))
        {
            moonlitFor = catalog;
            moonlitQuests = [];
            foreach (var entry in catalog.All)
            {
                moonlitQuests.Add(entry.QuestRowId);
            }
        }

        return moonlitQuests;
    }

    private static string Title(NamePlateMark mark, BlockerNames names) => mark.Kind switch
    {
        NamePlateMarkKind.Pinned => Strings.NamePlatePinned,
        NamePlateMarkKind.MoonlitReward => Strings.NamePlateMoonlit,
        _ => names.JobAbbreviation(mark.Job) is { Length: > 0 } job && mark.Job != 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.NamePlateReadyOnFormat, job)
            : Strings.NamePlateReadyOnOtherJob,
    };

    private void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        var current = titles;
        if (current.Count == 0)
        {
            return;
        }

        try
        {
            foreach (var handler in handlers)
            {
                if (handler.NamePlateKind != NamePlateKind.EventNpcCompanion
                    || handler.GameObject is not { ObjectKind: ObjectKind.EventNpc } npc
                    || !current.TryGetValue(npc.BaseId, out var title))
                {
                    continue;
                }

                handler.TitleParts.Text = title;
                handler.DisplayTitle = true;
                handler.IsPrefixTitle = false;
            }
        }
        catch (Exception ex)
        {
            Warn(ex, "Nameplate mark failed");
        }
    }

    private void Warn(Exception ex, string message)
    {
        if (!warned)
        {
            warned = true;
            log.Warning(ex, "{Message}; further failures are logged at debug level", message);
        }
        else
        {
            log.Debug(ex, "{Message}", message);
        }
    }
}
