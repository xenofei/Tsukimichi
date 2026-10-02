using System;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The panels beside game windows of 1.7.0 in one place: "Worth it?" beside the quest offer (<see cref="QuestOfferHint"/>
/// and <see cref="QuestOfferPanel"/>), "What this opened" beside the quest completion (<see cref="QuestResultHint"/> and
/// <see cref="QuestResultPanel"/>) and the Journal companion (<see cref="JournalCompanionHint"/> and
/// <see cref="JournalCompanionPanel"/>). It builds them, hooks their draws into <c>UiBuilder.Draw</c>, wires their
/// Settings › Integrations toggles, and takes them all down on dispose. Every game read sits behind the shared
/// <see cref="HookGate"/>.
/// </summary>
public sealed class GamePanels : IDisposable
{
    private readonly IUiBuilder uiBuilder;
    private readonly QuestOfferHint offer;
    private readonly QuestResultHint result;
    private readonly JournalCompanionHint journal;
    private readonly QuestOfferPanel offerPanel;
    private readonly QuestResultPanel resultPanel;
    private readonly JournalCompanionPanel journalPanel;
    private bool disposed;

    /// <param name="reveal">Opens the main window on a quest.</param>
    /// <param name="openRoute">Opens the route window on a target.</param>
    public GamePanels(
        IUiBuilder uiBuilder,
        IAddonLifecycle lifecycle,
        IGameGui gameGui,
        ITargetManager targets,
        Configuration settings,
        QuestBriefBuilder briefs,
        QueryRunner runner,
        GameLinks links,
        HookGate gate,
        IPluginLog log,
        Action<QuestRecord> reveal,
        Action<RouteTarget> openRoute)
    {
        this.uiBuilder = uiBuilder ?? throw new ArgumentNullException(nameof(uiBuilder));
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(briefs);
        ArgumentNullException.ThrowIfNull(openRoute);
        offer = new QuestOfferHint(lifecycle, gameGui, targets, briefs, gate, log) { Enabled = settings.QuestOfferPanelEnabled };
        result = new QuestResultHint(lifecycle, gameGui, briefs, gate, log) { Enabled = settings.QuestResultPanelEnabled };
        journal = new JournalCompanionHint(lifecycle, gameGui, briefs, gate, log) { Enabled = settings.JournalCompanionEnabled };
        offerPanel = new QuestOfferPanel(offer, briefs.Session, runner, reveal);
        resultPanel = new QuestResultPanel(result, briefs.Session, runner, links);
        journalPanel = new JournalCompanionPanel(journal, reveal, (quest, name) => openRoute(RouteTarget.ForQuest(quest.RowId, name)));
        uiBuilder.Draw += offerPanel.Draw;
        uiBuilder.Draw += resultPanel.Draw;
        uiBuilder.Draw += journalPanel.Draw;
    }

    /// <summary>Hands Settings › Integrations the three toggles.</summary>
    public void Attach(ConfigWindow config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.QuestOfferPanelToggled = enabled => offer.Enabled = enabled;
        config.QuestResultPanelToggled = enabled => result.Enabled = enabled;
        config.JournalCompanionToggled = enabled => journal.Enabled = enabled;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        uiBuilder.Draw -= offerPanel.Draw;
        uiBuilder.Draw -= resultPanel.Draw;
        uiBuilder.Draw -= journalPanel.Draw;
        offer.Dispose();
        result.Dispose();
        journal.Dispose();
    }
}
