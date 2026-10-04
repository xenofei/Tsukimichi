using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unlocks;

namespace Tsukimichi.Core.Query;

/// <summary>The kind chips of Nearby (1.21.0 P7): what a zone row counts and the Here list keeps.</summary>
[Flags]
public enum ZoneKinds : byte
{
    None = 0,

    /// <summary>Quests the character can start now.</summary>
    Ready = 1,

    /// <summary>Unlock quests left (My blues).</summary>
    Blues = 2,

    /// <summary>Side story quests left (the journal artwork stories).</summary>
    SideStories = 4,

    /// <summary>Quests left with a Moonlit reward.</summary>
    Rewards = 8,

    All = Ready | Blues | SideStories | Rewards,
}

/// <summary>How Nearby orders its rows (1.21.0 P7).</summary>
public enum ZoneSort : byte
{
    /// <summary>Closest to the current job's level first.</summary>
    LevelFit,

    /// <summary>Most Ready quests first.</summary>
    ReadyFirst,

    /// <summary>The game's own order: the story's zones, lowest level first.</summary>
    StoryOrder,
}

/// <summary>What one quest counts for on the board (<see cref="ZoneBoard.Build"/>).</summary>
/// <param name="Blue">An unlock quest (My blues).</param>
/// <param name="SideStory">A side story quest.</param>
/// <param name="Reward">A quest with a Moonlit reward the character does not have.</param>
public readonly record struct ZoneQuestKinds(bool Blue, bool SideStory, bool Reward);

/// <summary>One zone on the board.</summary>
/// <param name="Zone">The zone.</param>
/// <param name="MinLevel">The lowest level of the zone's quests.</param>
/// <param name="MaxLevel">The highest.</param>
/// <param name="Ready">Quests startable now whose giver stands here.</param>
/// <param name="Blues">Unlock quests left here.</param>
/// <param name="SideStories">Side story quests left here.</param>
/// <param name="Rewards">Quests left here with a Moonlit reward the character lacks.</param>
/// <param name="IsNew">A quest here came with the newest patch series ("new since 7.4").</param>
/// <param name="Masked">The zone lies past the story point: its name and counts are never shown.</param>
public sealed record ZoneLine(UnlockZone Zone, byte MinLevel, byte MaxLevel, int Ready, int Blues, int SideStories, int Rewards, bool IsNew, bool Masked)
{
    /// <summary>The count of one kind.</summary>
    public int Count(ZoneKinds kind) => kind switch
    {
        ZoneKinds.Ready => Ready,
        ZoneKinds.Blues => Blues,
        ZoneKinds.SideStories => SideStories,
        ZoneKinds.Rewards => Rewards,
        _ => 0,
    };

    /// <summary>Whether anything of the <paramref name="kinds"/> is left here.</summary>
    public bool HasAny(ZoneKinds kinds) =>
        ((kinds & ZoneKinds.Ready) != 0 && Ready > 0)
        || ((kinds & ZoneKinds.Blues) != 0 && Blues > 0)
        || ((kinds & ZoneKinds.SideStories) != 0 && SideStories > 0)
        || ((kinds & ZoneKinds.Rewards) != 0 && Rewards > 0);

    /// <summary>How far the job's level is from the zone's span: 0 inside it, else the distance to its nearer end.</summary>
    public int Fit(int level) => level < MinLevel ? MinLevel - level : level > MaxLevel ? level - MaxLevel : 0;
}

/// <summary>One expansion's group on the board.</summary>
/// <param name="Expansion">ExVersion row id.</param>
/// <param name="MinLevel">The lowest level of its zones' quests.</param>
/// <param name="MaxLevel">The highest.</param>
/// <param name="Zones">Zones with something left of the chosen kinds, in the chosen order; masked zones last.</param>
/// <param name="Empty">Zones with nothing left of the chosen kinds (not masked), in the game's order: "2 zones with nothing left".</param>
/// <param name="Masked">The whole expansion lies past the story point: one line, no zone named.</param>
/// <param name="Fits">The group fitting the job's level: it comes first and opens.</param>
public sealed record ZoneExpansion(byte Expansion, byte MinLevel, byte MaxLevel, IReadOnlyList<ZoneLine> Zones, IReadOnlyList<ZoneLine> Empty, bool Masked, bool Fits)
{
    /// <summary>Ready quests across its zones (not masked).</summary>
    public int Ready => Zones.Where(static z => !z.Masked).Sum(static z => z.Ready);

    /// <summary>Every zone of the expansion the board knows.</summary>
    public int ZoneCount => Zones.Count + Empty.Count;
}

