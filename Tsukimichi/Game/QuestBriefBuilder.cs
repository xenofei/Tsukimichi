using System;
using System.Collections.Generic;
using System.Globalization;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.GamePanels;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Unique;
using Tsukimichi.GameData;
using Tsukimichi.Ui;

namespace Tsukimichi.Game;

/// <summary>
/// What the panels beside the quest-offer, quest-complete and Journal windows (1.7.0) say about one quest, every
/// string built once when the model changes: its state and name for the logged-in character, the verdict line, its
/// Moonlit rewards (owned or not), what it unlocks, its chain step and the facts line (patch, repeatable, seasonal).
/// </summary>
/// <param name="Quest">The quest (for Open in Tsukimichi, Pin, Flag giver and Route).</param>
/// <param name="State">Its state for the logged-in character.</param>
/// <param name="Name">Its name through the logged-in character's spoiler shield.</param>
/// <param name="Masked">The shield hides it: the panel says nothing past the placeholder.</param>
/// <param name="StatusText"><see cref="BlockerText.StatusText"/>: the state word and the decisive blocker.</param>
/// <param name="Verdict"><see cref="QuestVerdict.Line"/>; empty when <paramref name="Masked"/>.</param>
/// <param name="Moonlit">Its Moonlit rewards; empty when masked.</param>
/// <param name="Unlocks">"Dungeon: Aglaia", one per unlock; empty when masked.</param>
/// <param name="ChainLine">"Step 3 of 7 · Hildibrand", or empty outside a chain.</param>
/// <param name="ChainNext">The chain's step after this one; null for the last step or outside a chain.</param>
/// <param name="ChainNextName">Its name through the spoiler shield; empty without one.</param>
/// <param name="FactsLine">"Added in 7.5 · Repeatable · Seasonal event", or empty.</param>
public sealed record QuestBrief(
    QuestRecord Quest,
    QuestState State,
    string Name,
    bool Masked,
    string StatusText,
    string Verdict,
    IReadOnlyList<MoonlitBriefLine> Moonlit,
    IReadOnlyList<string> Unlocks,
    string ChainLine,
    QuestRecord? ChainNext,
    string ChainNextName,
    string FactsLine);

/// <summary>One Moonlit reward line: "Wind-up Sun" with whether the logged-in character has it (null: cannot tell).</summary>
public sealed record MoonlitBriefLine(string Name, bool? Owned, string StatusWord);

/// <summary>
/// Builds <see cref="QuestBrief"/>s for the logged-in character (its states, blocker names and spoiler shield; the
/// viewed character's while nobody is logged in), and holds the catalog's title index for the panels' quest matching.
/// Framework thread.
/// </summary>
public sealed class QuestBriefBuilder
{
    private const string FactSeparator = " · ";

    private readonly SessionState session;
    private readonly Func<UniqueRewardCatalog> moonlit;
    private readonly RewardUnlockReader unlocks;
    private readonly Func<UnlockTags> tags;
    private readonly List<MoonlitReward> verdictRewards = [];
    private QuestTitleIndex titles = QuestTitleIndex.Empty;

