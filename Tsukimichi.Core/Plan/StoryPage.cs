using System.Runtime.CompilerServices;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Route;

namespace Tsukimichi.Core.Plan;

/// <summary>One optional line on Your story, for the character: where it stands, named through the shield.</summary>
/// <param name="Chain">The line (a curated or derived chain, or a side story, <see cref="ChainCatalog"/>).</param>
/// <param name="Name">Its name through the shield (<see cref="ChainCatalog.DisplayName"/>); empty when <see cref="StoryGroup.Hidden"/>.</param>
/// <param name="StoryNeedsIt">The main scenario needs one of its quests (<see cref="StoryRequirements.SideQuests"/>).</param>
/// <param name="Left">Quests left (<see cref="ChainCatalog.Progress"/>); 0 once done.</param>
/// <param name="Next">The next quest to do; null once done.</param>
/// <param name="NextState">The next quest's state (Completed once done).</param>
public sealed record StoryLine(Chain Chain, string Name, bool StoryNeedsIt, int Left, QuestRecord? Next, QuestState NextState)
{
    /// <summary>Every counted quest is done.</summary>
    public bool IsDone => Next is null;

    /// <summary>The next quest sits in the character's journal.</summary>
    public bool InJournal => NextState == QuestState.Accepted;
}

/// <summary>The lines that open after one main scenario quest, inside a band.</summary>
/// <param name="OpensAfter">The main scenario quest that gates them (<see cref="StoryPage"/>); null for the lines open from the start.</param>
/// <param name="OpensAfterName">Its name through the shield (its placeholder when masked); empty for the start.</param>
/// <param name="Hidden">The gate lies past the story point: the group shows a count, never the lines' names.</param>
public sealed record StoryGroup(QuestRecord? OpensAfter, string OpensAfterName, bool Hidden, IReadOnlyList<StoryLine> Lines);

/// <summary>One patch band of Your story: a main scenario journal category ("Seventh Astral Era", "Heavensward").</summary>
/// <param name="Expansion">ExVersion row id of its quests.</param>
/// <param name="ExpansionName">The expansion's name (never a spoiler).</param>
/// <param name="Title">The category's name without "Main Scenario Quests"; a spoiler past the story point.</param>
/// <param name="Patches">"2.1 – 2.5", "3.0" (<see cref="PatchVersion.Series"/>); empty when the quests carry no patch.</param>
/// <param name="FirstOfExpansion">The expansion's first band (its x.0 story): titled by the expansion's name.</param>
/// <param name="Past">Its first quest lies past the story point (the shield masks it): counts only.</param>
/// <param name="StoryTotal">Main scenario quests in the band that count for the character.</param>
/// <param name="StoryLeft">Of those, the ones not done.</param>
/// <param name="Next">The band's first main scenario quest not done; null once done.</param>
/// <param name="NextName">Its name through the shield.</param>
/// <param name="Groups">The optional lines by the quest that opens them, in story order.</param>
public sealed record StoryBand(
    byte Expansion,
    string ExpansionName,
    string Title,
    string Patches,
    bool FirstOfExpansion,
    bool Past,
    int StoryTotal,
    int StoryLeft,
    QuestRecord? Next,
    string NextName,
    IReadOnlyList<StoryGroup> Groups)
{
    /// <summary>Every main scenario quest of the band is done.</summary>
    public bool IsDone => StoryLeft == 0;

    /// <summary>Optional lines placed in the band.</summary>
    public int LineCount { get; } = Groups.Sum(static g => g.Lines.Count);

    /// <summary>Optional lines placed in the band with quests left.</summary>
    public int LinesLeft { get; } = Groups.Sum(static g => g.Lines.Count(static l => !l.IsDone));
}

