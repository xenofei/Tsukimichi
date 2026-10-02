using System.Globalization;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Route;

/// <summary>Why a quest is on the Next stops list; the lower value wins when a quest comes from several sources.</summary>
public enum StopReason : byte
{
    /// <summary>The followed route's next stop.</summary>
    Route,

    /// <summary>A pin.</summary>
    Pin,

    /// <summary>An unlock quest of the expansion pinned from My blues.</summary>
    Blue,

    /// <summary>Any other quest near the character's level.</summary>
    Side,
}

/// <summary>Where a stop is: the aetheryte nearest its givers (the teleport destination) and its name.</summary>
public sealed record StopPlace(uint AetheryteId, string Name);

/// <summary>One quest at a stop and why it is there; <paramref name="IsUnlock"/> for an unlock ("blue") quest.</summary>
public sealed record StopQuest(QuestRecord Quest, StopReason Reason, bool IsUnlock);

/// <summary>Ready quests whose givers share the nearest aetheryte: one teleport, several quests (1.6.0, R6 B).</summary>
/// <param name="IsHere">A giver of the stop stands in the zone the character is in.</param>
/// <param name="Quests">The stop's quests: the route's stop first, then pins, blues and the rest, unlock quests first within each, then by level.</param>
public sealed record Stop(StopPlace Place, bool IsHere, IReadOnlyList<StopQuest> Quests)
{
    public int Count => Quests.Count;

    /// <summary>Unlock quests among <see cref="Quests"/>.</summary>
    public int UnlockCount => Quests.Count(static q => q.IsUnlock);

    /// <summary>The lowest level among the stop's quests.</summary>
    public byte MinLevel => Quests.Count == 0 ? (byte)0 : Quests.Min(static q => q.Quest.DisplayLevel);

    /// <summary>How much the stop is worth: a quest counts once, an unlock quest <see cref="StopPlanner.UnlockWeight"/> times.</summary>
    public int Score => Count + ((StopPlanner.UnlockWeight - 1) * UnlockCount);

    /// <summary>"3 quests · 1 unlock" for the stop's line.</summary>
    public string CountText
    {
        get
        {
            var text = Count == 1
                ? CoreText.T("Core.Stops.CountOne", "1 quest")
                : string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Stops.Count", "{0} quests"), Count);
            var unlocks = UnlockCount;
            return unlocks switch
            {
                0 => text,
                1 => text + " · " + CoreText.T("Core.Stops.UnlockOne", "1 unlock"),
                _ => text + " · " + string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Stops.Unlocks", "{0} unlocks"), unlocks),
            };
        }
    }
}

/// <summary>Everything <see cref="StopPlanner.Plan"/> reads.</summary>
/// <param name="Catalog">The quest catalog.</param>
/// <param name="States">The character's evaluations by quest row id.</param>
/// <param name="PlaceOf">The aetheryte nearest a quest's giver; null leaves the quest out (nowhere to teleport).</param>
/// <param name="Pinned">The character's pins.</param>
/// <param name="RouteStop">The followed route's next stop (<see cref="ActiveRoute.NextStop"/>); null when none.</param>
/// <param name="Blues">The unlock quests of the expansion pinned from My blues.</param>
/// <param name="UnlockQuestIds">Unlock ("blue") quests, weighted when ranking stops.</param>
/// <param name="Level">The character's level on its current job; 0 leaves the other quests out.</param>
/// <param name="LevelRange">How far below <paramref name="Level"/> another quest may be and still count (it is Ready, so never above).</param>
/// <param name="TerritoryId">The zone the character stands in; 0 when unknown.</param>
public sealed record StopInputs(
    QuestCatalog Catalog,
    IReadOnlyDictionary<uint, QuestEvaluation> States,
    Func<QuestRecord, StopPlace?> PlaceOf,
    IReadOnlyList<uint> Pinned,
    uint? RouteStop,
    IEnumerable<uint> Blues,
    IReadOnlySet<uint> UnlockQuestIds,
    short Level,
    int LevelRange = StopPlanner.DefaultLevelRange,
    uint TerritoryId = 0);

