using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Tsukimichi.Core.Discovery;
using Tsukimichi.Core.GamePanels;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Game;

/// <summary>
/// The "Worth it?" panel (1.7.0), game side: while the quest-offer window (<c>JournalAccept</c>) is open, works out
/// which quest it offers and hands <see cref="Ui.QuestOfferPanel"/> a <see cref="QuestBrief"/> of it for the
/// logged-in character. The window does not tell its quest's id, so the quest is found by title
/// (<see cref="QuestIdentifier"/>): the title text node (<c>AddonJournalAccept.QuestTitleText</c>) and the window's
/// string values, matched first against the targeted NPC's quests (its base id is the issuer the catalog records), and
/// any value that is a Quest sheet row id, then across the whole catalog in the client's language.
/// <para>
/// Read on PostSetup, PostRefresh and PostRequestedUpdate; while no quest is found yet, PostUpdate retries every
/// <see cref="RetryInterval"/> for <see cref="RetryWindow"/> (the title can land a frame after setup). Behind the addon
/// kill switch like every panel beside a game window. Informational only: it never presses Accept or Decline.
/// </para>
/// </summary>
public sealed unsafe class QuestOfferHint : AddonPanelSource
{
    /// <summary>Name of the game's quest-offer window.</summary>
    public const string Addon = "JournalAccept";

    /// <summary>Milliseconds between retries while the offered quest is not found yet.</summary>
    public const long RetryInterval = 250;

    /// <summary>How long after the window opened the retries go on, in milliseconds.</summary>
    public const long RetryWindow = 3000;

    private readonly ITargetManager targets;
    private readonly QuestBriefBuilder briefs;
    private readonly List<string> texts = [];
    private readonly List<uint> numbers = [];
    private readonly List<QuestRecord> candidates = [];
    private readonly Func<QuestRecord, bool> prefer;

    private uint offered;
    private TitleMatchSource source;
    private long openedAt;

    // What the model was built from.
    private uint modelRowId;
    private int modelVersion = -1;
    private QuestOfferModel? model;

    // The targeted NPC's quests, kept while the same NPC and catalog stay.
    private uint npcFor;
    private QuestCatalog? npcCatalog;
    private List<QuestRecord> npcQuests = [];

    public QuestOfferHint(IAddonLifecycle lifecycle, IGameGui gameGui, ITargetManager targets, QuestBriefBuilder briefs, HookGate gate, IPluginLog log)
        : base(lifecycle, gameGui, gate, log, Addon, RetryInterval)
    {
        this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
        this.briefs = briefs ?? throw new ArgumentNullException(nameof(briefs));
        prefer = briefs.IsReady;
        Start();
    }

    protected override bool WantsPoll => offered == 0 && Environment.TickCount64 - openedAt < RetryWindow;

    /// <summary>
    /// The panel's model, or null when there is nothing to show: off or paused, the window is closed, or the offered
    /// quest is unknown. Framework thread; rebuilt only when the quest or the session changes.
    /// </summary>
    public QuestOfferModel? Current()
    {
        if (!IsActive || !IsOpen || offered == 0 || briefs.Session.Bundle is not { } bundle)
        {
            return null;
        }

        if (offered != modelRowId || briefs.Session.Version != modelVersion || model is null)
        {
            modelRowId = offered;
            modelVersion = briefs.Session.Version;
            model = bundle.Catalog.GetByRowId(offered) is { } quest
                ? new QuestOfferModel(briefs.Build(quest, bundle), source == TitleMatchSource.Catalog)
                : null;
        }

        return model;
    }

    protected override void Read(AtkUnitBase* addon)
    {
        if (offered == 0 && openedAt == 0)
        {
            openedAt = Environment.TickCount64;
        }

        if (briefs.Session.Bundle is not { } bundle)
        {
            return;
        }

        var catalog = bundle.Catalog;
        texts.Clear();
        numbers.Clear();
        texts.Add(NodeText(((AddonJournalAccept*)addon)->QuestTitleText));
        CollectValues(addon, texts, numbers);

        candidates.Clear();
        candidates.AddRange(NpcQuests(catalog));
        foreach (var number in numbers)
        {
            // Only a full Quest sheet row id (65536 and up) is taken from a value: small numbers are counts and flags.
            if (number > ushort.MaxValue && catalog.GetByRowId(number) is { } quest && !candidates.Contains(quest))
            {
                candidates.Add(quest);
            }
        }

        var match = QuestIdentifier.Identify(texts, candidates, briefs.Titles(catalog), prefer);
        if (match.Quest is { } found)
        {
            offered = found.RowId;
            source = match.Source;
        }
    }

    protected override void Forget()
    {
        offered = 0;
        source = TitleMatchSource.None;
        openedAt = 0;
        model = null;
        modelRowId = 0;
    }

    /// <summary>The quests the targeted event NPC hands out; empty with no such target.</summary>
    private List<QuestRecord> NpcQuests(QuestCatalog catalog)
    {
        var target = targets.Target ?? targets.SoftTarget;
        var npc = target is { ObjectKind: ObjectKind.EventNpc } ? target.BaseId : 0u;
        if (npc != npcFor || !ReferenceEquals(catalog, npcCatalog))
        {
            npcFor = npc;
            npcCatalog = catalog;
            npcQuests = QuestDiscovery.IssuedBy(catalog, npc);
        }

        return npcQuests;
    }
}

/// <summary>What the "Worth it?" panel shows for the offered quest.</summary>
/// <param name="Brief">The quest's brief for the logged-in character.</param>
/// <param name="ByTitleOnly">No expected candidate carried the title; it was matched across the catalog.</param>
public sealed record QuestOfferModel(QuestBrief Brief, bool ByTitleOnly);