/// <summary>
/// The zones board (feature plan v7, 1.21.0 P7; spec-1.21 P7): Nearby's Everywhere view. Every town and field zone,
/// grouped by expansion, with what is left there in words: quests startable now, blues, side stories and Moonlit
/// rewards, each counted only when its kind chip is on. Pure.
/// <para>
/// <b>A zone's quests</b> are those whose giver stands there, still in the game and counted in totals (not
/// repeatables). Its level span is theirs, done or not. A quest is left when it is not done (Completed, done this
/// cycle) and not out of the totals (locked out, out of season, a spare alternative).
/// </para>
/// <para>
/// <b>Order.</b> The group whose level span holds the job's level comes first (the highest such; above every span, the
/// highest expansion), then the others nearest first; an expansion past the story point comes last as one line. Zones
/// follow <see cref="ZoneSort"/>, ties in the game's own order.
/// </para>
/// <para>
/// <b>Spoilers.</b> An expansion past the character's story reach is masked whole. Inside a revealed one, a zone the
/// shield masks is listed after the others as "A zone ahead" with its level span, never with its counts.
/// </para>
/// </summary>
public static class ZoneBoard
{
    /// <summary>Builds the board.</summary>
    /// <param name="zones">The town and field zones (<see cref="QuestUnlocks.Zones"/>).</param>
    /// <param name="kindsOf">What a quest counts for besides Ready; null counts nothing but Ready.</param>
    /// <param name="kinds">The kind chips on.</param>
    /// <param name="sort">The order of zones in a group.</param>
    /// <param name="jobLevel">The current job's level, for the fit; 0 when unknown (the first group fits).</param>
    /// <param name="includeOtherJob">Ready on another job counts as Ready (Nearby's setting).</param>
    /// <param name="reachExpansion">The expansion the story has reached (<see cref="SpoilerMask.ReachExpansion"/>); 255 reveals all.</param>
    /// <param name="zoneMasked">Whether the shield hides a zone name; null hides none.</param>
    /// <param name="isNew">Whether a quest came with the newest patch series; null marks none.</param>
    public static IReadOnlyList<ZoneExpansion> Build(
        IReadOnlyList<UnlockZone> zones,
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        Func<QuestRecord, ZoneQuestKinds>? kindsOf,
        ZoneKinds kinds,
        ZoneSort sort,
        int jobLevel,
        bool includeOtherJob = true,
        byte reachExpansion = byte.MaxValue,
        Func<string, bool>? zoneMasked = null,
        Func<QuestRecord, bool>? isNew = null)
    {
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        zoneMasked ??= static _ => false;

        var byTerritory = new Dictionary<uint, UnlockZone>();
        foreach (var zone in zones)
        {
            if (zone is not null && zone.Name.Length > 0)
            {
                byTerritory.TryAdd(zone.TerritoryId, zone);
            }
        }

        var tallies = new Dictionary<uint, Tally>();
        foreach (var quest in catalog.All)
        {
            if (quest.IsRemoved || quest.IsRepeatable || !quest.CountsInTotals || quest.Issuer is not { } issuer || !byTerritory.ContainsKey(issuer.TerritoryId))
            {
                continue;
            }

            if (!tallies.TryGetValue(issuer.TerritoryId, out var tally))
            {
                tallies[issuer.TerritoryId] = tally = new Tally();
            }

            tally.Level(quest.DisplayLevel);
            tally.IsNew |= isNew?.Invoke(quest) == true;
            var evaluation = states.GetValueOrDefault(quest.RowId);
            var state = evaluation?.State ?? QuestState.Unknown;
            if (state is QuestState.Completed or QuestState.DoneThisCycle || evaluation is { LeavesTotals: true })
            {
                continue;
            }

            if (state == QuestState.Ready || (includeOtherJob && state == QuestState.ReadyOnOtherJob))
            {
                tally.Ready++;
            }

            if (kindsOf?.Invoke(quest) is { } of)
            {
                tally.Blues += of.Blue ? 1 : 0;
                tally.SideStories += of.SideStory ? 1 : 0;
                tally.Rewards += of.Reward ? 1 : 0;
            }
        }

        var groups = new List<ZoneExpansion>();
        foreach (var expansion in byTerritory.Values.Select(static z => z.Expansion).Distinct().Order())
        {
            var lines = new List<ZoneLine>();
            foreach (var zone in byTerritory.Values.Where(z => z.Expansion == expansion))
            {
                if (!tallies.TryGetValue(zone.TerritoryId, out var tally) || tally.Min > tally.Max)
                {
                    continue;
                }

                lines.Add(new ZoneLine(zone, tally.Min, tally.Max, tally.Ready, tally.Blues, tally.SideStories, tally.Rewards, tally.IsNew, zoneMasked(zone.Name)));
            }

            if (lines.Count == 0)
            {
                continue;
            }

            var min = lines.Min(static l => l.MinLevel);
            var max = lines.Max(static l => l.MaxLevel);
            var masked = expansion > reachExpansion;
            var left = lines.Where(l => l.Masked || l.HasAny(kinds)).ToList();
            var empty = lines.Where(l => !l.Masked && !l.HasAny(kinds)).OrderBy(static l => l.Zone.SortKey).ThenBy(static l => l.MinLevel).ThenBy(static l => l.Zone.TerritoryId).ToArray();
            left.Sort((a, b) => Compare(a, b, sort, kinds, jobLevel));
            groups.Add(new ZoneExpansion(expansion, min, max, left, empty, masked, Fits: false));
        }

        return Order(groups, jobLevel);
    }