/// <summary>
/// "Next stops" (1.6.0, R6 B): the quests the character can pick up right now, batched by where to teleport. The
/// Ready set is the followed route's next stop, the pins, the Ready unlock quests of the expansion pinned from My
/// blues, and every other Ready quest within <see cref="StopInputs.LevelRange"/> levels of the character (main
/// scenario, repeatable, seasonal and removed quests left out of that last group). Only Ready quests count: Ready on
/// another job needs a job change first, and an accepted quest has already been picked up. Each quest is keyed by
/// the aetheryte nearest its giver and quests sharing one form a stop. Order: a stop in the current zone first, then
/// the stop with more quests (an unlock quest counting <see cref="UnlockWeight"/> times), then the lower level, then
/// the aetheryte id. Pure.
/// </summary>
public static class StopPlanner
{
    /// <summary>How many times an unlock quest counts when stops are ranked.</summary>
    public const int UnlockWeight = 2;

    /// <summary>How far below the character's level a quest of the last group may be.</summary>
    public const int DefaultLevelRange = 5;

    public static IReadOnlyList<Stop> Plan(StopInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.Catalog);
        ArgumentNullException.ThrowIfNull(inputs.States);
        ArgumentNullException.ThrowIfNull(inputs.PlaceOf);
        ArgumentNullException.ThrowIfNull(inputs.Pinned);
        ArgumentNullException.ThrowIfNull(inputs.Blues);
        ArgumentNullException.ThrowIfNull(inputs.UnlockQuestIds);

        var reasons = new Dictionary<uint, StopReason>();
        void Add(uint rowId, StopReason reason)
        {
            if (!IsReady(inputs, rowId))
            {
                return;
            }

            if (!reasons.TryGetValue(rowId, out var known) || reason < known)
            {
                reasons[rowId] = reason;
            }
        }

        if (inputs.RouteStop is { } stop)
        {
            Add(stop, StopReason.Route);
        }

        foreach (var rowId in inputs.Pinned)
        {
            Add(rowId, StopReason.Pin);
        }

        foreach (var rowId in inputs.Blues)
        {
            Add(rowId, StopReason.Blue);
        }

        if (inputs.Level > 0)
        {
            var floor = inputs.Level - Math.Max(0, inputs.LevelRange);
            foreach (var quest in inputs.Catalog.All)
            {
                if (quest.DisplayLevel >= floor && IsSide(quest))
                {
                    Add(quest.RowId, StopReason.Side);
                }
            }
        }

        var byPlace = new Dictionary<uint, (StopPlace Place, List<StopQuest> Quests, bool Here)>();
        foreach (var (rowId, reason) in reasons)
        {
            var quest = inputs.Catalog.ByRowId[rowId];
            if (inputs.PlaceOf(quest) is not { } place)
            {
                continue;
            }

            if (!byPlace.TryGetValue(place.AetheryteId, out var group))
            {
                group = (place, [], false);
            }

            group.Quests.Add(new StopQuest(quest, reason, inputs.UnlockQuestIds.Contains(rowId)));
            group.Here |= inputs.TerritoryId != 0 && quest.Issuer?.TerritoryId == inputs.TerritoryId;
            byPlace[place.AetheryteId] = group;
        }

        var stops = new List<Stop>(byPlace.Count);
        foreach (var (_, group) in byPlace)
        {
            group.Quests.Sort(static (a, b) =>
                a.Reason != b.Reason ? a.Reason.CompareTo(b.Reason)
                : a.IsUnlock != b.IsUnlock ? b.IsUnlock.CompareTo(a.IsUnlock)
                : a.Quest.DisplayLevel != b.Quest.DisplayLevel ? a.Quest.DisplayLevel.CompareTo(b.Quest.DisplayLevel)
                : a.Quest.RowId.CompareTo(b.Quest.RowId));
            stops.Add(new Stop(group.Place, group.Here, group.Quests));
        }

        stops.Sort(static (a, b) =>
            a.IsHere != b.IsHere ? b.IsHere.CompareTo(a.IsHere)
            : a.Score != b.Score ? b.Score.CompareTo(a.Score)
            : a.MinLevel != b.MinLevel ? a.MinLevel.CompareTo(b.MinLevel)
            : a.Place.AetheryteId.CompareTo(b.Place.AetheryteId));
        return stops;
    }

    private static bool IsReady(StopInputs inputs, uint rowId) =>
        inputs.Catalog.ByRowId.ContainsKey(rowId)
        && inputs.States.TryGetValue(rowId, out var evaluation)
        && evaluation.State == QuestState.Ready
        && !evaluation.IsSpareAlternative;

    /// <summary>A quest of the last group: no main scenario, nothing repeatable or seasonal, nothing removed.</summary>
    private static bool IsSide(QuestRecord quest) =>
        !FeaturePresets.IsMainScenario(quest) && !quest.IsRepeatable && quest.Festival == 0 && !quest.IsRemoved;
}