/// <summary>What <see cref="StoryPage.Build"/> reads for one character.</summary>
/// <param name="Catalog">The quest catalog.</param>
/// <param name="Chains">The catalog's chains (<see cref="ChainCatalog"/>): the optional lines.</param>
/// <param name="States">The character's evaluations; empty reads every quest as not done.</param>
/// <param name="IsMasked">Whether the spoiler shield hides a quest (<see cref="SpoilerMask.IsMasked(QuestRecord)"/>).</param>
/// <param name="QuestName">A quest's name through the shield.</param>
/// <param name="ExpansionName">An expansion's name.</param>
/// <param name="StoryRequired">The side quests the story needs (<see cref="StoryRequirements.SideQuests"/>); null knows none.</param>
public sealed record StoryPageInputs(
    QuestCatalog Catalog,
    ChainCatalog Chains,
    IReadOnlyDictionary<uint, QuestEvaluation> States,
    Func<QuestRecord, bool> IsMasked,
    Func<QuestRecord, string> QuestName,
    Func<byte, string> ExpansionName,
    IReadOnlySet<uint>? StoryRequired = null);

/// <summary>
/// Your story on one page (feature plan v7 N9, spec-1.21): the main scenario by patch band (one band per main scenario
/// journal category), each optional line placed under the main scenario quest that opens it, with what is left.
/// <para>
/// <b>Placement.</b> A line opens after the latest main scenario quest its first quest needs: the prerequisite graph the
/// unlock routes walk (<see cref="QuestCatalog.PrerequisitesOf"/>: previous quests and quest accept conditions), where
/// an All join needs its latest gate and an Any join its earliest live one (the cheapest way in, as
/// <see cref="UnlockRoute"/> chooses), a main scenario quest being its own gate. A line no story quest gates opens
/// from the start, in the first band. Lines made only of main scenario quests (the journal's post-patch genres) and
/// seasonal lines are not optional lines. The placement is computed once per catalog and chain catalog.
/// </para>
/// <para>
/// <b>Spoilers.</b> A band whose first quest the shield masks is past the story point and shows counts only; inside a
/// band, a group whose gate the shield masks names neither the gate (its placeholder) nor its lines. With the shield
/// off nothing is masked.
/// </para>
/// </summary>
public sealed class StoryPage
{
    private static readonly ConditionalWeakTable<QuestCatalog, Holder> Cache = [];

    private StoryPage(IReadOnlyList<StoryBand> bands, int storyLeft, byte minLevel, byte maxLevel)
    {
        Bands = bands;
        StoryLeft = storyLeft;
        MinLevelLeft = minLevel;
        MaxLevelLeft = maxLevel;
    }

    /// <summary>The bands in story order.</summary>
    public IReadOnlyList<StoryBand> Bands { get; }

    /// <summary>Main scenario quests left to the latest story.</summary>
    public int StoryLeft { get; }

    /// <summary>The lowest and highest level among the main scenario quests left; 0 when none is.</summary>
    public byte MinLevelLeft { get; }

    public byte MaxLevelLeft { get; }

    /// <summary>The page for one character.</summary>
    public static StoryPage Build(StoryPageInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var layout = Layout.For(inputs.Catalog, inputs.Chains);
        var states = inputs.States;
        var bands = new List<StoryBand>(layout.Bands.Count);
        var storyLeft = 0;
        byte min = 0;
        byte max = 0;
        foreach (var band in layout.Bands)
        {
            var total = 0;
            var left = 0;
            QuestRecord? next = null;
            foreach (var quest in band.Quests)
            {
                var evaluation = states.GetValueOrDefault(quest.RowId);
                if (evaluation is { LeavesTotals: true })
                {
                    continue;
                }

                total++;
                if (evaluation?.State == QuestState.Completed)
                {
                    continue;
                }

                left++;
                next ??= quest;
                min = min == 0 ? quest.DisplayLevel : Math.Min(min, quest.DisplayLevel);
                max = Math.Max(max, quest.DisplayLevel);
            }

            storyLeft += left;
            var past = inputs.IsMasked(band.Quests[0]);
            var groups = new List<StoryGroup>(band.Groups.Count);
            foreach (var (gate, chains) in band.Groups)
            {
                var hidden = past || (gate is not null && inputs.IsMasked(gate));
                var lines = new List<StoryLine>(chains.Count);
                foreach (var chain in chains)
                {
                    var progress = ChainCatalog.Progress(chain, states);
                    if (progress.IsEmpty)
                    {
                        continue;
                    }

                    var first = inputs.Catalog.GetByRowId(chain.RowIds[0]);
                    var nameHidden = hidden || (first is not null && inputs.IsMasked(first));
                    var nextQuest = progress.NextRowId is { } id ? inputs.Catalog.GetByRowId(id) : null;
                    var nextState = nextQuest is null ? QuestState.Completed : states.GetValueOrDefault(nextQuest.RowId)?.State ?? QuestState.Unknown;
                    var needed = inputs.StoryRequired is { } required && chain.RowIds.Any(required.Contains);
                    lines.Add(new StoryLine(
                        chain,
                        nameHidden ? string.Empty : ChainCatalog.DisplayName(chain, rowId => inputs.Catalog.GetByRowId(rowId) is { } q ? inputs.QuestName(q) : string.Empty),
                        needed,
                        progress.Total - progress.Done,
                        nextQuest,
                        nextState));
                }

                if (lines.Count > 0)
                {
                    groups.Add(new StoryGroup(gate, gate is null ? string.Empty : inputs.QuestName(gate), hidden, lines));
                }
            }

            bands.Add(new StoryBand(
                band.Quests[0].Expansion,
                inputs.ExpansionName(band.Quests[0].Expansion),
                band.Title,
                band.Patches,
                band.FirstOfExpansion,
                past,
                total,
                left,
                next,
                next is null ? string.Empty : inputs.QuestName(next),
                groups));
        }

        return new StoryPage(bands, storyLeft, min, max);
    }

