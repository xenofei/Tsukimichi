using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// The Journal companion (1.7.0), game side: while the game's Journal (<c>Journal</c> addon, its detail half the
/// <c>JournalDetail</c> addon beside it) is open, follows the quest selected in it and hands
/// <see cref="Ui.JournalCompanionPanel"/> Tsukimichi's verdict on it for the logged-in character.
/// <para>
/// The selection is read from <c>AgentQuestJournal</c>: <c>SelectedCompletedQuestId</c> only while the completed tab
/// is shown (<c>IsDisplayingCompletedQuests</c>), <c>SelectedQuestId</c> otherwise; <c>SelectedQuestType</c> is not
/// trusted to tell the tabs apart (what it reads for a completed quest is unverified), only to skip a levequest (type
/// 2). An id of 0 (nothing selected on that tab) shows nothing. Either holds the quest id as <c>OpenForQuest</c> takes it (the low 16 bits of the row id); a
/// full row id is taken as one. Read on the Journal's setup, refresh, requested update and clicks, and every
/// <see cref="PollInterval"/> on PostUpdate for a keyboard or pad selection: three struct fields, no allocation.
/// Behind the addon kill switch like every panel beside a game window.
/// </para>
/// </summary>
public sealed unsafe class JournalCompanionHint : AddonPanelSource
{
    /// <summary>Name of the game's Journal window (the quest list).</summary>
    public const string Addon = "Journal";

    /// <summary>Name of the Journal's detail half, drawn beside the list as its own window.</summary>
    public const string DetailAddon = "JournalDetail";

    /// <summary><c>AgentQuestJournal.SelectedQuestType</c> of a levequest.</summary>
    private const uint LeveType = 2;

    /// <summary>The first Quest sheet row id: row ids are 65536 + the quest id.</summary>
    private const uint QuestRowBase = 0x10000;

    private readonly QuestBriefBuilder briefs;

    private uint selected;

    private uint modelRowId;
    private int modelVersion = -1;
    private QuestBrief? model;

    public JournalCompanionHint(IAddonLifecycle lifecycle, IGameGui gameGui, QuestBriefBuilder briefs, HookGate gate, IPluginLog log)
        : base(lifecycle, gameGui, gate, log, Addon, 250)
    {
        this.briefs = briefs ?? throw new ArgumentNullException(nameof(briefs));
        Start();
    }

    protected override bool ReadOnEvents => true;

    protected override bool WantsPoll => true;

    /// <summary>The brief of the selected quest, or null when there is nothing to show. Framework thread.</summary>
    public QuestBrief? Current()
    {
        if (!IsActive || !IsOpen || selected == 0 || briefs.Session.Bundle is not { } bundle)
        {
            return null;
        }

        if (selected != modelRowId || briefs.Session.Version != modelVersion || model is null)
        {
            modelRowId = selected;
            modelVersion = briefs.Session.Version;
            model = bundle.Catalog.GetByRowId(selected) is { } quest ? briefs.Build(quest, bundle) : null;
        }

        return model;
    }

    /// <summary>The Journal's list and, while it is shown, its detail half: the panel goes beside both.</summary>
    public override bool TryGetWindowRect(out ScreenRect rect)
    {
        if (!TryGetAddonRect(Addon, out rect))
        {
            return false;
        }

        if (TryGetAddonRect(DetailAddon, out var detail))
        {
            rect = ScreenRect.Union(rect, detail);
        }

        return true;
    }

    protected override void Read(AtkUnitBase* addon)
    {
        var agent = AgentQuestJournal.Instance();
        if (agent == null)
        {
            selected = 0;
            return;
        }

        var type = agent->SelectedQuestType;
        if (type == LeveType)
        {
            selected = 0;
            return;
        }

        // The tab decides which field to trust, not the type: the type's value for a completed quest is unverified, and
        // reading SelectedCompletedQuestId on the quests tab would show a stale completed quest. 0 shows nothing.
        var id = agent->IsDisplayingCompletedQuests ? agent->SelectedCompletedQuestId : agent->SelectedQuestId;
        selected = ToRowId(id);
    }

    protected override void Forget()
    {
        selected = 0;
        model = null;
        modelRowId = 0;
    }

    /// <summary>A quest id (low 16 bits) or a full Quest sheet row id to the row id; 0 stays 0.</summary>
    internal static uint ToRowId(uint id) => id == 0 ? 0 : id > ushort.MaxValue ? id : QuestRowBase | id;
}

