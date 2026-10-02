using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Tsukimichi.Core.GamePanels;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.GameData;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// The "What this opened" panel (1.7.0), game side: while the quest-complete window (<c>JournalResult</c>) is open,
/// works out which quest is being turned in and hands <see cref="Ui.QuestResultPanel"/> what its completion opens for
/// the logged-in character: the quests that become ready (<see cref="NewlyOpened"/>, from the session's reverse
/// prerequisite index), what it unlocks (duties, jobs, systems) and the next quest of its chain.
/// <para>
/// The window does not tell its quest's id either. Its two text nodes (<c>AddonJournalResult.AtkTextNode250</c> and
/// <c>AtkTextNode258</c>, unnamed in ClientStructs; the title is one of them) and its string values are matched by
/// title (<see cref="QuestIdentifier"/>) against the quests in the logged-in character's journal and those the state
/// poller saw completed in the last <see cref="RecentCompletionMinutes"/> minutes (Accepted to Completed), then across
/// the catalog. The model is rebuilt on every session change, so the list stays right when the poller applies the
/// completion while the window is still up. Behind the addon kill switch; it never presses Complete.
/// </para>
/// </summary>
public sealed unsafe class QuestResultHint : AddonPanelSource
{
    /// <summary>Name of the game's quest-complete window.</summary>
    public const string Addon = "JournalResult";

    /// <summary>Quests listed before the panel folds the rest into "and N more".</summary>
    public const int MaxOpened = 5;

    /// <summary>How far back a completion the poller saw still counts as the window's quest.</summary>
    public const int RecentCompletionMinutes = 10;

    private const long RetryInterval = 250;
    private const long RetryWindow = 3000;

    private readonly QuestBriefBuilder briefs;
    private readonly List<string> texts = [];
    private readonly List<QuestRecord> candidates = [];
    private readonly Func<QuestRecord, bool> prefer;

    private uint completed;
    private long openedAt;

    private uint modelRowId;
    private int modelVersion = -1;
    private QuestResultModel? model;

    public QuestResultHint(IAddonLifecycle lifecycle, IGameGui gameGui, QuestBriefBuilder briefs, HookGate gate, IPluginLog log)
        : base(lifecycle, gameGui, gate, log, Addon, RetryInterval)
    {
        this.briefs = briefs ?? throw new ArgumentNullException(nameof(briefs));
        prefer = InJournal;
        Start();
    }

    protected override bool WantsPoll => completed == 0 && Environment.TickCount64 - openedAt < RetryWindow;

    /// <summary>The panel's model, or null when there is nothing to show. Framework thread.</summary>
    public QuestResultModel? Current()
    {
        if (!IsActive || !IsOpen || completed == 0 || briefs.Session.Bundle is not { } bundle)
        {
            return null;
        }

        if (completed != modelRowId || briefs.Session.Version != modelVersion || model is null)
        {
            modelRowId = completed;
            modelVersion = briefs.Session.Version;
            model = bundle.Catalog.GetByRowId(completed) is { } quest ? Build(quest, bundle) : null;
        }

        return model;
    }

    protected override void Read(AtkUnitBase* addon)
    {
        if (completed == 0 && openedAt == 0)
        {
            openedAt = Environment.TickCount64;
        }

        if (briefs.Session.Bundle is not { } bundle)
        {
            return;
        }

        var catalog = bundle.Catalog;
        var result = (AddonJournalResult*)addon;
        texts.Clear();
        texts.Add(NodeText(result->AtkTextNode250));
        texts.Add(NodeText(result->AtkTextNode258));
        CollectValues(addon, texts, null);

        candidates.Clear();
        if (briefs.Session.LiveSnapshot is { } live)
        {
            foreach (var accepted in live.Accepted)
            {
                if (catalog.GetByQuestId(accepted.QuestId) is { } quest)
                {
                    candidates.Add(quest);
                }
            }
        }

        var since = DateTime.UtcNow.AddMinutes(-RecentCompletionMinutes);
        foreach (var e in briefs.Session.RecentEvents)
        {
            if (e.Kind == QuestEventKind.Completed && e.TimeUtc >= since && catalog.GetByRowId(e.RowId) is { } quest && !candidates.Contains(quest))
            {
                candidates.Add(quest);
            }
        }

        var match = QuestIdentifier.Identify(texts, candidates, briefs.Titles(catalog), prefer);
        if (match.Quest is { } found)
        {
            completed = found.RowId;
        }
    }

    protected override void Forget()
    {
        completed = 0;
        openedAt = 0;
        model = null;
        modelRowId = 0;
    }

    private bool InJournal(QuestRecord quest) => briefs.StateOf(quest) is QuestState.Accepted or QuestState.Completed;

    private QuestResultModel Build(QuestRecord quest, CatalogBundle bundle)
    {
        var session = briefs.Session;
        var brief = briefs.Build(quest, bundle);
        var spoilers = briefs.Spoilers;
        var opened = session.Index is { } index
            ? NewlyOpened.By(quest.RowId, bundle.Catalog, index, briefs.States, session.FeatureQuestIds)
            : [];
        var lines = new List<OpenedLine>(Math.Min(opened.Count, MaxOpened));
        for (var i = 0; i < opened.Count && i < MaxOpened; i++)
        {
            var o = opened[i];
            var name = spoilers.DisplayName(o.Quest);
            lines.Add(new OpenedLine(o.Quest, briefs.StateOf(o.Quest), o.OtherJob ? string.Format(CultureInfo.CurrentCulture, Strings.GamePanelOtherJobFormat, name) : name));
        }

        var (msq, feature, other) = NewlyOpened.Count(opened, session.FeatureQuestIds);
        var summary = opened.Count == 0 ? Strings.GamePanelOpenedNone : Strings.GamePanelOpenedSummary(msq, feature, other);
        var more = opened.Count > MaxOpened
            ? string.Format(CultureInfo.CurrentCulture, Strings.GamePanelMoreFormat, opened.Count - MaxOpened)
            : string.Empty;
        OpenedLine? next = brief.ChainNext is { } chainNext
            ? new OpenedLine(chainNext, briefs.StateOf(chainNext), spoilers.DisplayName(chainNext))
            : null;
        return new QuestResultModel(brief, summary, lines, more, next);
    }
}

/// <summary>What the "What this opened" panel shows for the quest being turned in.</summary>
/// <param name="Brief">The quest's brief (its unlocks and chain line among them).</param>
/// <param name="Summary">"Opens 1 main scenario quest, 2 unlock quests" or "Opens no new quests".</param>
/// <param name="Opened">The first <see cref="QuestResultHint.MaxOpened"/> quests it opens.</param>
/// <param name="MoreText">"and N more" when it opens more; empty otherwise.</param>
/// <param name="ChainNext">The next quest of its chain; null outside a chain or at its end.</param>
public sealed record QuestResultModel(QuestBrief Brief, string Summary, IReadOnlyList<OpenedLine> Opened, string MoreText, OpenedLine? ChainNext);

/// <summary>One quest line of the "What this opened" panel, its name through the spoiler shield.</summary>
public sealed record OpenedLine(QuestRecord Quest, QuestState State, string Name);