    /// <summary>
    /// The main scenario quest index (in <see cref="MsqGraph.Story"/>) a quest waits for: itself for a story quest, else
    /// the latest its prerequisites need (see the class summary); -1 when no story quest gates it. Exposed for tests.
    /// </summary>
    public static int GateOf(QuestCatalog catalog, uint rowId) => new Layout(catalog, ChainCatalog.Empty).Gate(rowId);

    private sealed class Holder
    {
        public ChainCatalog? Chains { get; set; }

        public Layout? Value { get; set; }
    }

    /// <summary>The bands and the lines' places, the same for every character.</summary>
    private sealed class Layout
    {
        private readonly QuestCatalog catalog;
        private readonly Dictionary<uint, int> storyIndex = [];
        private readonly Dictionary<uint, int> memo = [];
        private readonly IReadOnlyList<QuestRecord> story;

        public Layout(QuestCatalog catalog, ChainCatalog chains)
        {
            this.catalog = catalog;
            story = MsqGraph.For(catalog).Story;
            for (var i = 0; i < story.Count; i++)
            {
                storyIndex[story[i].RowId] = i;
            }

            // Bands: runs of story quests sharing a journal category, in story order.
            var bands = new List<(int Start, int End)>();
            for (var i = 0; i < story.Count; i++)
            {
                if (bands.Count == 0 || story[bands[^1].Start].Journal.CategoryId != story[i].Journal.CategoryId)
                {
                    bands.Add((i, i));
                }
                else
                {
                    bands[^1] = (bands[^1].Start, i);
                }
            }

            var bandOf = new int[story.Count];
            for (var b = 0; b < bands.Count; b++)
            {
                for (var i = bands[b].Start; i <= bands[b].End; i++)
                {
                    bandOf[i] = b;
                }
            }

            // Lines, placed at their gate.
            var placed = new List<(int Gate, Chain Chain, QuestRecord First)>();
            foreach (var chain in chains.Chains)
            {
                if (chain.RowIds.Count == 0 || catalog.GetByRowId(chain.RowIds[0]) is not { } first || !IsOptional(chain))
                {
                    continue;
                }

                placed.Add((Gate(first.RowId), chain, first));
            }

            // A line no story quest gates (a job that needs only its expansion and a level) opens with its expansion: the
            // first band of that expansion, or the first band.
            int StartBand(QuestRecord first)
            {
                for (var b = 0; b < bands.Count; b++)
                {
                    if (story[bands[b].Start].Expansion >= first.Expansion)
                    {
                        return b;
                    }
                }

                return 0;
            }

            var result = new List<BandLayout>(bands.Count);
            var seenExpansions = new HashSet<byte>();
            for (var b = 0; b < bands.Count; b++)
            {
                var quests = story.Skip(bands[b].Start).Take(bands[b].End - bands[b].Start + 1).ToArray();
                // A line opens after its gate, so it sits in the band of the story quest that follows the gate: lines
                // opened by a band's last quest (The Ultimate Weapon) lead the next band, where they can be done.
                var groups = placed
                    .Where(p => (p.Gate < 0 ? StartBand(p.First) : bandOf[Math.Min(p.Gate + 1, story.Count - 1)]) == b)
                    .OrderBy(static p => p.Gate)
                    .ThenBy(static p => p.First.DisplayLevel)
                    .ThenBy(static p => p.First.Journal.SortKey)
                    .GroupBy(static p => p.Gate)
                    .Select(g => (g.Key < 0 ? null : story[g.Key], (IReadOnlyList<Chain>)g.Select(static p => p.Chain).ToArray()))
                    .ToArray();
                // The band's patches, of its own major version: a quest a later patch reworked into it (2.0's 6.1
                // rewrites) does not stretch "2.0" to "2.0 – 6.1".
                var series = quests.Select(static q => PatchVersion.Series(q.AddedIn)).Where(static p => p.Length > 0).Distinct().Order(PatchVersion.Comparer).ToArray();
                var major = series.Length == 0 ? string.Empty : series[0][..(series[0].IndexOf('.', StringComparison.Ordinal) + 1)];
                var patches = series.Where(p => p.StartsWith(major, StringComparison.Ordinal)).ToArray();
                var range = patches.Length == 0 ? string.Empty : patches.Length == 1 ? patches[0] : patches[0] + " – " + patches[^1];
                result.Add(new BandLayout(quests, UnlockRoute.MilestoneName(quests[0].Journal), range, seenExpansions.Add(quests[0].Expansion), groups));
            }

            Bands = result;
        }

