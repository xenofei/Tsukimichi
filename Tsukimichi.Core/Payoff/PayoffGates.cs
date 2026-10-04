using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Payoff;

/// <summary>A curated gate with its milestone and its content resolved against the catalog.</summary>
/// <param name="Content">Row ids of the optional content, in the order listed (a chain: journal order); never empty.</param>
public sealed record ResolvedPayoffGate(PayoffGate Gate, QuestRecord Milestone, IReadOnlyList<uint> Content);

/// <summary>A gate to show now: its milestone is Ready or in the journal and its content is not all completed.</summary>
/// <param name="MilestoneState"><see cref="QuestState.Ready"/>, <see cref="QuestState.ReadyOnOtherJob"/> or <see cref="QuestState.Accepted"/>.</param>
/// <param name="Done">Content quests completed.</param>
/// <param name="Total">Content quests in all; always more than <paramref name="Done"/>.</param>
public sealed record ActivePayoffGate(ResolvedPayoffGate Resolved, QuestState MilestoneState, int Done, int Total)
{
    public PayoffGate Gate => Resolved.Gate;

    public QuestRecord Milestone => Resolved.Milestone;
}

/// <summary>
/// "Before you continue" payoff gates (P5): which curated gates speak for a character now. A gate speaks only while
/// its milestone quest is Ready or in the journal and at least one quest of its optional content is not completed;
/// before the milestone is reached, once it is turned in, and once the content is done it is silent, so the plugin
/// never lists the pairs for the whole game. The gate's instruction names only the optional content (the curated
/// file's rule, pinned by a lint test); its reason is for the caller to show behind a closed "why?".
/// <para>
/// Built once per catalog (the resolution walks chains); <see cref="Active(IReadOnlyDictionary{uint, QuestEvaluation})"/>
/// is a scan over a handful of gates and their content and allocates only its result.
/// </para>
/// </summary>
public sealed class PayoffGates
{
    public static readonly PayoffGates Empty = new([], []);

    private PayoffGates(IReadOnlyList<ResolvedPayoffGate> gates, IReadOnlyList<string> warnings)
    {
        Gates = gates;
        Warnings = warnings;
    }

    /// <summary>The gates whose milestone and content were found, in file order.</summary>
    public IReadOnlyList<ResolvedPayoffGate> Gates { get; }

    /// <summary>One line per gate left out (an unknown or removed milestone, an unknown chain, no live content quest).</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Resolves the curated gates: the milestone must be a live quest of the catalog; <c>before</c> ids are kept when
    /// they are live quests, a chain name becomes every live, non-retired quest of the chain's genres in journal order.
    /// A gate left with no content, or without its milestone, is dropped with a warning.
    /// </summary>
    public static PayoffGates Build(QuestCatalog catalog, CuratedData curated)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(curated);
        if (curated.PayoffGates.Count == 0)
        {
            return Empty;
        }

        var gates = new List<ResolvedPayoffGate>(curated.PayoffGates.Count);
        var warnings = new List<string>();
        foreach (var gate in curated.PayoffGates)
        {
            if (catalog.GetByRowId(gate.MilestoneRowId) is not { IsRemoved: false } milestone)
            {
                warnings.Add($"payoff gate \"{gate.Id}\": milestone {gate.MilestoneRowId} is not a live quest of the catalog; skipped.");
                continue;
            }

            var content = new List<uint>();
            if (gate.BeforeChain is { } chainName)
            {
                var chain = curated.Chains.FirstOrDefault(c => string.Equals(c.Name, chainName, StringComparison.Ordinal));
                if (chain is null)
                {
                    warnings.Add($"payoff gate \"{gate.Id}\": chain \"{chainName}\" is not in chains.json; skipped.");
                    continue;
                }

                foreach (var rowId in chain.StartQuest != 0 ? Chains.ChainCatalog.GrowFrom(chain.StartQuest, catalog) : chain.QuestIds)
                {
                    if (catalog.GetByRowId(rowId) is { IsRemoved: false, IsRetired: false } && rowId != milestone.RowId && !content.Contains(rowId))
                    {
                        content.Add(rowId);
                    }
                }

                foreach (var genreId in chain.GenreIds)
                {
                    if (!catalog.ByGenre.TryGetValue(genreId, out var quests))
                    {
                        continue;
                    }

                    foreach (var quest in quests)
                    {
                        if (!quest.IsRemoved && !quest.IsRetired && quest.RowId != milestone.RowId && !content.Contains(quest.RowId))
                        {
                            content.Add(quest.RowId);
                        }
                    }
                }
            }
            else
            {
                foreach (var rowId in gate.BeforeRowIds)
                {
                    if (catalog.GetByRowId(rowId) is { IsRemoved: false } && rowId != milestone.RowId)
                    {
                        content.Add(rowId);
                    }
                    else
                    {
                        warnings.Add($"payoff gate \"{gate.Id}\": before {rowId} is not a live quest of the catalog; left out.");
                    }
                }
            }

            if (content.Count == 0)
            {
                warnings.Add($"payoff gate \"{gate.Id}\": no live quest left in its content; skipped.");
                continue;
            }

            gates.Add(new ResolvedPayoffGate(gate, milestone, content.ToArray()));
        }

        return new PayoffGates(gates, warnings);
    }

    /// <summary>The gates to show for a character's evaluations; none without evaluations (browse mode, no capture).</summary>
    public IReadOnlyList<ActivePayoffGate> Active(IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        return states.Count == 0 ? [] : Active(new EvaluationSource(states));
    }

    /// <summary>The gates to show for a plain state map; missing rows read as <see cref="QuestState.Unknown"/> (never Ready, never completed).</summary>
    public IReadOnlyList<ActivePayoffGate> Active(IReadOnlyDictionary<uint, QuestState> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        return states.Count == 0 ? [] : Active(new StateMapSource(states));
    }

    private List<ActivePayoffGate> Active<TSource>(TSource source)
        where TSource : struct, IStateSource
    {
        List<ActivePayoffGate>? result = null;
        foreach (var gate in Gates)
        {
            var milestoneState = source.StateOf(gate.Milestone.RowId);
            if (milestoneState is not (QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Accepted))
            {
                continue;
            }

            var done = 0;
            foreach (var rowId in gate.Content)
            {
                if (source.StateOf(rowId) == QuestState.Completed)
                {
                    done++;
                }
            }

            if (done < gate.Content.Count)
            {
                (result ??= []).Add(new ActivePayoffGate(gate, milestoneState, done, gate.Content.Count));
            }
        }

        return result ?? [];
    }

    /// <summary>
    /// The one-time chat notice (shared notice budget: once per gate per character): the active gates not in
    /// <paramref name="noticed"/>, each added to it as it is returned, so the caller persists the set and a gate is
    /// never announced twice for the character, across logins included.
    /// </summary>
    public static List<ActivePayoffGate> TakeNotices(IReadOnlyList<ActivePayoffGate> active, ISet<string> noticed)
    {
        ArgumentNullException.ThrowIfNull(active);
        ArgumentNullException.ThrowIfNull(noticed);
        var result = new List<ActivePayoffGate>();
        foreach (var gate in active)
        {
            if (noticed.Add(gate.Gate.Id))
            {
                result.Add(gate);
            }
        }

        return result;
    }
}
