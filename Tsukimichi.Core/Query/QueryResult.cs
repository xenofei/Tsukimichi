using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>One table row: the quest, its resolved state and the pre-materialized Status text (<see cref="Evaluation.BlockerText.StatusText"/>).</summary>
public readonly record struct QuestRow(QuestRecord Quest, QuestState State, string Status);

/// <summary>
/// Why a query produced no rows. <see cref="Filters"/> lists, in panel order, each filter that would alone have
/// restored rows had it been removed; it is empty when only a combination of filters (or an empty scope) is to blame.
/// </summary>
public sealed record EmptyReason(IReadOnlyList<string> Filters, bool ScopeIsEmpty)
{
    public static readonly EmptyReason Scope = new([], true);
    public static readonly EmptyReason Combination = new([], false);
}

/// <param name="Rows">Filtered and sorted rows.</param>
/// <param name="Empty">Null when <paramref name="Rows"/> is non-empty.</param>
/// <param name="TotalInScope">Quests under the selected node before filters and search (Unlisted excluded unless included).</param>
public sealed record QueryResult(QuestRow[] Rows, EmptyReason? Empty, int TotalInScope);
