using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Game;

/// <summary>
/// The New Game+ session (feature plan v7, 1.19.0, C4), game side: reads the game's New Game+ HUD every
/// <see cref="ReadInterval"/> ms and hands it to <see cref="SessionState.NewGamePlus"/>. The read follows Questionable's
/// (QuestFunctions.GetMainScenarioQuest): New Game+ is open once "Memories Rekindled" is complete; a chapter runs while
/// the <c>QuestRedoHud</c> agent is active and its HUD window's first value reads 0 (playing), 2, 3 (the New Game+
/// window open) or 4 (between steps); the agent holds the chapter at +44 and the quest at +46. Those offsets are not in
/// ClientStructs, so the read sits behind the shared hook gate: on an untested game nothing here touches memory and
/// the session rests on the capture alone (<see cref="NewGamePlusSession.ObserveCapture"/>).
/// <para>Safety: reads only; a read that throws is logged once and reads as "not readable". Framework thread only.</para>
/// </summary>
public sealed unsafe class NewGamePlusWatch : IDisposable
{
    /// <summary>Milliseconds between two reads.</summary>
    public const long ReadInterval = 1000;

    /// <summary>"Memories Rekindled", the quest that opens New Game+ (runtime quest id).</summary>
    public const ushort MemoriesRekindled = 3759;

    private const string HudAddon = "QuestRedoHud";
    private const int ChapterOffset = 44;
    private const int QuestOffset = 46;

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IGameGui gameGui;
    private readonly SessionState session;
    private readonly HookGate gate;
    private readonly IPluginLog log;
    private long lastRead;
    private bool warned;
    private bool disposed;

    public NewGamePlusWatch(IFramework framework, IClientState clientState, IGameGui gameGui, SessionState session, HookGate gate, IPluginLog log)
    {
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.gameGui = gameGui ?? throw new ArgumentNullException(nameof(gameGui));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        framework.Update -= OnUpdate;
    }

    private void OnUpdate(IFramework _)
    {
        var now = Environment.TickCount64;
        if (disposed || now - lastRead < ReadInterval)
        {
            return;
        }

        lastRead = now;
        var tracker = session.NewGamePlus;
        if (!clientState.IsLoggedIn)
        {
            tracker.Reset();
            return;
        }

        tracker.ObserveHud(gate.HooksAllowed ? Read() : null);
    }

    /// <summary>The HUD as the game holds it; null when it cannot be read.</summary>
    private NewGamePlusHud? Read()
    {
        try
        {
            if (!QuestManager.IsQuestComplete(MemoriesRekindled))
            {
                return NewGamePlusHud.Inactive;
            }

            var module = AgentModule.Instance();
            var agent = module == null ? null : module->GetAgentByInternalId(AgentId.QuestRedoHud);
            if (agent == null || !agent->IsAgentActive())
            {
                return NewGamePlusHud.Inactive;
            }

            var addon = (AtkUnitBase*)gameGui.GetAddonByName(HudAddon).Address;
            if (addon == null || addon->AtkValuesCount != 4 || addon->AtkValues == null || addon->AtkValues[0].UInt is not (0 or 2 or 3 or 4))
            {
                return NewGamePlusHud.Inactive;
            }

            var chapter = *(ushort*)((byte*)agent + ChapterOffset);
            var quest = *(ushort*)((byte*)agent + QuestOffset);
            return new NewGamePlusHud(true, quest, chapter);
        }
        catch (Exception ex)
        {
            if (!warned)
            {
                warned = true;
                log.Warning(ex, "Reading the New Game+ HUD failed; New Game+ is recognised from the captures only");
            }

            return null;
        }
    }
}