        public IReadOnlyList<BandLayout> Bands { get; }

        public static Layout For(QuestCatalog catalog, ChainCatalog chains)
        {
            var holder = Cache.GetValue(catalog, static _ => new Holder());
            lock (holder)
            {
                if (holder.Value is null || !ReferenceEquals(holder.Chains, chains))
                {
                    holder.Chains = chains;
                    holder.Value = new Layout(catalog, chains);
                }

                return holder.Value;
            }
        }

        /// <summary>An optional line: not only main scenario quests, and not a seasonal event's.</summary>
        private bool IsOptional(Chain chain)
        {
            var side = false;
            foreach (var id in chain.RowIds)
            {
                if (catalog.GetByRowId(id) is not { } quest || quest.Festival != 0)
                {
                    return false;
                }

                side |= !storyIndex.ContainsKey(id);
            }

            return side;
        }

        public int Gate(uint rowId)
        {
            lock (memo)
            {
                return GateOf(rowId);
            }
        }

        private int GateOf(uint rowId)
        {
            if (storyIndex.TryGetValue(rowId, out var index))
            {
                return index;
            }

            if (memo.TryGetValue(rowId, out var known))
            {
                return known;
            }

            // A cycle in the data reads as no gate rather than recursing forever.
            memo[rowId] = -1;
            var result = -1;
            if (catalog.GetByRowId(rowId) is { } quest)
            {
                var previous = catalog.PrerequisitesOf(quest);
                if (previous.Join == JoinKind.All)
                {
                    foreach (var id in previous.QuestIds)
                    {
                        result = Math.Max(result, GateOf(id));
                    }
                }
                else if (previous.QuestIds.Length > 0)
                {
                    // Any one will do: the earliest live way in, a removed one only when every one is.
                    var live = previous.QuestIds.Where(id => catalog.GetByRowId(id) is { IsRemoved: false }).ToArray();
                    result = (live.Length > 0 ? live : previous.QuestIds).Min(GateOf);
                }
            }

            memo[rowId] = result;
            return result;
        }
    }

    private sealed record BandLayout(QuestRecord[] Quests, string Title, string Patches, bool FirstOfExpansion, IReadOnlyList<(QuestRecord? Gate, IReadOnlyList<Chain> Chains)> Groups);
}