    /// <summary>The order of two zones of one group: masked last, then by <paramref name="sort"/>, then the game's order.</summary>
    public static int Compare(ZoneLine a, ZoneLine b, ZoneSort sort, ZoneKinds kinds, int jobLevel)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var byMask = a.Masked.CompareTo(b.Masked);
        if (byMask != 0)
        {
            return byMask;
        }

        var by = 0;
        if (sort == ZoneSort.LevelFit)
        {
            by = a.Fit(jobLevel).CompareTo(b.Fit(jobLevel));
        }
        else if (sort == ZoneSort.ReadyFirst)
        {
            by = (kinds & ZoneKinds.Ready) != 0 ? b.Ready.CompareTo(a.Ready) : 0;
            if (by == 0)
            {
                by = Left(b, kinds).CompareTo(Left(a, kinds));
            }
        }
        if (by != 0)
        {
            return by;
        }

        var byLevel = a.MinLevel.CompareTo(b.MinLevel);
        if (byLevel != 0)
        {
            return byLevel;
        }

        var byKey = a.Zone.SortKey.CompareTo(b.Zone.SortKey);
        return byKey != 0 ? byKey : a.Zone.TerritoryId.CompareTo(b.Zone.TerritoryId);
    }

    /// <summary>
    /// The groups in board order: the one fitting <paramref name="jobLevel"/> first (marked <see cref="ZoneExpansion.Fits"/>),
    /// the other revealed ones nearest it first (lower before higher at the same distance), masked ones last.
    /// </summary>
    private static IReadOnlyList<ZoneExpansion> Order(List<ZoneExpansion> groups, int jobLevel)
    {
        var revealed = groups.Where(static g => !g.Masked).ToList();
        if (revealed.Count == 0)
        {
            return groups;
        }

        var fit = revealed.LastOrDefault(g => jobLevel >= g.MinLevel && jobLevel <= g.MaxLevel)
                  ?? (jobLevel > revealed.Max(static g => g.MaxLevel) ? revealed[^1] : revealed[0]);
        var at = revealed.IndexOf(fit);
        var ordered = new List<ZoneExpansion>(groups.Count) { fit with { Fits = true } };
        ordered.AddRange(revealed
            .Where(g => !ReferenceEquals(g, fit))
            .OrderBy(g => Math.Abs(revealed.IndexOf(g) - at))
            .ThenBy(static g => g.Expansion));
        ordered.AddRange(groups.Where(static g => g.Masked));
        return ordered;
    }

    private static int Left(ZoneLine line, ZoneKinds kinds) =>
        ((kinds & ZoneKinds.Ready) != 0 ? line.Ready : 0) + ((kinds & ZoneKinds.Blues) != 0 ? line.Blues : 0)
        + ((kinds & ZoneKinds.SideStories) != 0 ? line.SideStories : 0) + ((kinds & ZoneKinds.Rewards) != 0 ? line.Rewards : 0);

    private sealed class Tally
    {
        public byte Min { get; private set; } = byte.MaxValue;

        public byte Max { get; private set; }

        public int Ready { get; set; }

        public int Blues { get; set; }

        public int SideStories { get; set; }

        public int Rewards { get; set; }

        public bool IsNew { get; set; }

        public void Level(byte level)
        {
            if (level == 0)
            {
                return;
            }

            Min = Math.Min(Min, level);
            Max = Math.Max(Max, level);
        }
    }
}