    /// <param name="moonlit">The Moonlit catalog (shipped data merged with the user's overrides), read per build.</param>
    /// <param name="tags">The plan's unlock tags (built once per catalog by the plan source), read per build.</param>
    public QuestBriefBuilder(SessionState session, Func<UniqueRewardCatalog> moonlit, RewardUnlockReader unlocks, Func<UnlockTags> tags)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.moonlit = moonlit ?? throw new ArgumentNullException(nameof(moonlit));
        this.unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
        this.tags = tags ?? throw new ArgumentNullException(nameof(tags));
    }

    public SessionState Session => session;

    /// <summary>How many of the unlock index's rows the panel adds after the plan's tags.</summary>
    private const int MaxIndexLabels = 4;

    /// <summary>
    /// What every quest opens (feature plan v6 K4): its areas, aetherytes, duties and features join the unlock lines, and
    /// its headline ("Unlocks Kugane") the verdict when the plan's tags name nothing more specific. Null leaves both out.
    /// </summary>
    public Func<Core.Unlocks.QuestUnlocks?>? QuestUnlocks { get; set; }

    private static bool NamedAlready(IReadOnlyList<PlanUnlock> opens, string name)
    {
        foreach (var unlock in opens)
        {
            if (string.Equals(PlanDutiesKey(unlock.Name), PlanDutiesKey(name), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string PlanDutiesKey(string name)
    {
        var trimmed = name.Trim();
        return (trimmed.StartsWith("the ", StringComparison.OrdinalIgnoreCase) ? trimmed[4..] : trimmed).ToLowerInvariant();
    }

    /// <summary>The logged-in character's evaluations; the viewed character's while nobody is logged in.</summary>
    public IReadOnlyDictionary<uint, QuestEvaluation> States => session.LiveStates.Count > 0 ? session.LiveStates : session.States;

    /// <summary>The blocker names that go with <see cref="States"/>.</summary>
    public BlockerNames Names => session.LiveStates.Count > 0 ? session.LiveNames : session.Names;

    /// <summary>The logged-in character's spoiler shield (the viewed one's while nobody is logged in, as the session decides).</summary>
    public SpoilerMask Spoilers => session.LiveSpoilers;

    /// <summary>The title index of the loaded catalog, built on first use after a catalog change.</summary>
    public QuestTitleIndex Titles(QuestCatalog catalog)
    {
        if (!ReferenceEquals(titles.Catalog, catalog))
        {
            titles = QuestTitleIndex.For(catalog);
        }

        return titles;
    }

    /// <summary>Whether <paramref name="quest"/> is one the logged-in character can take now (Ready, or Ready on another job).</summary>
    public bool IsReady(QuestRecord quest) =>
        States.TryGetValue(quest.RowId, out var evaluation) && evaluation.State is QuestState.Ready or QuestState.ReadyOnOtherJob;

    /// <summary>The quest's state for the logged-in character.</summary>
    public QuestState StateOf(QuestRecord quest) => States.GetValueOrDefault(quest.RowId)?.State ?? QuestState.Unknown;

    public QuestBrief Build(QuestRecord quest, CatalogBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(bundle);
        var states = States;
        var spoilers = Spoilers;
        var evaluation = states.GetValueOrDefault(quest.RowId);
        var state = evaluation?.State ?? QuestState.Unknown;
        var masked = spoilers.IsMasked(quest);

        var moonlitLines = new List<MoonlitBriefLine>();
        var unlockLabels = new List<string>();
        verdictRewards.Clear();
        IReadOnlyList<PlanUnlock> opens = [];
        string? opensHeadline = null;
        if (!masked)
        {
            // The reader answers for the viewed character; while another one is viewed it would speak for the wrong
            // character, so the panel says it cannot tell (as the item hint does).
            var forLive = session.IsLive;
            foreach (var entry in moonlit().ForQuest(quest.RowId))
            {
                var owned = forLive ? unlocks.IsObtained(entry) : null;
                var name = RewardNames.Display(entry, quest, bundle.Language);
                verdictRewards.Add(new MoonlitReward(name, owned));
                moonlitLines.Add(new MoonlitBriefLine(name, owned, owned switch
                {
                    true => Strings.GamePanelOwned,
                    false => Strings.GamePanelNotOwned,
                    _ => Strings.GamePanelOwnedUnknown,
                }));
            }

            opens = QuestVerdict.Unlocks(quest, tags().For(quest.RowId));
            foreach (var unlock in opens)
            {
                unlockLabels.Add(unlock.Label);
            }

            // Every quest's areas, aetherytes, duties and features from the unlock index (feature plan v6 K4), after
            // the plan's tags and without repeating what they name.
            if (QuestUnlocks?.Invoke() is { } index)
            {
                var added = 0;
                foreach (var entry in index.For(quest.RowId))
                {
                    if (entry.Group > Core.Unlocks.UnlockGroup.Feature || added >= MaxIndexLabels || NamedAlready(opens, entry.Name))
                    {
                        continue;
                    }

                    unlockLabels.Add(Core.Unlocks.UnlockTargets.Name(entry.Target) + ": " + entry.Name);
                    added++;
                }

                if (index.Headline(quest.RowId) is { Group: <= Core.Unlocks.UnlockGroup.Feature } headline)
                {
                    opensHeadline = headline.Name;
                }
            }
        }

        var chainLine = string.Empty;
        string chainName = string.Empty;
        ChainStep? step = null;
        QuestRecord? next = null;
        if (session.Chains.ForQuest(quest.RowId) is { } chain && QuestVerdict.StepOf(chain, quest.RowId) is { } found)
        {
            step = found;
            chainName = ChainCatalog.DisplayName(chain, id => spoilers.DisplayName(bundle.Catalog, id, id.ToString(CultureInfo.InvariantCulture)));
            chainLine = string.Format(CultureInfo.CurrentCulture, Strings.GamePanelChainFormat, found.Position, found.Total, chainName);
            next = found.NextRowId is { } nextId ? bundle.Catalog.GetByRowId(nextId) : null;
        }

        var verdict = QuestVerdict.Line(quest, masked, opens, verdictRewards, step, chainName, opensHeadline);
        return new QuestBrief(
            quest,
            state,
            spoilers.DisplayName(quest),
            masked,
            BlockerText.StatusText(evaluation, quest, Names, states),
            verdict,
            moonlitLines,
            unlockLabels,
            chainLine,
            next,
            next is null ? string.Empty : spoilers.DisplayName(next),
            Facts(quest));
    }

    /// <summary>"Added in 7.5 · Allied society daily · Seasonal event": what the window does not say about the quest.</summary>
    private static string Facts(QuestRecord quest)
    {
        var facts = new List<string>(3);
        if (quest.AddedIn.Length > 0)
        {
            facts.Add(string.Format(CultureInfo.CurrentCulture, Strings.GamePanelAddedInFormat, quest.AddedIn));
        }

        if (quest.IsAlliedSocietyDaily)
        {
            facts.Add(Strings.GamePanelSocietyDaily);
        }
        else if (quest.IsRepeatable)
        {
            facts.Add(Strings.GamePanelRepeatable);
        }

        if (quest.Festival != 0)
        {
            facts.Add(Strings.GamePanelSeasonal);
        }

        return string.Join(FactSeparator, facts);
    }
}
