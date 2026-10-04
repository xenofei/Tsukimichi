using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Core.Runtime;

/// <summary>Which group a Wotsit entry is registered in; lower groups are registered first.</summary>
public enum WotsitPriority : byte
{
    /// <summary>A quest in the journal, Ready, or Ready on another job.</summary>
    Active,

    /// <summary>A Moonlit reward, whatever its quest's state.</summary>
    Reward,

    /// <summary>A quest still ahead but not takeable now: Blocked, done for today, or not checked.</summary>
    Open,

    /// <summary>A quest Completed or Locked out.</summary>
    Finished,
}

/// <summary>
/// One Wotsit entry before the plugin adds its display text, icon and action: the quest it reveals, the reward it
/// stands for (null for a quest entry), the name it shows and the text Wotsit matches the query against.
/// </summary>
public sealed record WotsitItem(QuestRecord Quest, UniqueRewardEntry? Reward, string Name, string SearchText)
{
    public bool IsReward => Reward is not null;
}

/// <summary>
/// What Tsukimichi registers with Wotsit, and in which order. Wotsit (goaaats/Dalamud.FindAnything) matches a query
/// against each entry's search text with a fuzzy matcher by default (letters in order, gaps allowed) and keeps only
/// the first 26 matches per plugin, counted in registration order before it sorts the results by score. So the search
/// text is the shown name and nothing else (a genre or expansion word would let every quest of that genre match a
/// short query), and the quests a player is most likely looking for are registered first: <see cref="WotsitPriority"/>
/// order, the catalog's journal order within a group (story order for the main scenario).
/// </summary>
public static class WotsitOrder
{
    /// <summary>
    /// The entries for every listed catalog quest (removed quests are skipped), then every Moonlit reward whose quest is
    /// in the catalog. A quest <paramref name="spoilers"/> masks is named, and matched, by its placeholder alone. A
    /// reward is named by its reward name, or by its kind (<paramref name="kindName"/>) when it has none.
    /// </summary>
    /// <param name="language">The catalog's language (<c>CatalogBundle.Language</c>), for <see cref="RewardNames.Display"/>.</param>
    public static List<WotsitItem> Items(QuestCatalog catalog, UniqueRewardCatalog rewards, Func<RewardKind, string> kindName, string? language, SpoilerMask? spoilers = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(rewards);
        ArgumentNullException.ThrowIfNull(kindName);
        spoilers ??= SpoilerMask.None;

        var items = new List<WotsitItem>(catalog.Count + rewards.Count);
        foreach (var quest in catalog.All)
        {
            if (quest.IsRemoved)
            {
                // Removed from the game (retired, or left without a journal genre): nothing to find on the map, and
                // a retired twin with the same name is listed.
                continue;
            }

            var name = spoilers.DisplayName(quest);
            items.Add(new WotsitItem(quest, null, name, name));
        }

        foreach (var entry in rewards.All)
        {
            if (catalog.GetByRowId(entry.QuestRowId) is not { } quest)
            {
                continue;
            }

            var rewardName = RewardNames.Display(entry, quest, language);
            if (spoilers.IsNameMasked(SpoilerKind.Reward, rewardName))
            {
                // A reward the story has not introduced (1.20.0 N6) is not registered: a placeholder finds nothing.
                continue;
            }

            var name = string.IsNullOrWhiteSpace(rewardName) ? kindName(entry.Kind) : rewardName;
            items.Add(new WotsitItem(quest, entry, name, name));
        }

        return items;
    }

    /// <summary>The group of a quest in <paramref name="state"/>; null (not evaluated) counts as <see cref="WotsitPriority.Open"/>.</summary>
    public static WotsitPriority ForState(QuestState? state) => state switch
    {
        QuestState.Accepted or QuestState.Ready or QuestState.ReadyOnOtherJob => WotsitPriority.Active,
        QuestState.Completed or QuestState.Foreclosed => WotsitPriority.Finished,
        _ => WotsitPriority.Open,
    };

    /// <summary>
    /// Each item's group: <see cref="WotsitPriority.Reward"/> for a reward, its quest's state in
    /// <paramref name="states"/> (keyed by row id) for a quest.
    /// </summary>
    public static WotsitPriority[] Priorities<T>(IReadOnlyList<T> items, Func<T, WotsitItem> item, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(states);
        var priorities = new WotsitPriority[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            var source = item(items[i]);
            priorities[i] = source.IsReward
                ? WotsitPriority.Reward
                : ForState(states.TryGetValue(source.Quest.RowId, out var evaluation) ? evaluation.State : null);
        }

        return priorities;
    }

    /// <summary>The groups to register with before any state is known: rewards ahead of quests, which read Not checked.</summary>
    public static WotsitPriority[] Unevaluated<T>(IReadOnlyList<T> items, Func<T, WotsitItem> item) =>
        Priorities(items, item, EmptyStates);

    /// <summary>Positions <c>0..n-1</c> in registration order: by group, keeping the list's own order within a group.</summary>
    public static List<int> Order(IReadOnlyList<WotsitPriority> priorities)
    {
        ArgumentNullException.ThrowIfNull(priorities);
        var order = new List<int>(priorities.Count);
        for (var group = WotsitPriority.Active; group <= WotsitPriority.Finished; group++)
        {
            for (var i = 0; i < priorities.Count; i++)
            {
                if (priorities[i] == group)
                {
                    order.Add(i);
                }
            }
        }

        return order;
    }

    /// <summary>
    /// Whether the registration order must change: some entry moved to another group. A state change inside a group
    /// (Ready to In journal, Blocked to Done today) keeps the order, so it costs no re-registration.
    /// </summary>
    public static bool GroupsChanged(IReadOnlyList<WotsitPriority> registered, IReadOnlyList<WotsitPriority> next) =>
        RegistrationDiff.ChangedIndices(registered, next, static (a, b) => a == b) is not { Count: 0 };

    private static readonly IReadOnlyDictionary<uint, QuestEvaluation> EmptyStates = new Dictionary<uint, QuestEvaluation>();
}
