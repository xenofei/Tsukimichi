using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Companions;

namespace Tsukimichi.Game;

/// <summary>
/// "Open in Quest Map": shows a quest in Quest Map's requirement graph. The gates are Quest Map's own (GemPlugins/QuestMap
/// <c>QuestMap/Ipc.cs</c>, added in b2ce55e3e9d972f126961a66342f58113dd75b86 and unchanged at
/// 5926c83ee9aec9036a8bbffc686b8e3ba133720f): <c>QuestMap.ShowGraphByQuestId(uint questId) -> bool</c> and
/// <c>QuestMap.ShowInfoByQuestId(uint questId) -> bool</c>, both taking the Quest sheet row id (Quest Map keys its
/// nodes by <c>Quest.RowId</c>) and answering false for a quest it does not chart. Available follows the companion
/// registry; every call is wrapped and the first failure is logged once.
/// </summary>
public sealed class QuestMapIpc
{
    public const string ShowGraphGate = "QuestMap.ShowGraphByQuestId";
    public const string ShowInfoGate = "QuestMap.ShowInfoByQuestId";

    private readonly CompanionPlugins companions;
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<uint, bool>? showGraph;
    private readonly ICallGateSubscriber<uint, bool>? showInfo;
    private bool warned;

    public QuestMapIpc(IDalamudPluginInterface pluginInterface, CompanionPlugins companions, IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        this.companions = companions ?? throw new ArgumentNullException(nameof(companions));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        try
        {
            showGraph = pluginInterface.GetIpcSubscriber<uint, bool>(ShowGraphGate);
            showInfo = pluginInterface.GetIpcSubscriber<uint, bool>(ShowInfoGate);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Quest Map IPC subscribers unavailable");
        }
    }

    /// <summary>Quest Map is loaded and recent enough.</summary>
    public bool Available => showGraph is not null && companions.IsLoaded(CompanionPlugin.QuestMap);

    /// <summary>Why "Open in Quest Map" is disabled; null when it is not.</summary>
    public string? DisabledReason => Available ? null : CompanionPlugins.DisabledReason(CompanionPlugin.QuestMap);

    /// <summary>Opens Quest Map's graph on the quest. False when Quest Map is absent, does not chart the quest, or threw.</summary>
    public bool ShowGraph(uint questRowId) => Call(showGraph, questRowId, "QuestMap.ShowGraphByQuestId failed");

    /// <summary>Opens Quest Map's info window for the quest. False when Quest Map is absent, does not know it, or threw.</summary>
    public bool ShowInfo(uint questRowId) => Call(showInfo, questRowId, "QuestMap.ShowInfoByQuestId failed");

    private bool Call(ICallGateSubscriber<uint, bool>? gate, uint questRowId, string failure)
    {
        if (questRowId == 0 || gate is null || !Available)
        {
            return false;
        }

        try
        {
            return gate.InvokeFunc(questRowId);
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            if (!warned)
            {
                warned = true;
                log.Warning(ex, failure);
            }
            else
            {
                log.Debug(ex, failure);
            }

            return false;
        }
    }
}
